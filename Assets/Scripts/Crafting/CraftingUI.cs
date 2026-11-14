using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// The workbench window, built in code in the same dark-and-gold style as the HUD. Created automatically.
///   Tabs: TOOLS / SURVIVAL / BUILD.  Left: the recipes of the tab.  Right: the selected recipe -
///   big icon, description, what it does, every ingredient as "have / need" (green or red), and CRAFT.
///   CRAFT runs a short progress bar while the player works at the table (craft animation, anvil sounds),
///   then takes the materials and gives the result.
/// Also draws the small quick-item slots above the hotbar ([H] Salve, [J] Potion) whenever you own some.
/// </summary>
public class CraftingUI : MonoBehaviour
{
    static CraftingUI instance;
    public static bool IsOpen { get; private set; }

    // ---- window
    RectTransform canvasRect, window;
    CanvasGroup group;
    float openT;
    readonly Button[] tabs = new Button[3];
    readonly TextMeshProUGUI[] tabLabels = new TextMeshProUGUI[3];
    RectTransform listRoot;
    readonly List<Row> rows = new List<Row>();
    CraftCategory tab = CraftCategory.Tools;
    Recipe selected;

    // ---- detail
    Image detailIcon;
    TextMeshProUGUI detailName, detailTag, detailDesc, detailEffect, blockerText, craftLabel;
    RectTransform ingredientsRoot;
    readonly List<IngredientRow> ingRows = new List<IngredientRow>();
    Button craftButton;
    Image craftFill, craftBg;

    // ---- crafting progress
    Recipe crafting;
    float craftT;
    float nextClang;
    AudioSource audioSource;
    AudioClip clang, done;

    // ---- toast + quick bar
    TextMeshProUGUI toast;
    float toastT = -1f;
    QuickSlot salveSlot, potionSlot;

    // look input (same trick as InventoryUI)
    MonoBehaviour lookInputs;
    FieldInfo lookEnabledField, lookVectorField;

    static Sprite panelSprite, slotSprite, slotSelSprite;
    static readonly Dictionary<string, Sprite> icons = new Dictionary<string, Sprite>();

    class Row
    {
        public Recipe recipe;
        public Button button;
        public Image bg, sel, icon;
        public TextMeshProUGUI name, status;
        public CanvasGroup group;
    }

    class IngredientRow
    {
        public RectTransform root;
        public Image icon;
        public TextMeshProUGUI name, count;
    }

    class QuickSlot
    {
        public RectTransform root;
        public CanvasGroup group;
        public TextMeshProUGUI count;
        public float alpha;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (instance != null) return;
        instance = new GameObject("CraftingUI").AddComponent<CraftingUI>();
    }

    public static void Open(Workbench bench) { if (instance != null) instance.DoOpen(); }
    public static void Close() { if (instance != null) instance.DoClose(); }

    // ------------------------------------------------------------------ build

    static Sprite Icon(string name)
    {
        Sprite s;
        if (icons.TryGetValue(name, out s)) return s;
        s = Resources.Load<Sprite>("Crafting/" + name);
        icons[name] = s;
        return s;
    }

    public static Sprite IconFor(Recipe r)
    {
        if (r.isToolUpgrade) return Icon(r.tool == ToolType.Axe ? "Icon_Axe" : r.tool == ToolType.Pickaxe ? "Icon_Pickaxe" : "Icon_Sword");
        switch (r.item)
        {
            case CraftItem.HerbSalve: return Icon("Icon_HerbSalve") ?? Icon("Item_Herb");
            case CraftItem.HealthPotion: return Icon("Icon_HealthPotion") ?? Icon("Item_Ruby");
            case CraftItem.Torch: return Icon("Icon_Torch") ?? Icon("Item_Amber");
            case CraftItem.Campfire: return Icon("Icon_Campfire") ?? Icon("Item_Wood");
            default: return Icon("Icon_SpikeTrap") ?? Icon("Item_Stone");
        }
    }

    static Sprite ItemIcon(ItemType t) { return Icon("Item_" + t); }

    void Awake()
    {
        panelSprite = Resources.Load<Sprite>("Combat/UI_Panel");
        slotSprite = Resources.Load<Sprite>("Crafting/Slot");
        slotSelSprite = Resources.Load<Sprite>("Crafting/SlotSelected");

        canvasRect = CombatSkin.MakeCanvas(transform, "CraftingCanvas", 30);
        canvasRect.gameObject.AddComponent<GraphicRaycaster>();
        EnsureEventSystem();

        // ---- dim background
        GameObject root = new GameObject("Root", typeof(RectTransform));
        RectTransform rrt = (RectTransform)root.transform;
        rrt.SetParent(canvasRect, false);
        Stretch(rrt);
        group = root.AddComponent<CanvasGroup>();
        group.alpha = 0f; group.blocksRaycasts = false; group.interactable = false;
        Image dim = root.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.62f);

        // ---- window
        GameObject w = new GameObject("Window", typeof(RectTransform));
        window = (RectTransform)w.transform;
        window.SetParent(rrt, false);
        CombatSkin.Place(window, new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(1180f, 720f), new Vector2(0.5f, 0.5f));
        Image wp = w.AddComponent<Image>();
        wp.sprite = panelSprite; wp.type = Image.Type.Sliced; wp.pixelsPerUnitMultiplier = 0.9f;
        if (panelSprite == null) wp.color = new Color(0.06f, 0.05f, 0.04f, 0.95f);

        TextMeshProUGUI title = CombatSkin.Text(window, "WORKBENCH", 34f, CombatSkin.Gold, TextAlignmentOptions.TopLeft);
        title.characterSpacing = 14f;
        CombatSkin.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(46f, -32f), new Vector2(600f, 46f), new Vector2(0f, 1f));
        TextMeshProUGUI sub = CombatSkin.Text(window, "Shape what the maze gives you into what keeps you alive.", 16f, CombatSkin.Cream, TextAlignmentOptions.TopLeft);
        sub.fontStyle = FontStyles.Italic; sub.outlineWidth = 0f; sub.color = new Color(0.86f, 0.8f, 0.66f, 0.85f);
        CombatSkin.Place(sub.rectTransform, new Vector2(0f, 1f), new Vector2(48f, -80f), new Vector2(700f, 26f), new Vector2(0f, 1f));
        TextMeshProUGUI hint = CombatSkin.Text(window, "[E] / [Esc]  Close", 15f, new Color(0.75f, 0.68f, 0.55f), TextAlignmentOptions.TopRight);
        hint.outlineWidth = 0f;
        CombatSkin.Place(hint.rectTransform, new Vector2(1f, 1f), new Vector2(-46f, -40f), new Vector2(300f, 26f), new Vector2(1f, 1f));
        Image rule = MakeImage(window, null, new Color(0.93f, 0.76f, 0.38f, 0.55f));
        CombatSkin.Place(rule.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -116f), new Vector2(1090f, 2f), new Vector2(0.5f, 1f));

        // ---- tabs
        string[] tabNames = { "TOOLS", "SURVIVAL", "BUILD" };
        for (int i = 0; i < 3; i++)
        {
            int idx = i;
            Button b = MakeButton(window, slotSprite, out tabLabels[i], tabNames[i], 17f);
            CombatSkin.Place((RectTransform)b.transform, new Vector2(0f, 1f), new Vector2(46f + i * 160f, -136f), new Vector2(150f, 46f), new Vector2(0f, 1f));
            tabLabels[i].characterSpacing = 5f;
            b.onClick.AddListener(() => { tab = (CraftCategory)idx; selected = null; Rebuild(); Click(); });
            tabs[i] = b;
        }

        // ---- recipe list
        GameObject lr = new GameObject("List", typeof(RectTransform));
        listRoot = (RectTransform)lr.transform;
        listRoot.SetParent(window, false);
        CombatSkin.Place(listRoot, new Vector2(0f, 1f), new Vector2(46f, -200f), new Vector2(470f, 480f), new Vector2(0f, 1f));

        // ---- detail panel
        Image dpanel = MakeImage(window, slotSprite, new Color(1f, 1f, 1f, 0.55f));
        dpanel.type = Image.Type.Sliced; dpanel.pixelsPerUnitMultiplier = 0.6f;
        CombatSkin.Place(dpanel.rectTransform, new Vector2(1f, 1f), new Vector2(-46f, -136f), new Vector2(590f, 544f), new Vector2(1f, 1f));
        RectTransform d = dpanel.rectTransform;

        Image iconFrame = MakeImage(d, slotSprite, Color.white);
        iconFrame.type = Image.Type.Sliced;
        CombatSkin.Place(iconFrame.rectTransform, new Vector2(0f, 1f), new Vector2(28f, -28f), new Vector2(132f, 132f), new Vector2(0f, 1f));
        detailIcon = MakeImage(iconFrame.rectTransform, null, Color.white);
        detailIcon.preserveAspect = true;
        CombatSkin.Place(detailIcon.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(104f, 104f), new Vector2(0.5f, 0.5f));

        detailName = CombatSkin.Text(d, "", 28f, CombatSkin.Gold, TextAlignmentOptions.TopLeft);
        CombatSkin.Place(detailName.rectTransform, new Vector2(0f, 1f), new Vector2(182f, -30f), new Vector2(390f, 38f), new Vector2(0f, 1f));
        detailTag = CombatSkin.Text(d, "", 14f, new Color(0.75f, 0.68f, 0.55f), TextAlignmentOptions.TopLeft);
        detailTag.characterSpacing = 4f; detailTag.outlineWidth = 0f;
        CombatSkin.Place(detailTag.rectTransform, new Vector2(0f, 1f), new Vector2(184f, -70f), new Vector2(390f, 22f), new Vector2(0f, 1f));
        detailDesc = CombatSkin.Text(d, "", 16f, CombatSkin.Cream, TextAlignmentOptions.TopLeft);
        detailDesc.fontStyle = FontStyles.Italic; detailDesc.outlineWidth = 0f;
        detailDesc.textWrappingMode = TextWrappingModes.Normal;
        CombatSkin.Place(detailDesc.rectTransform, new Vector2(0f, 1f), new Vector2(184f, -96f), new Vector2(380f, 64f), new Vector2(0f, 1f));
        detailEffect = CombatSkin.Text(d, "", 17f, new Color(0.62f, 0.92f, 0.5f), TextAlignmentOptions.TopLeft);
        CombatSkin.Place(detailEffect.rectTransform, new Vector2(0f, 1f), new Vector2(30f, -176f), new Vector2(540f, 26f), new Vector2(0f, 1f));

        TextMeshProUGUI req = CombatSkin.Text(d, "REQUIRES", 14f, new Color(0.75f, 0.68f, 0.55f), TextAlignmentOptions.TopLeft);
        req.characterSpacing = 6f; req.outlineWidth = 0f;
        CombatSkin.Place(req.rectTransform, new Vector2(0f, 1f), new Vector2(30f, -218f), new Vector2(300f, 22f), new Vector2(0f, 1f));
        GameObject ir = new GameObject("Ingredients", typeof(RectTransform));
        ingredientsRoot = (RectTransform)ir.transform;
        ingredientsRoot.SetParent(d, false);
        CombatSkin.Place(ingredientsRoot, new Vector2(0f, 1f), new Vector2(30f, -246f), new Vector2(530f, 190f), new Vector2(0f, 1f));

        blockerText = CombatSkin.Text(d, "", 15f, new Color(1f, 0.45f, 0.35f), TextAlignmentOptions.Left);
        blockerText.outlineWidth = 0f;
        CombatSkin.Place(blockerText.rectTransform, new Vector2(0f, 0f), new Vector2(30f, 44f), new Vector2(230f, 40f), new Vector2(0f, 0.5f));

        craftButton = MakeButton(d, slotSelSprite, out craftLabel, "CRAFT", 22f);
        craftLabel.characterSpacing = 10f;
        craftBg = craftButton.GetComponent<Image>();
        CombatSkin.Place((RectTransform)craftButton.transform, new Vector2(1f, 0f), new Vector2(-26f, 22f), new Vector2(290f, 66f), new Vector2(1f, 0f));
        craftFill = MakeImage(craftButton.transform, null, new Color(1f, 0.78f, 0.3f, 0.35f));
        craftFill.type = Image.Type.Filled; craftFill.fillMethod = Image.FillMethod.Horizontal; craftFill.fillAmount = 0f;
        craftFill.raycastTarget = false;
        RectTransform cf = craftFill.rectTransform; cf.anchorMin = Vector2.zero; cf.anchorMax = Vector2.one; cf.offsetMin = new Vector2(8f, 8f); cf.offsetMax = new Vector2(-8f, -8f);
        craftFill.transform.SetSiblingIndex(0);
        craftButton.onClick.AddListener(StartCraft);

        // ---- toast (over the window)
        toast = CombatSkin.Text(rrt, "", 24f, CombatSkin.Gold, TextAlignmentOptions.Center);
        toast.characterSpacing = 6f;
        CombatSkin.Place(toast.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 400f), new Vector2(1000f, 40f), new Vector2(0.5f, 0.5f));
        toast.alpha = 0f;

        // ---- quick bar (always on, outside the window)
        salveSlot = MakeQuickSlot("H", Icon("Icon_HerbSalve") ?? Icon("Item_Herb"), 0);
        potionSlot = MakeQuickSlot("J", Icon("Icon_HealthPotion") ?? Icon("Item_Ruby"), 1);

        // ---- sound
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false; audioSource.spatialBlend = 0f;
        clang = MakeClang(false);
        done = MakeClang(true);
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static Image MakeImage(Transform parent, Sprite sprite, Color color)
    {
        GameObject go = new GameObject("Image", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Image img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        if (sprite != null && sprite.border.sqrMagnitude > 0f) img.type = Image.Type.Sliced;
        return img;
    }

    static Button MakeButton(Transform parent, Sprite sprite, out TextMeshProUGUI label, string text, float size)
    {
        GameObject go = new GameObject("Button", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Image img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.type = Image.Type.Sliced;
        img.raycastTarget = true;
        Button b = go.AddComponent<Button>();
        ColorBlock cb = b.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1f, 0.92f, 0.7f);
        cb.pressedColor = new Color(0.85f, 0.75f, 0.5f);
        cb.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.7f);
        b.colors = cb;
        label = CombatSkin.Text(go.transform, text, size, CombatSkin.Gold, TextAlignmentOptions.Center);
        Stretch(label.rectTransform);
        return b;
    }

    QuickSlot MakeQuickSlot(string key, Sprite icon, int index)
    {
        QuickSlot q = new QuickSlot();
        GameObject go = new GameObject("Quick" + key, typeof(RectTransform));
        q.root = (RectTransform)go.transform;
        q.root.SetParent(canvasRect, false);
        // to the right of the 4-slot hotbar
        CombatSkin.Place(q.root, new Vector2(0.5f, 0f), new Vector2(262f + index * 82f, 26f), new Vector2(72f, 72f), new Vector2(0f, 0f));
        q.group = go.AddComponent<CanvasGroup>();
        q.group.alpha = 0f; q.group.blocksRaycasts = false;
        Image bg = go.AddComponent<Image>();
        bg.sprite = slotSprite; bg.type = Image.Type.Sliced; bg.raycastTarget = false;
        Image ic = MakeImage(q.root, icon, Color.white);
        ic.preserveAspect = true;
        CombatSkin.Place(ic.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 2f), new Vector2(50f, 50f), new Vector2(0.5f, 0.5f));
        TextMeshProUGUI k = CombatSkin.Text(q.root, key, 15f, CombatSkin.Gold, TextAlignmentOptions.TopLeft);
        CombatSkin.Place(k.rectTransform, new Vector2(0f, 1f), new Vector2(8f, -4f), new Vector2(30f, 22f), new Vector2(0f, 1f));
        q.count = CombatSkin.Text(q.root, "", 16f, Color.white, TextAlignmentOptions.BottomRight);
        CombatSkin.Place(q.count.rectTransform, new Vector2(1f, 0f), new Vector2(-8f, 4f), new Vector2(50f, 24f), new Vector2(1f, 0f));
        return q;
    }

    static void EnsureEventSystem()
    {
        if (EventSystem.current != null || Object.FindAnyObjectByType<EventSystem>() != null) return;
        GameObject es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
    }

    // ------------------------------------------------------------------ open / close

    void DoOpen()
    {
        if (IsOpen) return;
        IsOpen = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SetLook(false);
        selected = null;
        Rebuild();
        Click();
    }

    void DoClose()
    {
        if (!IsOpen) return;
        IsOpen = false;
        crafting = null;
        craftFill.fillAmount = 0f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        SetLook(true);
    }

    void SetLook(bool on)
    {
        if (lookInputs == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                foreach (MonoBehaviour m in player.GetComponents<MonoBehaviour>())
                    if (m != null && m.GetType().Name == "StarterAssetsInputs")
                    {
                        lookInputs = m;
                        lookEnabledField = m.GetType().GetField("cursorInputForLook");
                        lookVectorField = m.GetType().GetField("look");
                        break;
                    }
        }
        if (lookInputs == null) return;
        if (lookEnabledField != null) lookEnabledField.SetValue(lookInputs, on);
        if (lookVectorField != null) lookVectorField.SetValue(lookInputs, Vector2.zero);
        // stop walking while the window is open
        FieldInfo move = lookInputs.GetType().GetField("move");
        if (move != null) move.SetValue(lookInputs, Vector2.zero);
    }

    // ------------------------------------------------------------------ content

    void Rebuild()
    {
        CraftingState cs = CraftingState.Instance;
        if (cs == null) return;

        for (int i = 0; i < 3; i++)
        {
            bool on = (int)tab == i;
            tabs[i].GetComponent<Image>().sprite = on ? slotSelSprite : slotSprite;
            tabLabels[i].color = on ? CombatSkin.Gold : new Color(0.75f, 0.68f, 0.55f);
        }

        foreach (Row r in rows) Destroy(r.button.gameObject);
        rows.Clear();
        int n = 0;
        foreach (Recipe rec in cs.Recipes)
        {
            if (rec.category != tab) continue;
            if (selected == null) selected = rec;
            rows.Add(MakeRow(rec, n++));
        }
        RefreshAll();
    }

    Row MakeRow(Recipe rec, int index)
    {
        Row r = new Row { recipe = rec };
        TextMeshProUGUI dummy;
        r.button = MakeButton(listRoot, slotSprite, out dummy, "", 1f);
        Destroy(dummy.gameObject);
        RectTransform rt = (RectTransform)r.button.transform;
        CombatSkin.Place(rt, new Vector2(0f, 1f), new Vector2(0f, -index * 80f), new Vector2(470f, 72f), new Vector2(0f, 1f));
        r.bg = r.button.GetComponent<Image>();
        r.group = r.button.gameObject.AddComponent<CanvasGroup>();
        r.sel = MakeImage(rt, slotSelSprite, new Color(1f, 1f, 1f, 0.55f));
        Stretch(r.sel.rectTransform);
        r.icon = MakeImage(rt, IconFor(rec), Color.white);
        r.icon.preserveAspect = true;
        CombatSkin.Place(r.icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(50f, 50f), new Vector2(0f, 0.5f));
        r.name = CombatSkin.Text(rt, rec.name, 19f, CombatSkin.Cream, TextAlignmentOptions.TopLeft);
        CombatSkin.Place(r.name.rectTransform, new Vector2(0f, 1f), new Vector2(80f, -12f), new Vector2(370f, 26f), new Vector2(0f, 1f));
        r.status = CombatSkin.Text(rt, "", 14f, new Color(0.75f, 0.68f, 0.55f), TextAlignmentOptions.TopLeft);
        r.status.outlineWidth = 0f;
        CombatSkin.Place(r.status.rectTransform, new Vector2(0f, 1f), new Vector2(81f, -40f), new Vector2(370f, 22f), new Vector2(0f, 1f));
        r.button.onClick.AddListener(() => { if (crafting == null) { selected = rec; RefreshAll(); Click(); } });
        return r;
    }

    void RefreshAll()
    {
        CraftingState cs = CraftingState.Instance;
        Inventory inv = Inventory.Instance;
        if (cs == null || inv == null) return;

        foreach (Row r in rows)
        {
            string block = cs.Blocker(r.recipe);
            bool owned = block == "Already owned";
            r.sel.enabled = r.recipe == selected;
            r.group.alpha = block == "" || r.recipe == selected ? 1f : (owned ? 0.75f : 0.55f);
            r.status.text = Status(r.recipe, block);
            r.status.color = block == "" ? new Color(0.62f, 0.92f, 0.5f) : (owned ? CombatSkin.Gold : new Color(0.8f, 0.6f, 0.5f));
        }

        if (selected == null) return;
        Recipe s = selected;
        detailIcon.sprite = IconFor(s);
        detailName.text = s.name;
        detailTag.text = s.isToolUpgrade
            ? "TOOL UPGRADE  ·  TIER " + (s.tier == 1 ? "II" : "III")
            : (s.category == CraftCategory.Survival ? "SURVIVAL" : "BUILD") + "  ·  OWNED " + cs.Count(s.item);
        detailDesc.text = s.description;
        detailEffect.text = "▲  " + s.effect;

        // ingredient rows
        while (ingRows.Count < s.cost.Length) ingRows.Add(MakeIngredientRow(ingRows.Count));
        for (int i = 0; i < ingRows.Count; i++)
        {
            IngredientRow ir = ingRows[i];
            bool on = i < s.cost.Length;
            ir.root.gameObject.SetActive(on);
            if (!on) continue;
            Ingredient g = s.cost[i];
            int have = inv.Get(g.item);
            ir.icon.sprite = ItemIcon(g.item);
            ir.name.text = g.item.ToString();
            bool ok = have >= g.amount;
            ir.count.text = "<color=" + (ok ? "#9EEB80" : "#FF7A66") + ">" + have + "</color><color=#A08C64>  /  " + g.amount + "</color>";
        }

        string b = cs.Blocker(s);
        bool can = b == "" && crafting == null;
        craftButton.interactable = can;
        craftBg.color = can || crafting != null ? Color.white : new Color(0.45f, 0.42f, 0.38f, 0.85f);   // dim when you can't craft
        craftLabel.color = can || crafting != null ? CombatSkin.Gold : new Color(0.6f, 0.55f, 0.45f);
        craftLabel.text = crafting != null ? "CRAFTING..." : (b == "Already owned" ? "OWNED" : "CRAFT");
        blockerText.text = crafting != null ? "" : (b == "Already owned" ? "" : b);
    }

    static string Status(Recipe r, string block)
    {
        CraftingState cs = CraftingState.Instance;
        if (r.isToolUpgrade)
        {
            if (block == "Already owned") return "OWNED";
            if (block.StartsWith("Needs")) return "LOCKED  ·  " + block;
            return "Tier " + (r.tier == 1 ? "II" : "III") + (block == "" ? "  ·  ready to craft" : "  ·  missing materials");
        }
        return "Owned " + cs.Count(r.item) + (block == "" ? "  ·  ready to craft" : "  ·  missing materials");
    }

    IngredientRow MakeIngredientRow(int i)
    {
        IngredientRow r = new IngredientRow();
        GameObject go = new GameObject("Ingredient", typeof(RectTransform));
        r.root = (RectTransform)go.transform;
        r.root.SetParent(ingredientsRoot, false);
        CombatSkin.Place(r.root, new Vector2(0f, 1f), new Vector2(0f, -i * 46f), new Vector2(530f, 42f), new Vector2(0f, 1f));
        Image bg = MakeImage(r.root, slotSprite, new Color(1f, 1f, 1f, 0.5f));
        Stretch(bg.rectTransform);
        r.icon = MakeImage(r.root, null, Color.white);
        r.icon.preserveAspect = true;
        CombatSkin.Place(r.icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(30f, 30f), new Vector2(0f, 0.5f));
        r.name = CombatSkin.Text(r.root, "", 17f, CombatSkin.Cream, TextAlignmentOptions.Left);
        CombatSkin.Place(r.name.rectTransform, new Vector2(0f, 0.5f), new Vector2(52f, 0f), new Vector2(250f, 30f), new Vector2(0f, 0.5f));
        r.count = CombatSkin.Text(r.root, "", 18f, Color.white, TextAlignmentOptions.Right);
        CombatSkin.Place(r.count.rectTransform, new Vector2(1f, 0.5f), new Vector2(-16f, 0f), new Vector2(200f, 30f), new Vector2(1f, 0.5f));
        return r;
    }

    // ------------------------------------------------------------------ crafting

    void StartCraft()
    {
        if (crafting != null || selected == null || CraftingState.Instance == null) return;
        if (CraftingState.Instance.Blocker(selected) != "") return;
        crafting = selected;
        craftT = 0f;
        nextClang = 0f;
        ToolController tc = ToolController.Instance;
        if (tc != null && tc.animator != null) tc.animator.SetTrigger("Craft");
        RefreshAll();
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;

        // open / close animation
        openT = Mathf.MoveTowards(openT, IsOpen ? 1f : 0f, dt / 0.16f);
        float k = openT * openT * (3f - 2f * openT);
        group.alpha = k;
        group.blocksRaycasts = IsOpen;
        group.interactable = IsOpen;
        window.localScale = Vector3.one * Mathf.Lerp(0.94f, 1f, k);

        if (IsOpen)
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) DoClose();
        }

        // crafting progress
        if (crafting != null)
        {
            craftT += dt;
            craftFill.fillAmount = Mathf.Clamp01(craftT / crafting.craftTime);
            if (craftT >= nextClang) { audioSource.pitch = Random.Range(0.92f, 1.08f); audioSource.PlayOneShot(clang, 0.55f); nextClang += 0.38f; }
            if (craftT >= crafting.craftTime)
            {
                Recipe r = crafting;
                crafting = null;
                craftFill.fillAmount = 0f;
                if (CraftingState.Instance.Craft(r))
                {
                    audioSource.pitch = 1f;
                    audioSource.PlayOneShot(done, 0.8f);
                    ShowToast("CRAFTED  ·  " + r.name.ToUpperInvariant());
                    CameraShake cs = CameraShake.Instance;
                    if (cs != null) cs.Shake(0.03f, 0.15f);
                }
                RefreshAll();
            }
        }

        // toast
        if (toastT >= 0f)
        {
            toastT += dt;
            float a = toastT < 0.2f ? toastT / 0.2f : (toastT < 1.6f ? 1f : 1f - (toastT - 1.6f) / 0.6f);
            toast.alpha = Mathf.Clamp01(a);
            toast.rectTransform.anchoredPosition = new Vector2(0f, 395f + 10f * Mathf.Clamp01(toastT / 0.3f));
            if (toastT > 2.2f) { toastT = -1f; toast.alpha = 0f; }
        }

        UpdateQuick(salveSlot, CraftItem.HerbSalve, dt);
        UpdateQuick(potionSlot, CraftItem.HealthPotion, dt);

        // inventory can change while open (pickups fly in)
        if (IsOpen && Time.frameCount % 15 == 0) RefreshAll();
    }

    void UpdateQuick(QuickSlot q, CraftItem item, float dt)
    {
        int n = CraftingState.Instance != null ? CraftingState.Instance.Count(item) : 0;
        q.alpha = Mathf.MoveTowards(q.alpha, n > 0 ? 1f : 0f, dt * 4f);
        q.group.alpha = q.alpha;
        q.count.text = n > 0 ? "x" + n : "";
    }

    void ShowToast(string text)
    {
        toast.text = text;
        toastT = 0f;
    }

    void Click()
    {
        if (audioSource != null && clang != null) { audioSource.pitch = 1.6f; audioSource.PlayOneShot(clang, 0.12f); }
    }

    /// <summary>Hammer on an anvil: a short metallic ring (done = brighter, longer, two notes).</summary>
    static AudioClip MakeClang(bool done)
    {
        const int rate = 44100;
        float seconds = done ? 0.9f : 0.35f;
        int n = (int)(rate * seconds);
        float[] d = new float[n];
        System.Random rnd = new System.Random(done ? 3 : 9);
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float noise = (float)(rnd.NextDouble() * 2.0 - 1.0);
            float ring = Mathf.Sin(6.2832f * 1180f * t) * 0.5f + Mathf.Sin(6.2832f * 2730f * t) * 0.25f + Mathf.Sin(6.2832f * 4310f * t) * 0.12f;
            float v = ring * Mathf.Exp(-t * (done ? 4f : 14f)) + noise * Mathf.Exp(-t * 120f) * 0.6f;
            if (done && t > 0.12f) { float tt = t - 0.12f; v += (Mathf.Sin(6.2832f * 1568f * tt) * 0.4f + Mathf.Sin(6.2832f * 3136f * tt) * 0.15f) * Mathf.Exp(-tt * 3.5f); }
            d[i] = Mathf.Clamp(v * 0.7f, -1f, 1f);
        }
        AudioClip c = AudioClip.Create(done ? "craftDone" : "clang", n, 1, rate, false);
        c.SetData(d, 0);
        return c;
    }
}
