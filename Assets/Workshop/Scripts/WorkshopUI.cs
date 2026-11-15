using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// MAZURA Workshop (crafting) window.
/// Opened by a Workbench ([E]). Uses the existing systems only:
///   recipes / tool tiers / crafted items  -> CraftingState
///   materials                             -> Inventory
/// The window is built at runtime on its own Canvas ("WorkshopCanvas"), so no scene objects are needed.
/// Sprites and font live in Assets/Workshop/Resources/WorkshopUI.
/// Layout is authored for 1920x1080 and scales to any PC resolution (CanvasScaler: Expand).
/// </summary>
public class WorkshopUI : MonoBehaviour
{
    public static WorkshopUI Instance { get; private set; }
    public static bool IsOpen { get; private set; }

    enum Cat { Tools, Healing, Light, TrapsCamp }
    static readonly string[] CatLabels = { "Tools", "Healing", "Weapons", "Traps &\nCamp" };
    static readonly string[] CatIcons = { "cat_tools", "cat_healing", "icon_sword", "cat_traps" };

    static readonly Color Parchment = new Color(0.95f, 0.87f, 0.70f);
    static readonly Color Gold = new Color(1f, 0.80f, 0.42f);
    static readonly Color Dim = new Color(0.55f, 0.50f, 0.44f);
    static readonly Color Good = new Color(0.45f, 0.95f, 0.40f);
    static readonly Color Bad = new Color(1f, 0.35f, 0.30f);

    // ---------------------------------------------------------------- refs
    Canvas canvas;
    CanvasGroup group;
    RectTransform window;
    float openT;
    TMP_FontAsset font;
    TMP_FontAsset numFont;   // default TMP font: clear lining digits for counts

    class Tab { public Image frame, icon; public TextMeshProUGUI label; }
    readonly Tab[] tabs = new Tab[4];
    Cat cat = Cat.Light;
    bool handMode;            // opened with [C] away from a Workbench: only hand recipes can be made
    TextMeshProUGUI modeText;
    float nextCraftAnim;

    class Card
    {
        public RectTransform rt;
        public Image frame, icon, lockIcon;
        public TextMeshProUGUI label, badge;
        public Button button;
        public Recipe recipe;
    }
    readonly List<Card> cards = new List<Card>();
    Recipe selected;

    // detail panel
    Image dIcon;
    TextMeshProUGUI dName, dDesc, dEffect, dStatus, dCraftLabel;
    class MatRow { public RectTransform rt; public Image icon; public TextMeshProUGUI name, count; }
    readonly List<MatRow> matRows = new List<MatRow>();
    Image craftImage, craftFill;
    float statusT = -1f;

    // bottom bar
    class Res { public ItemType type; public Image icon; public TextMeshProUGUI count; }
    readonly List<Res> resources = new List<Res>();
    class Slot { public Image frame, icon; public TextMeshProUGUI count, sub; }
    readonly Slot[] slots = new Slot[Hotbar.Size];
    int dragFrom = -1;
    Image dragIcon;

    // crafting progress
    Recipe crafting;
    float craftT;
    float nextClang;
    AudioSource sfx;
    AudioClip clang, done;

    // look input (same approach as CraftingUI / InventoryUI)
    MonoBehaviour lookInputs;
    FieldInfo lookEnabledField, lookVectorField;
    Inventory hookedInventory;

    static readonly Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>();

    // ---------------------------------------------------------------- boot / API

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (Instance != null) return;
        new GameObject("WorkshopUI").AddComponent<WorkshopUI>();
    }

    /// <summary>Opens the full Workshop (standing at a Workbench).</summary>
    public static void Open() { if (Instance != null) Instance.DoOpen(false); }
    /// <summary>Opens hand crafting [C]: only Workbench, Torch and Campfire can be made.</summary>
    public static void OpenHand() { if (Instance != null) Instance.DoOpen(true); }
    public static void Close() { if (Instance != null) Instance.DoClose(); }

    void Awake()
    {
        Instance = this;
        LoadFont();
        Build();
        sfx = gameObject.AddComponent<AudioSource>();
        sfx.playOnAwake = false;
        sfx.spatialBlend = 0f;
        clang = MakeClang(false);
        done = MakeClang(true);
        CraftingState.OnChanged += Refresh;
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;
        canvas.enabled = false;
    }

    void OnDestroy()
    {
        CraftingState.OnChanged -= Refresh;
        if (hookedInventory != null) hookedInventory.OnChanged -= Refresh;
        if (Instance == this) { Instance = null; IsOpen = false; }
    }

    // ---------------------------------------------------------------- assets

    static Sprite Spr(string name)
    {
        Sprite s;
        if (spriteCache.TryGetValue(name, out s)) return s;
        s = Resources.Load<Sprite>("WorkshopUI/" + name);
        spriteCache[name] = s;
        return s;
    }

    static Sprite GameIcon(string name)
    {
        Sprite s;
        string key = "game:" + name;
        if (spriteCache.TryGetValue(key, out s)) return s;
        s = Resources.Load<Sprite>("Crafting/" + name);
        spriteCache[key] = s;
        return s;
    }

    static Sprite ItemSprite(ItemType t)
    {
        switch (t)
        {
            case ItemType.Wood: return Spr("res_wood");
            case ItemType.Stone: return Spr("res_stone");
            case ItemType.Herb: return Spr("res_herb");
            default: return GameIcon("Item_" + t);
        }
    }

    static string ItemName(ItemType t) { return t == ItemType.Herb ? "Herb" : t.ToString(); }

    static Sprite RecipeSprite(Recipe r, bool large)
    {
        if (r.isToolUpgrade)
            return Spr(r.tool == ToolType.Axe ? "icon_axe" : r.tool == ToolType.Pickaxe ? "icon_pickaxe" : "icon_sword");
        switch (r.item)
        {
            case CraftItem.HerbSalve: return Spr("icon_herbsalve");
            case CraftItem.HealthPotion: return GameIcon("Icon_HealthPotion") ?? GameIcon("Item_Ruby");
            case CraftItem.Torch: return Spr(large ? "icon_torch_large" : "icon_torch");
            case CraftItem.Campfire: return Spr("icon_campfire");
            case CraftItem.Shield: return Spr("icon_shield");
            case CraftItem.Sword: return Spr("icon_sword");
            case CraftItem.Workbench: return Resources.Load<Sprite>("RecipeBookUI/Icon_Workbench");
            case CraftItem.Spear: return Spr("icon_spear");
            case CraftItem.BattleAxe: return Spr("icon_doubleaxe");
            case CraftItem.WarHammer: return Spr("icon_warhammer");
            default: return Spr("icon_spiketrap");
        }
    }

    /// <summary>Short card title (the full recipe name is shown in the details panel).</summary>
    static string CardTitle(Recipe r)
    {
        if (!r.isToolUpgrade) return r.name;
        string t = r.tool == ToolType.Axe ? "Axe" : r.tool == ToolType.Pickaxe ? "Pickaxe" : "Sword";
        return t + " Upgrade" + (r.tier >= 2 ? " II" : "");
    }

    /// <summary>UI-only flavour text overrides (gameplay data is untouched).</summary>
    static string Description(Recipe r)
    {
        if (!r.isToolUpgrade && r.item == CraftItem.Torch) return "Lights the way and scares Shadow Wraiths.";
        return r.description;
    }

    static Cat CategoryOf(Recipe r)
    {
        if (r.isToolUpgrade) return Cat.Tools;
        switch (r.item)
        {
            case CraftItem.Shield:
            case CraftItem.Sword:
            case CraftItem.Spear:
            case CraftItem.BattleAxe:
            case CraftItem.WarHammer: return Cat.Light;
            case CraftItem.HerbSalve:
            case CraftItem.HealthPotion: return Cat.Healing;
            case CraftItem.Torch: return Cat.TrapsCamp;
            default: return Cat.TrapsCamp;
        }
    }

    void LoadFont()
    {
        Font f = Resources.Load<Font>("WorkshopUI/Alegreya-SemiBold");
        if (f != null)
        {
            font = TMP_FontAsset.CreateFontAsset(f);
            if (font != null && TMP_Settings.defaultFontAsset != null)
            {
                if (font.fallbackFontAssetTable == null) font.fallbackFontAssetTable = new List<TMP_FontAsset>();
                font.fallbackFontAssetTable.Add(TMP_Settings.defaultFontAsset);
            }
        }
        if (font == null) font = TMP_Settings.defaultFontAsset;
        numFont = TMP_Settings.defaultFontAsset != null ? TMP_Settings.defaultFontAsset : font;
    }

    // ---------------------------------------------------------------- UI helpers

    static RectTransform NewRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    /// <summary>Places a rect using top-left coordinates inside its parent (authoring space).</summary>
    static void Place(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static Image Img(Transform parent, string name, Sprite s, float x, float y, float w, float h, bool aspect = false, bool raycast = false)
    {
        RectTransform rt = NewRect(name, parent);
        Place(rt, x, y, w, h);
        Image im = rt.gameObject.AddComponent<Image>();
        im.sprite = s;
        im.preserveAspect = aspect;
        im.raycastTarget = raycast;
        return im;
    }

    TextMeshProUGUI Num(Transform parent, string name, string text, float x, float y, float w, float h, float size, Color c, TextAlignmentOptions align)
    {
        TextMeshProUGUI t = Txt(parent, name, text, x, y, w, h, size, c, align);
        t.font = numFont;
        t.fontStyle = FontStyles.Bold;
        return t;
    }

    TextMeshProUGUI Txt(Transform parent, string name, string text, float x, float y, float w, float h, float size, Color c, TextAlignmentOptions align)
    {
        RectTransform rt = NewRect(name, parent);
        Place(rt, x, y, w, h);
        TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = c;
        t.alignment = align;
        t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.overflowMode = TextOverflowModes.Overflow;
        return t;
    }

    static ColorBlock Tints()
    {
        ColorBlock cb = ColorBlock.defaultColorBlock;
        cb.normalColor = new Color(0.88f, 0.88f, 0.88f);
        cb.highlightedColor = Color.white;
        cb.pressedColor = new Color(0.72f, 0.72f, 0.72f);
        cb.selectedColor = new Color(0.88f, 0.88f, 0.88f);
        cb.disabledColor = new Color(0.6f, 0.6f, 0.6f);
        cb.fadeDuration = 0.08f;
        return cb;
    }

    // ---------------------------------------------------------------- build

    void Build()
    {
        GameObject cgo = new GameObject("WorkshopCanvas", typeof(RectTransform));
        cgo.transform.SetParent(transform, false);
        canvas = cgo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 120;
        CanvasScaler cs = cgo.AddComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920f, 1080f);
        cs.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;   // whole 1920x1080 window always fits
        cgo.AddComponent<GraphicRaycaster>();
        group = cgo.AddComponent<CanvasGroup>();

        // dim the game behind the window
        RectTransform dimRt = NewRect("Dim", cgo.transform);
        Stretch(dimRt);
        Image dim = dimRt.gameObject.AddComponent<Image>();
        dim.color = new Color(0.02f, 0.015f, 0.01f, 0.72f);
        dim.raycastTarget = true;

        window = NewRect("Window", cgo.transform);
        window.anchorMin = window.anchorMax = window.pivot = new Vector2(0.5f, 0.5f);
        window.sizeDelta = new Vector2(1920f, 1080f);
        window.anchoredPosition = Vector2.zero;

        // ---- top
        Img(window, "TitleBanner", Spr("title_banner"), 626f, 58f, 680f, 160f, true);
        modeText = Txt(window, "ModeText", "", 420f, 18f, 1080f, 30f, 22f, Gold, TextAlignmentOptions.Center);
        Image close = Img(window, "CloseButton", Spr("close"), 1748f, 168f, 96f, 96f, true, true);
        Button cb = close.gameObject.AddComponent<Button>();
        cb.colors = Tints();
        cb.onClick.AddListener(DoClose);

        // ---- left tabs
        RectTransform tabsRoot = NewRect("CategoryTabs", window);
        Place(tabsRoot, 0f, 0f, 1920f, 1080f);
        for (int i = 0; i < 4; i++)
        {
            int idx = i;
            Tab t = new Tab();
            t.frame = Img(tabsRoot, "Tab_" + (Cat)i, Spr("tab_normal"), 176f, 238f + i * 128f, 240f, 116f, false, true);
            t.icon = Img(t.frame.transform, "Icon", Spr(CatIcons[i]), 16f, 18f, 80f, 80f, true);
            t.label = Txt(t.frame.transform, "Label", CatLabels[i], 98f, 8f, 132f, 100f, 31f, Parchment, TextAlignmentOptions.MidlineLeft);
            t.label.lineSpacing = -18f;
            Button b = t.frame.gameObject.AddComponent<Button>();
            b.colors = Tints();
            b.onClick.AddListener(() => { SetCategory((Cat)idx); Click(); });
            tabs[i] = t;
        }

        // ---- center grid
        Image grid = Img(window, "RecipeGridPanel", Spr("grid_panel"), 420f, 196f, 832f, 680f);
        const float cw = 226f, ch = 192f, gx = 28f, gy = 14f;
        float gx0 = (832f - (cw * 3f + gx * 2f)) * 0.5f;
        float gy0 = (680f - (ch * 3f + gy * 2f)) * 0.5f;
        for (int i = 0; i < 9; i++)
        {
            int col = i % 3, row = i / 3;
            Card c = new Card();
            c.frame = Img(grid.transform, "RecipeCard_" + i, Spr("card_normal"), gx0 + col * (cw + gx), gy0 + row * (ch + gy), cw, ch, false, true);
            c.rt = c.frame.rectTransform;
            c.icon = Img(c.frame.transform, "Icon", null, 30f, 14f, cw - 60f, 122f, true);
            c.lockIcon = Img(c.frame.transform, "Lock", Spr("icon_lock"), cw * 0.5f - 34f, 40f, 68f, 76f, true);
            c.label = Txt(c.frame.transform, "Name", "", 10f, 136f, cw - 20f, 44f, 25f, Parchment, TextAlignmentOptions.Center);
            c.badge = Num(c.frame.transform, "Badge", "", cw - 92f, 10f, 80f, 28f, 19f, Gold, TextAlignmentOptions.TopRight);
            c.button = c.frame.gameObject.AddComponent<Button>();
            c.button.colors = Tints();
            Card cc = c;
            c.button.onClick.AddListener(() => { if (cc.recipe != null) { Select(cc.recipe); Click(); } });
            cards.Add(c);
        }

        // ---- right details
        Image det = Img(window, "DetailsPanel", Spr("detail_panel"), 1250f, 204f, 487f, 689f);
        Transform d = det.transform;
        dName = Txt(d, "ItemName", "", 40f, 22f, 407f, 56f, 40f, Gold, TextAlignmentOptions.Center);
        dIcon = Img(d, "ItemIcon", null, 133f, 78f, 220f, 222f, true);
        dDesc = Txt(d, "Description", "", 40f, 300f, 407f, 66f, 24f, Parchment, TextAlignmentOptions.Top);
        dEffect = Txt(d, "Effect", "", 40f, 366f, 407f, 34f, 20f, Gold, TextAlignmentOptions.Top);
        for (int i = 0; i < 3; i++)
        {
            MatRow m = new MatRow();
            Image bar = Img(d, "Material_" + i, null, 44f, 438f + i * 36f, 400f, 32f);
            bar.color = new Color(0.07f, 0.045f, 0.03f, 0.78f);
            m.rt = bar.rectTransform;
            m.icon = Img(bar.transform, "Icon", null, 6f, -3f, 38f, 38f, true);
            m.name = Txt(bar.transform, "Name", "", 54f, 0f, 220f, 32f, 23f, Parchment, TextAlignmentOptions.MidlineLeft);
            m.count = Num(bar.transform, "Count", "", 250f, 0f, 140f, 32f, 24f, Good, TextAlignmentOptions.MidlineRight);
            matRows.Add(m);
        }
        dStatus = Txt(d, "StatusMessage", "", 30f, 546f, 427f, 30f, 21f, Bad, TextAlignmentOptions.Center);
        craftImage = Img(d, "CraftButton", Spr("craft_button"), 63f, 580f, 360f, 90f, false, true);
        Button craft = craftImage.gameObject.AddComponent<Button>();
        craft.colors = Tints();
        craft.onClick.AddListener(OnCraftClicked);
        craftFill = Img(craftImage.transform, "Progress", Spr("craft_button"), 0f, 0f, 360f, 90f);
        Stretch(craftFill.rectTransform);
        craftFill.color = new Color(1f, 0.95f, 0.7f, 0.55f);
        craftFill.type = Image.Type.Filled;
        craftFill.fillMethod = Image.FillMethod.Horizontal;
        craftFill.fillAmount = 0f;
        dCraftLabel = Txt(craftImage.transform, "CraftingLabel", "", 0f, 0f, 360f, 90f, 30f, new Color(0.25f, 0.12f, 0.03f), TextAlignmentOptions.Center);
        Stretch(dCraftLabel.rectTransform);

        // ---- bottom inventory + hotbar
        Image bar2 = Img(window, "InventoryHotbarBar", Spr("bottom_bar"), 147f, 896f, 1596f, 156f);
        Transform bt = bar2.transform;
        ItemType[] big = { ItemType.Wood, ItemType.Stone, ItemType.Herb };
        string[] bigNames = { "Wood", "Stone", "Herbs" };
        for (int i = 0; i < 3; i++)
        {
            Res r = new Res { type = big[i] };
            float x = 238f + i * 112f;
            r.icon = Img(bt, "Res_" + bigNames[i], ItemSprite(big[i]), x, 18f, 78f, 74f, true);
            r.count = Num(bt, "Count", "0", x + 40f, 56f, 56f, 36f, 30f, Color.white, TextAlignmentOptions.MidlineRight);
            r.count.outlineWidth = 0.22f;
            r.count.outlineColor = new Color32(10, 6, 3, 255);
            Txt(bt, "Label", bigNames[i], x - 14f, 98f, 106f, 34f, 24f, Parchment, TextAlignmentOptions.Center);
            resources.Add(r);
        }
        ItemType[] gems = { ItemType.Amber, ItemType.Ruby, ItemType.Emerald, ItemType.Sapphire };
        for (int i = 0; i < 4; i++)
        {
            Res r = new Res { type = gems[i] };
            float x = 572f + (i % 2) * 76f, y = 26f + (i / 2) * 54f;
            r.icon = Img(bt, "Res_" + gems[i], ItemSprite(gems[i]), x, y, 40f, 40f, true);
            r.count = Num(bt, "Count", "0", x + 40f, y + 4f, 34f, 32f, 22f, Color.white, TextAlignmentOptions.MidlineLeft);
            resources.Add(r);
        }
        for (int i = 0; i < Hotbar.Size; i++)
        {
            int idx = i;
            Slot s = new Slot();
            float x = 840f + i * 146f;
            s.frame = Img(bt, "HotbarSlot_" + (i + 1), Spr("card_normal"), x, 18f, 122f, 122f, false, true);
            s.icon = Img(s.frame.transform, "Icon", null, 14f, 12f, 94f, 94f, true);
            TextMeshProUGUI num = Num(s.frame.transform, "Number", (i + 1).ToString(), 9f, 4f, 28f, 30f, 22f, Parchment, TextAlignmentOptions.Center);
            num.outlineWidth = 0.25f; num.outlineColor = new Color32(10, 6, 3, 255);
            s.count = Num(s.frame.transform, "Count", "", 52f, 82f, 62f, 32f, 23f, Color.white, TextAlignmentOptions.BottomRight);
            s.count.outlineWidth = 0.25f; s.count.outlineColor = new Color32(10, 6, 3, 255);
            s.sub = Txt(s.frame.transform, "Tip", "", 6f, 96f, 110f, 22f, 15f, Dim, TextAlignmentOptions.Center);
            // drag a slot onto another one to swap them
            UnityEngine.EventSystems.EventTrigger et = s.frame.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
            AddTrigger(et, UnityEngine.EventSystems.EventTriggerType.BeginDrag, d => BeginSlotDrag(idx));
            AddTrigger(et, UnityEngine.EventSystems.EventTriggerType.Drag, d => { if (dragIcon != null) dragIcon.rectTransform.position = ((UnityEngine.EventSystems.PointerEventData)d).position; });
            AddTrigger(et, UnityEngine.EventSystems.EventTriggerType.Drop, d => { if (dragFrom >= 0 && Hotbar.Instance != null) { Hotbar.Instance.Swap(dragFrom, idx); Click(); Refresh(); } });
            AddTrigger(et, UnityEngine.EventSystems.EventTriggerType.EndDrag, d => EndSlotDrag());
            slots[i] = s;
        }
        Txt(bt, "HotbarHint", "drag to reorder", 840f, 138f, 706f, 18f, 14f, Dim, TextAlignmentOptions.Center);
        dragIcon = Img(window, "DragIcon", null, 0f, 0f, 90f, 90f, true);
        dragIcon.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        dragIcon.enabled = false;

        EnsureEventSystem();
    }

    static void EnsureEventSystem()
    {
        if (EventSystem.current != null || Object.FindAnyObjectByType<EventSystem>() != null) return;
        GameObject es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
    }

    // ---------------------------------------------------------------- open / close

    void DoOpen(bool hand)
    {
        if (IsOpen) return;
        IsOpen = true;
        canvas.enabled = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SetLook(false);
        HookInventory();
        crafting = null;
        craftFill.fillAmount = 0f;
        dCraftLabel.text = "";
        statusT = -1f; dStatus.text = "";
        // default view: Light / Torch (as in the design)
        handMode = hand;
        modeText.text = hand ? "CRAFTING BY HAND  \u00b7  craft a Workbench and place it to unlock everything" : "";
        if (hand) { cat = Cat.TrapsCamp; selected = FindRecipe("workbench"); }
        else { cat = Cat.Light; selected = FindRecipe("sword0"); }
        Refresh();
        Click();
    }

    void DoClose()
    {
        if (!IsOpen) return;
        IsOpen = false;
        crafting = null;
        craftFill.fillAmount = 0f;
        dCraftLabel.text = "";
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        SetLook(true);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }

    void HookInventory()
    {
        Inventory inv = Inventory.Instance;
        if (inv == hookedInventory) return;
        if (hookedInventory != null) hookedInventory.OnChanged -= Refresh;
        hookedInventory = inv;
        if (inv != null) inv.OnChanged += Refresh;
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
        FieldInfo move = lookInputs.GetType().GetField("move");
        if (move != null) move.SetValue(lookInputs, Vector2.zero);
    }

    // ---------------------------------------------------------------- content

    static Recipe FindRecipe(string id)
    {
        CraftingState cs = CraftingState.Instance;
        if (cs == null || cs.Recipes == null) return null;
        foreach (Recipe r in cs.Recipes) if (r.id == id) return r;
        return null;
    }

    List<Recipe> RecipesFor(Cat c)
    {
        List<Recipe> list = new List<Recipe>();
        CraftingState cs = CraftingState.Instance;
        if (cs == null || cs.Recipes == null) return list;
        if (c == Cat.Tools)
        {
            // row 1 = tier 1 upgrades, row 2 = tier 2 upgrades (Axe, Pickaxe, Sword)
            string[] order = { "axe1", "pick1", "sword1", "axe2", "pick2", "sword2" };
            foreach (string id in order) { Recipe r = FindRecipe(id); if (r != null) list.Add(r); }
            return list;
        }
        foreach (Recipe r in cs.Recipes) if (CategoryOf(r) == c) list.Add(r);
        return list;
    }

    void SetCategory(Cat c)
    {
        cat = c;
        List<Recipe> list = RecipesFor(c);
        selected = list.Count > 0 ? list[0] : null;
        Refresh();
    }

    void Select(Recipe r)
    {
        selected = r;
        Refresh();
    }

    /// <summary>"Locked" = a prerequisite is missing (Gemforged needs Reinforced first).</summary>
    bool IsLocked(Recipe r, out string why)
    {
        why = "";
        if (handMode && !r.byHand) { why = "Needs a Workbench - craft one here and place it"; return true; }
        if (!r.isToolUpgrade || CraftingState.Instance == null) return false;
        if (!CraftingState.Instance.HasTool(r.tool)) { why = "You need a " + (r.tool == ToolType.Weapon ? "Sword" : r.tool.ToString()) + " first"; return true; }
        int cur = CraftingState.Instance.TierOf(r.tool);
        if (cur < r.tier - 1) { why = "Locked - craft the " + CraftingState.TierNames[r.tier - 1] + " upgrade first"; return true; }
        return false;
    }

    static bool IsOwned(Recipe r)
    {
        if (CraftingState.Instance == null) return false;
        if (!r.isToolUpgrade)
            return (r.item == CraftItem.Shield && CraftingState.Instance.Count(CraftItem.Shield) > 0)
                || (Weapons.KindOf(r.item).HasValue && CraftingState.Instance.Count(r.item) > 0);
        return CraftingState.Instance.TierOf(r.tool) >= r.tier;
    }

    void Refresh()
    {
        if (window == null) return;
        Inventory inv = Inventory.Instance;

        // tabs
        for (int i = 0; i < 4; i++)
        {
            bool on = (int)cat == i;
            tabs[i].frame.sprite = Spr(on ? "tab_selected" : "tab_normal");
            tabs[i].label.color = on ? new Color(1f, 0.93f, 0.78f) : Parchment * 0.92f;
            tabs[i].icon.color = on ? Color.white : new Color(0.85f, 0.85f, 0.85f);
        }

        // grid
        List<Recipe> list = RecipesFor(cat);
        for (int i = 0; i < cards.Count; i++)
        {
            Card c = cards[i];
            Recipe r = i < list.Count ? list[i] : null;
            c.recipe = r;
            c.button.interactable = r != null;
            if (r == null)
            {
                c.frame.sprite = Spr("card_normal");
                c.frame.color = new Color(1f, 1f, 1f, 0.35f);
                c.icon.enabled = false; c.lockIcon.enabled = false;
                c.label.text = ""; c.badge.text = "";
                continue;
            }
            string why;
            bool locked = IsLocked(r, out why);
            bool owned = IsOwned(r);
            bool sel = r == selected;
            c.frame.sprite = Spr(sel ? "card_selected" : "card_normal");
            c.frame.color = Color.white;
            c.icon.enabled = true;
            c.icon.sprite = RecipeSprite(r, false);
            c.icon.color = locked ? new Color(0.32f, 0.30f, 0.28f, 0.9f) : (r.isToolUpgrade && r.tier >= 2 ? new Color(1f, 0.88f, 0.6f) : Color.white);
            c.lockIcon.enabled = locked;
            c.label.text = CardTitle(r);
            c.label.color = locked ? Dim : (sel ? new Color(1f, 0.95f, 0.82f) : Parchment);
            if (owned) { c.badge.text = "OWNED"; c.badge.color = Good; }
            else if (!locked && CraftingState.Instance != null && CraftingState.Instance.Blocker(r) == "") { c.badge.text = "READY"; c.badge.color = Gold; }
            else c.badge.text = "";
        }

        // details
        if (selected == null)
        {
            dName.text = ""; dDesc.text = ""; dEffect.text = ""; dIcon.enabled = false;
            foreach (MatRow m in matRows) m.rt.gameObject.SetActive(false);
        }
        else
        {
            Recipe r = selected;
            dName.text = r.name;
            dIcon.enabled = true;
            dIcon.sprite = RecipeSprite(r, true);
            string why;
            bool locked = IsLocked(r, out why);
            dIcon.color = locked ? new Color(0.4f, 0.38f, 0.35f) : Color.white;
            dDesc.text = Description(r);
            dEffect.text = r.effect.Replace("   ", "  ·  ");
            for (int i = 0; i < matRows.Count; i++)
            {
                MatRow m = matRows[i];
                bool show = i < r.cost.Length;
                m.rt.gameObject.SetActive(show);
                if (!show) continue;
                Ingredient g = r.cost[i];
                int have = inv != null ? inv.Get(g.item) : 0;
                m.icon.sprite = ItemSprite(g.item);
                m.name.text = ItemName(g.item);
                m.count.text = Mathf.Min(have, 999) + " / " + g.amount;
                m.count.color = have >= g.amount ? Good : Bad;
            }
            // with 3 ingredients the rows sit a little tighter
            float step = r.cost.Length >= 3 ? 34f : 40f;
            for (int i = 0; i < matRows.Count; i++) matRows[i].rt.anchoredPosition = new Vector2(44f, -(440f + i * step));

            bool canCraft = !locked && !IsOwned(r) && CraftingState.Instance != null && CraftingState.Instance.Blocker(r) == "";
            craftImage.color = crafting != null ? new Color(0.9f, 0.9f, 0.9f) : (canCraft ? Color.white : new Color(0.80f, 0.76f, 0.72f));
        }

        // bottom: resources
        foreach (Res res in resources)
        {
            int n = inv != null ? inv.Get(res.type) : 0;
            res.count.text = n.ToString();
            res.icon.color = n > 0 ? Color.white : new Color(1f, 1f, 1f, 0.45f);
        }

        // bottom: the real 5-slot hotbar (keys 1-5)
        Hotbar hb = Hotbar.Instance;
        int selIndex = hb != null ? hb.Selected : -1;
        for (int i = 0; i < Hotbar.Size; i++)
        {
            HotbarEntry e = hb != null ? hb[i] : new HotbarEntry();
            bool has = e.kind != HotbarKind.Empty;
            Slot sl = slots[i];
            sl.frame.sprite = Spr(i == selIndex ? "card_selected" : "card_normal");
            sl.icon.enabled = has;
            sl.icon.sprite = e.Icon;
            sl.icon.color = i == dragFrom ? new Color(1f, 1f, 1f, 0.3f) : Color.white;
            sl.count.text = has && hb != null ? hb.CountText(i) : "";
            sl.count.color = e.kind == HotbarKind.Tool ? Gold : Color.white;
            sl.sub.text = e.Name;
        }
    }

    static string Tier(ToolType t)
    {
        CraftingState st = CraftingState.Instance;
        int tier = st != null ? st.TierOf(t) : 0;
        return tier == 0 ? "" : (tier == 1 ? "I" : "II");
    }

    void SetSlot(int i, Sprite s, string count, bool equipped, bool have)
    {
        Slot sl = slots[i];
        sl.frame.sprite = Spr(equipped ? "card_selected" : "card_normal");
        sl.icon.sprite = s;
        sl.icon.color = have ? Color.white : new Color(1f, 1f, 1f, 0.22f);
        sl.count.text = count;
        sl.count.color = i < 3 ? Gold : Color.white;
    }

    // ---------------------------------------------------------------- crafting

    static void AddTrigger(UnityEngine.EventSystems.EventTrigger et, UnityEngine.EventSystems.EventTriggerType type, System.Action<UnityEngine.EventSystems.BaseEventData> a)
    {
        UnityEngine.EventSystems.EventTrigger.Entry en = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = type };
        en.callback.AddListener(d => a(d));
        et.triggers.Add(en);
    }

    void BeginSlotDrag(int i)
    {
        Hotbar hb = Hotbar.Instance;
        if (hb == null || hb[i].kind == HotbarKind.Empty) { dragFrom = -1; return; }
        dragFrom = i;
        dragIcon.sprite = hb[i].Icon;
        dragIcon.enabled = true;
        dragIcon.transform.SetAsLastSibling();
        Refresh();
    }

    void EndSlotDrag()
    {
        dragFrom = -1;
        if (dragIcon != null) dragIcon.enabled = false;
        Refresh();
    }

    void OnCraftClicked()
    {
        if (crafting != null || selected == null || CraftingState.Instance == null) return;
        Recipe r = selected;
        string why;
        if (IsLocked(r, out why)) { Status(why, Bad); Deny(); return; }
        if (IsOwned(r)) { Status("You already own this", Gold); Deny(); return; }
        string blocker = CraftingState.Instance.Blocker(r);
        if (blocker != "")
        {
            Status(blocker == "Not enough materials" ? "Not enough materials - need " + Missing(r) : blocker, Bad);
            Deny();
            return;
        }
        crafting = r;
        craftT = 0f;
        nextClang = 0f;
        dCraftLabel.text = "";   // the glowing fill shows progress; the button already reads CRAFT
        Status("", Parchment);
        ToolController tc = ToolController.Instance;
        if (tc != null && tc.animator != null) tc.animator.SetTrigger("Craft");
        Refresh();
    }

    static string Missing(Recipe r)
    {
        Inventory inv = Inventory.Instance;
        List<string> parts = new List<string>();
        foreach (Ingredient g in r.cost)
        {
            int have = inv != null ? inv.Get(g.item) : 0;
            if (have < g.amount) parts.Add((g.amount - have) + " " + ItemName(g.item));
        }
        return string.Join(", ", parts.ToArray());
    }

    void Status(string text, Color c)
    {
        dStatus.text = text;
        dStatus.color = c;
        statusT = text == "" ? -1f : 0f;
        dStatus.alpha = 1f;
    }

    void Deny()
    {
        if (sfx != null && clang != null) { sfx.pitch = 0.55f; sfx.PlayOneShot(clang, 0.35f); }
        shakeT = 0.3f;
    }

    float shakeT;

    void Update()
    {
        float dt = Time.unscaledDeltaTime;

        openT = Mathf.MoveTowards(openT, IsOpen ? 1f : 0f, dt / 0.18f);
        float k = openT * openT * (3f - 2f * openT);
        group.alpha = k;
        group.blocksRaycasts = IsOpen;
        group.interactable = IsOpen;
        window.localScale = Vector3.one * Mathf.Lerp(0.95f, 1f, k);
        if (!IsOpen && openT <= 0f && canvas.enabled) canvas.enabled = false;
        Keyboard kbc = Keyboard.current;
        if (!IsOpen)
        {
            if (kbc != null && kbc.cKey.wasPressedThisFrame && Cursor.lockState == CursorLockMode.Locked
                && !ChestLootUI.IsOpen && !RecipeBookUI.IsOpen && !PlacementController.IsPlacing)
            {
                ToolController tcc = ToolController.Instance;
                HealthManager hmc = tcc != null ? tcc.GetComponent<HealthManager>() : null;
                if (hmc == null || !hmc.IsDead) DoOpen(Workbench.Current == null);
            }
            return;
        }
        if (kbc != null && kbc.cKey.wasPressedThisFrame && handMode) { DoClose(); return; }

        Keyboard kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame) { DoClose(); return; }

        // keep cursor free while open (other scripts may relock it)
        if (Cursor.lockState != CursorLockMode.None) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }

        HookInventory();

        // craft button denied wiggle
        if (shakeT > 0f)
        {
            shakeT -= dt;
            craftImage.rectTransform.anchoredPosition = new Vector2(63f + Mathf.Sin(shakeT * 70f) * 7f * (shakeT / 0.3f), -580f);
        }
        else craftImage.rectTransform.anchoredPosition = new Vector2(63f, -580f);

        // crafting progress
        if (crafting != null)
        {
            craftT += dt;
            if (Time.unscaledTime >= nextCraftAnim)
            {
                nextCraftAnim = Time.unscaledTime + 1.1f;
                ToolController tca = ToolController.Instance;
                if (tca != null && tca.animator != null) tca.animator.SetTrigger("Craft");
            }
            craftFill.fillAmount = Mathf.Clamp01(craftT / crafting.craftTime);
            if (craftT >= nextClang) { sfx.pitch = Random.Range(0.92f, 1.08f); sfx.PlayOneShot(clang, 0.5f); nextClang += 0.38f; }
            if (craftT >= crafting.craftTime)
            {
                Recipe r = crafting;
                crafting = null;
                craftFill.fillAmount = 0f;
                dCraftLabel.text = "";
                if (CraftingState.Instance != null && CraftingState.Instance.Craft(r))
                {
                    sfx.pitch = 1f;
                    sfx.PlayOneShot(done, 0.8f);
                    Status("Crafted: " + r.name, Good);
                    if (!r.isToolUpgrade && (r.item == CraftItem.Campfire || r.item == CraftItem.SpikeTrap || r.item == CraftItem.Workbench) && PlacementController.Instance != null)
                    {
                        DoClose();
                        PlacementController.Instance.BeginItem(r.item);
                        HudMessage.Show("Choose a spot for the " + r.name + "  -  [LMB] place   [R] rotate   [RMB] later");
                        return;
                    }
                    CameraShake cs = CameraShake.Instance;
                    if (cs != null) cs.Shake(0.03f, 0.15f);
                }
                else Status("Could not craft - materials changed", Bad);
                Refresh();
            }
        }

        // status fade
        if (statusT >= 0f)
        {
            statusT += dt;
            if (statusT > 2.6f) dStatus.alpha = Mathf.Clamp01(1f - (statusT - 2.6f) / 0.5f);
            if (statusT > 3.1f) { statusT = -1f; dStatus.text = ""; }
        }

        // tool can be switched with keys while open; cheap periodic refresh
        if (Time.frameCount % 20 == 0) Refresh();
    }

    void Click()
    {
        if (sfx != null && clang != null) { sfx.pitch = 1.7f; sfx.PlayOneShot(clang, 0.1f); }
    }

    static AudioClip MakeClang(bool finished)
    {
        const int rate = 44100;
        float seconds = finished ? 0.9f : 0.35f;
        int n = (int)(rate * seconds);
        float[] data = new float[n];
        System.Random rnd = new System.Random(finished ? 5 : 11);
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float noise = (float)(rnd.NextDouble() * 2.0 - 1.0);
            float ring = Mathf.Sin(6.2832f * 1180f * t) * 0.5f + Mathf.Sin(6.2832f * 2730f * t) * 0.25f + Mathf.Sin(6.2832f * 4310f * t) * 0.12f;
            float v = ring * Mathf.Exp(-t * (finished ? 4f : 14f)) + noise * Mathf.Exp(-t * 120f) * 0.6f;
            if (finished && t > 0.12f) { float tt = t - 0.12f; v += (Mathf.Sin(6.2832f * 1568f * tt) * 0.4f + Mathf.Sin(6.2832f * 3136f * tt) * 0.15f) * Mathf.Exp(-tt * 3.5f); }
            data[i] = Mathf.Clamp(v * 0.7f, -1f, 1f);
        }
        AudioClip c = AudioClip.Create(finished ? "workshopDone" : "workshopClang", n, 1, rate, false);
        c.SetData(data, 0);
        return c;
    }
}
