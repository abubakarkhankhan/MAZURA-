using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// MAZURA Recipe Book. Press B to open / close (Esc or X also close).
/// Left page: every recipe grouped by category (locked ones show "???").
/// Right page: the selected recipe with picture, description, materials (what you have / need)
/// and where it is crafted. Read-only: crafting itself happens at the Workshop.
/// Data comes from CraftingState.Recipes, so new recipes appear here automatically.
/// </summary>
public class RecipeBookUI : MonoBehaviour
{
    public static RecipeBookUI Instance { get; private set; }
    public static bool IsOpen { get; private set; }

    static readonly Color Ink = new Color(0.20f, 0.12f, 0.06f);
    static readonly Color InkSoft = new Color(0.33f, 0.22f, 0.12f);
    static readonly Color InkFaded = new Color(0.33f, 0.22f, 0.12f, 0.45f);
    static readonly Color Good = new Color(0.16f, 0.45f, 0.12f);
    static readonly Color Bad = new Color(0.62f, 0.12f, 0.08f);

    const float BookX = 260f;           // book sprite (1400x1080) placed in the 1920x1080 window
    const int LinesPerPage = 14;
    const float LineH = 50f;

    enum Cat { Tools, Healing, Light, Building }
    static readonly string[] CatNames = { "Tools", "Healing", "Light", "Building" };
    static readonly string[] CatIcons = { "Icon_Category_Tools", "Icon_Category_Healing", "Icon_Category_Light_Lantern", "Icon_Category_Building_Hut" };

    class Line { public bool header; public Cat cat; public Recipe recipe; }
    class LineUI { public RectTransform rt; public Image highlight, icon; public TextMeshProUGUI label; public Button button; public Line line; }

    Canvas canvas;
    CanvasGroup group;
    RectTransform window;
    float openT;
    TMP_FontAsset font;

    readonly List<Line> lines = new List<Line>();
    readonly List<LineUI> lineUIs = new List<LineUI>();
    int page, pageCount = 1;
    Recipe selected;

    TextMeshProUGUI dTitle, dDesc, dEffect, dWhere, pageNum;
    Image dPicture;
    class MatUI { public RectTransform rt; public Image icon; public TextMeshProUGUI name, count; }
    readonly List<MatUI> mats = new List<MatUI>();
    Button prevBtn, nextBtn;

    MonoBehaviour lookInputs;
    FieldInfo lookEnabledField, lookVectorField;
    AudioSource sfx;
    AudioClip pageClip;
    int openedFrame;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (Instance != null) return;
        new GameObject("RecipeBookUI").AddComponent<RecipeBookUI>();
    }

    public static void Open() { if (Instance != null) Instance.DoOpen(); }
    public static void Close() { if (Instance != null) Instance.DoClose(); }

    void Awake()
    {
        Instance = this;
        Font f = Resources.Load<Font>("WorkshopUI/Alegreya-SemiBold");
        if (f != null) font = TMP_FontAsset.CreateFontAsset(f);
        if (font == null) font = TMP_Settings.defaultFontAsset;
        Build();
        sfx = gameObject.AddComponent<AudioSource>();
        sfx.playOnAwake = false;
        pageClip = MakePage();
        group.alpha = 0f; group.blocksRaycasts = false; group.interactable = false;
        canvas.enabled = false;
    }

    void OnDestroy() { if (Instance == this) { Instance = null; IsOpen = false; } }

    // ---------------------------------------------------------------- sprites

    static Sprite Book(string n) { return Resources.Load<Sprite>("RecipeBookUI/" + n); }
    static Sprite Ws(string n) { return Resources.Load<Sprite>("WorkshopUI/" + n); }

    static Sprite RecipeIcon(Recipe r)
    {
        if (r.isToolUpgrade)
            return r.tool == ToolType.Weapon ? Book("Icon_Sword") : Ws(r.tool == ToolType.Axe ? "icon_axe" : "icon_pickaxe");
        switch (r.item)
        {
            case CraftItem.HerbSalve: return Book("Icon_Herb_Salve");
            case CraftItem.HealthPotion: return Book("Icon_Health_Potion");
            case CraftItem.Torch: return Book("Icon_Torch");
            case CraftItem.Campfire: return Ws("icon_campfire");
            case CraftItem.Shield: return Ws("icon_shield");
            case CraftItem.Sword: return Book("Icon_Sword");
            case CraftItem.Spear: return Ws("icon_spear");
            case CraftItem.BattleAxe: return Ws("icon_doubleaxe");
            case CraftItem.WarHammer: return Ws("icon_warhammer");
            case CraftItem.Workbench: return Book("Icon_Workbench");
            default: return Ws("icon_spiketrap");
        }
    }

    static Sprite MaterialIcon(ItemType t)
    {
        switch (t)
        {
            case ItemType.Wood: return Book("Icon_Wood_Logs");
            case ItemType.Stone: return Book("Icon_Stone");
            case ItemType.Herb: return Book("Icon_Herb_Bundle");
            default: return Resources.Load<Sprite>("Crafting/Item_" + t);
        }
    }

    static Cat CategoryOf(Recipe r)
    {
        if (r.isToolUpgrade || r.item == CraftItem.Shield || Weapons.KindOf(r.item).HasValue) return Cat.Tools;
        switch (r.item)
        {
            case CraftItem.HerbSalve:
            case CraftItem.HealthPotion: return Cat.Healing;
            case CraftItem.Torch: return Cat.Light;
            default: return Cat.Building;
        }
    }

    static bool IsLocked(Recipe r)
    {
        if (!r.isToolUpgrade || CraftingState.Instance == null) return false;
        return CraftingState.Instance.TierOf(r.tool) < r.tier - 1;
    }

    static string Description(Recipe r)
    {
        if (!r.isToolUpgrade && r.item == CraftItem.Torch) return "Lights the way and scares Shadow Wraiths.";
        return r.description;
    }

    // ---------------------------------------------------------------- ui helpers

    static RectTransform NewRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static void Place(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);
    }

    static Image Img(Transform p, string name, Sprite s, float x, float y, float w, float h, bool aspect = true, bool raycast = false)
    {
        RectTransform rt = NewRect(name, p);
        Place(rt, x, y, w, h);
        Image im = rt.gameObject.AddComponent<Image>();
        im.sprite = s; im.preserveAspect = aspect; im.raycastTarget = raycast;
        return im;
    }

    TextMeshProUGUI Txt(Transform p, string name, string text, float x, float y, float w, float h, float size, Color c, TextAlignmentOptions a, FontStyles style = FontStyles.Normal)
    {
        RectTransform rt = NewRect(name, p);
        Place(rt, x, y, w, h);
        TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.font = font; t.text = text; t.fontSize = size; t.color = c; t.alignment = a; t.fontStyle = style;
        t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.Normal;
        return t;
    }

    static ColorBlock Tints()
    {
        ColorBlock cb = ColorBlock.defaultColorBlock;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1f, 0.95f, 0.85f);
        cb.pressedColor = new Color(0.8f, 0.75f, 0.7f);
        cb.selectedColor = Color.white;
        cb.disabledColor = new Color(1f, 1f, 1f, 0.35f);
        cb.fadeDuration = 0.08f;
        return cb;
    }

    // ---------------------------------------------------------------- build

    void Build()
    {
        GameObject cgo = new GameObject("RecipeBookCanvas", typeof(RectTransform));
        cgo.transform.SetParent(transform, false);
        canvas = cgo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 122;
        CanvasScaler cs = cgo.AddComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920f, 1080f);
        cs.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        cgo.AddComponent<GraphicRaycaster>();
        group = cgo.AddComponent<CanvasGroup>();

        RectTransform dimRt = NewRect("Dim", cgo.transform);
        dimRt.anchorMin = Vector2.zero; dimRt.anchorMax = Vector2.one; dimRt.offsetMin = dimRt.offsetMax = Vector2.zero;
        Image dim = dimRt.gameObject.AddComponent<Image>();
        dim.color = new Color(0.01f, 0.01f, 0.02f, 0.7f);

        window = NewRect("Window", cgo.transform);
        window.anchorMin = window.anchorMax = window.pivot = new Vector2(0.5f, 0.5f);
        window.sizeDelta = new Vector2(1920f, 1080f);

        Image book = Img(window, "Book", Book("book_bg"), BookX, 0f, 1400f, 1080f, false);
        Transform b = book.transform;

        // close (the red X is part of the book picture; this is the click area)
        Image close = Img(b, "CloseButton", null, 1270f, 44f, 120f, 120f, false, true);
        close.color = new Color(1f, 1f, 1f, 0f);
        Button cb = close.gameObject.AddComponent<Button>();
        cb.onClick.AddListener(DoClose);

        // ---- left page: recipe list
        for (int i = 0; i < LinesPerPage; i++)
        {
            int idx = i;
            LineUI l = new LineUI();
            l.rt = NewRect("Line_" + i, b);
            Place(l.rt, 196f, 168f + i * LineH, 450f, LineH);
            l.highlight = Img(l.rt, "Highlight", Book("row_selected"), 10f, -2f, 432f, LineH + 4f, false);
            l.icon = Img(l.rt, "Icon", null, 44f, 4f, 44f, 42f);
            l.label = Txt(l.rt, "Label", "", 110f, 0f, 330f, LineH, 30f, Ink, TextAlignmentOptions.MidlineLeft);
            Image hit = l.rt.gameObject.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0f);
            l.button = l.rt.gameObject.AddComponent<Button>();
            l.button.transition = Selectable.Transition.None;
            l.button.onClick.AddListener(() => OnLineClicked(idx));
            lineUIs.Add(l);
        }
        Image prev = Img(b, "PrevPage", Book("arrow_left"), 180f, 908f, 90f, 60f, false, true);
        prevBtn = prev.gameObject.AddComponent<Button>();
        prevBtn.colors = Tints();
        prevBtn.onClick.AddListener(() => Turn(-1));
        Image next = Img(b, "NextPage", Book("arrow_right"), 1200f, 908f, 90f, 60f, false, true);
        nextBtn = next.gameObject.AddComponent<Button>();
        nextBtn.colors = Tints();
        nextBtn.onClick.AddListener(() => Turn(1));
        pageNum = Txt(b, "PageNumber", "1 / 1", 340f, 915f, 170f, 50f, 34f, Ink, TextAlignmentOptions.Center, FontStyles.Bold);

        // ---- right page: details
        dTitle = Txt(b, "Title", "", 800f, 170f, 450f, 62f, 50f, Ink, TextAlignmentOptions.Center, FontStyles.Bold);
        dTitle.enableAutoSizing = true; dTitle.fontSizeMin = 30f; dTitle.fontSizeMax = 50f; dTitle.textWrappingMode = TextWrappingModes.NoWrap;
        dPicture = Img(b, "Picture", null, 875f, 252f, 300f, 288f);
        dDesc = Txt(b, "Description", "", 805f, 548f, 445f, 80f, 24f, InkSoft, TextAlignmentOptions.Top, FontStyles.Italic);
        dEffect = Txt(b, "Effect", "", 815f, 600f, 425f, 28f, 21f, new Color(0.45f, 0.26f, 0.06f), TextAlignmentOptions.Top);
        for (int i = 0; i < 3; i++)
        {
            MatUI m = new MatUI();
            m.rt = NewRect("Material_" + i, b);
            Place(m.rt, 830f, 684f + i * 42f, 400f, 42f);
            m.icon = Img(m.rt, "Icon", null, 0f, 0f, 46f, 42f);
            m.name = Txt(m.rt, "Name", "", 66f, 0f, 220f, 42f, 28f, Ink, TextAlignmentOptions.MidlineLeft);
            m.count = Txt(m.rt, "Count", "", 230f, 0f, 160f, 42f, 30f, Ink, TextAlignmentOptions.MidlineRight, FontStyles.Bold);
            mats.Add(m);
        }
        dWhere = Txt(b, "CraftedAt", "", 815f, 852f, 425f, 44f, 26f, InkSoft, TextAlignmentOptions.Center, FontStyles.Italic);

        Txt(window, "Hint", "[B] / [Esc]  close", 0f, 1040f, 1920f, 34f, 22f, new Color(0.95f, 0.87f, 0.7f, 0.75f), TextAlignmentOptions.Center);

        if (EventSystem.current == null && Object.FindAnyObjectByType<EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }
    }

    // ---------------------------------------------------------------- open / close

    void DoOpen()
    {
        if (IsOpen) return;
        IsOpen = true;
        openedFrame = Time.frameCount;
        canvas.enabled = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SetLook(false);
        BuildLines();
        if (selected == null)
            foreach (Line l in lines) if (!l.header && !IsLocked(l.recipe)) { selected = l.recipe; break; }
        page = Mathf.Clamp(PageOf(selected), 0, pageCount - 1);
        Refresh();
        Play(1f);
    }

    void DoClose()
    {
        if (!IsOpen) return;
        IsOpen = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        SetLook(true);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        Play(0.8f);
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

    void BuildLines()
    {
        lines.Clear();
        CraftingState cs = CraftingState.Instance;
        if (cs == null || cs.Recipes == null) return;
        for (int c = 0; c < 4; c++)
        {
            List<Recipe> rs = new List<Recipe>();
            foreach (Recipe r in cs.Recipes) if ((int)CategoryOf(r) == c) rs.Add(r);
            if (rs.Count == 0) continue;
            // keep a header together with at least one recipe on the same page
            int used = lines.Count % LinesPerPage;
            if (used == LinesPerPage - 1) lines.Add(null);
            lines.Add(new Line { header = true, cat = (Cat)c });
            foreach (Recipe r in rs) lines.Add(new Line { recipe = r, cat = (Cat)c });
        }
        pageCount = Mathf.Max(1, Mathf.CeilToInt(lines.Count / (float)LinesPerPage));
    }

    int PageOf(Recipe r)
    {
        for (int i = 0; i < lines.Count; i++) if (lines[i] != null && lines[i].recipe == r) return i / LinesPerPage;
        return 0;
    }

    void Turn(int dir)
    {
        int p = Mathf.Clamp(page + dir, 0, pageCount - 1);
        if (p == page) return;
        page = p;
        Play(1.1f);
        Refresh();
    }

    void OnLineClicked(int i)
    {
        LineUI ui = lineUIs[i];
        if (ui.line == null || ui.line.header) return;
        selected = ui.line.recipe;
        Play(1.3f);
        Refresh();
    }

    void Refresh()
    {
        Inventory inv = Inventory.Instance;

        // list page
        for (int i = 0; i < lineUIs.Count; i++)
        {
            LineUI ui = lineUIs[i];
            int li = page * LinesPerPage + i;
            Line l = li < lines.Count ? lines[li] : null;
            ui.line = l;
            ui.rt.gameObject.SetActive(l != null);
            if (l == null) continue;
            if (l.header)
            {
                ui.highlight.enabled = false;
                ui.icon.sprite = Book(CatIcons[(int)l.cat]);
                ui.icon.color = Color.white;
                ui.icon.rectTransform.anchoredPosition = new Vector2(4f, -2f);
                ui.icon.rectTransform.sizeDelta = new Vector2(50f, 46f);
                ui.label.rectTransform.anchoredPosition = new Vector2(66f, 0f);
                ui.label.text = CatNames[(int)l.cat] + "  <size=60%><color=#5a3c22>————————</color></size>";
                ui.label.fontSize = 36f;
                ui.label.fontStyle = FontStyles.Bold;
                ui.label.color = Ink;
                ui.button.interactable = false;
                continue;
            }
            bool locked = IsLocked(l.recipe);
            bool sel = l.recipe == selected;
            ui.highlight.enabled = sel;
            bool padded = l.recipe.isToolUpgrade && l.recipe.tool != ToolType.Weapon || (!l.recipe.isToolUpgrade && (l.recipe.item == CraftItem.Campfire || l.recipe.item == CraftItem.SpikeTrap || l.recipe.item == CraftItem.Shield));
            float sz = padded && !locked ? 62f : 44f;
            ui.icon.rectTransform.anchoredPosition = new Vector2(66f - sz * 0.5f, -(LineH - sz) * 0.5f);
            ui.icon.rectTransform.sizeDelta = new Vector2(sz, sz);
            ui.icon.sprite = locked ? Book("Icon_Padlock_Small") : RecipeIcon(l.recipe);
            ui.icon.color = locked ? new Color(0.35f, 0.25f, 0.18f, 0.55f) : (l.recipe.isToolUpgrade && l.recipe.tier >= 2 ? new Color(1f, 0.85f, 0.55f) : Color.white);
            ui.label.rectTransform.anchoredPosition = new Vector2(110f, 0f);
            ui.label.text = locked ? "???" : l.recipe.name;
            ui.label.fontSize = 30f;
            ui.label.fontStyle = FontStyles.Normal;
            ui.label.color = locked ? InkFaded : (sel ? new Color(1f, 0.93f, 0.78f) : Ink);
            ui.button.interactable = true;
        }
        pageNum.text = (page + 1) + " / " + pageCount;
        prevBtn.gameObject.SetActive(page > 0);
        nextBtn.gameObject.SetActive(page < pageCount - 1);

        // details
        Recipe r = selected;
        bool show = r != null;
        dTitle.text = show ? (IsLocked(r) ? "???" : r.name) : "";
        dPicture.enabled = show;
        foreach (MatUI m in mats) m.rt.gameObject.SetActive(false);
        if (!show) { dDesc.text = dEffect.text = dWhere.text = ""; return; }

        if (IsLocked(r))
        {
            dPicture.sprite = Book("Icon_Padlock_Small");
            dPicture.color = new Color(0.35f, 0.25f, 0.18f, 0.6f);
            dDesc.text = "Craft the " + CraftingState.TierNames[r.tier - 1] + " upgrade first to learn this recipe.";
            dEffect.text = "";
            dWhere.text = "";
            return;
        }
        dPicture.sprite = RecipeIcon(r);
        dPicture.color = r.isToolUpgrade && r.tier >= 2 ? new Color(1f, 0.85f, 0.55f) : Color.white;

        dDesc.text = Description(r) + "\n<size=82%><color=#734212><i>" + r.effect.Replace("   ", "  - ") + "</i></color></size>";
        dEffect.text = "";
        for (int i = 0; i < mats.Count; i++)
        {
            MatUI m = mats[i];
            if (i >= r.cost.Length) continue;
            Ingredient g = r.cost[i];
            int have = inv != null ? inv.Get(g.item) : 0;
            m.rt.gameObject.SetActive(true);
            m.icon.sprite = MaterialIcon(g.item);
            m.name.text = g.item == ItemType.Herb ? "Herb" : g.item.ToString();
            m.count.text = Mathf.Min(have, 999) + " / " + g.amount;
            m.count.color = have >= g.amount ? Good : Bad;
        }
        // with 3 ingredients the rows sit tighter so they stay inside the box
        float step = r.cost.Length >= 3 ? 40f : 52f;
        float top = r.cost.Length >= 3 ? 682f : 690f;
        for (int i = 0; i < mats.Count; i++) mats[i].rt.anchoredPosition = new Vector2(830f, -(top + i * step));

        bool owned = CraftingState.Instance != null && (r.isToolUpgrade ? CraftingState.Instance.TierOf(r.tool) >= r.tier : ((r.item == CraftItem.Shield && CraftingState.Instance.Count(CraftItem.Shield) > 0) || (Weapons.KindOf(r.item).HasValue && CraftingState.Instance.Count(r.item) > 0)));
        dWhere.text = (r.byHand ? "Crafted at: By Hand [C] or Workbench" : "Crafted at: Workbench") + (owned ? "   <color=#2a6a1e>(owned)</color>" : "");
    }

    // ---------------------------------------------------------------- update

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        openT = Mathf.MoveTowards(openT, IsOpen ? 1f : 0f, dt / 0.2f);
        float k = openT * openT * (3f - 2f * openT);
        group.alpha = k;
        group.blocksRaycasts = IsOpen;
        group.interactable = IsOpen;
        window.localScale = Vector3.one * Mathf.Lerp(0.93f, 1f, k);
        window.anchoredPosition = new Vector2(0f, Mathf.Lerp(-30f, 0f, k));
        if (!IsOpen && openT <= 0f && canvas.enabled) canvas.enabled = false;

        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        if (IsOpen)
        {
            if (Time.frameCount != openedFrame && (kb.bKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame)) { DoClose(); return; }
            if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) Turn(-1);
            if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) Turn(1);
            if (Cursor.lockState != CursorLockMode.None) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            if (Time.frameCount % 20 == 0) Refresh();   // materials can change (pickups)
            return;
        }

        if (!kb.bKey.wasPressedThisFrame) return;
        if (Cursor.lockState != CursorLockMode.Locked) return;           // another window is open
        if (WorkshopUI.IsOpen || ChestLootUI.IsOpen || PlacementController.IsPlacing) return;
        ToolController tc = ToolController.Instance;
        HealthManager hm = tc != null ? tc.GetComponent<HealthManager>() : null;
        if (hm != null && hm.IsDead) return;
        DoOpen();
    }

    void Play(float pitch)
    {
        if (sfx == null || pageClip == null) return;
        sfx.pitch = pitch;
        sfx.PlayOneShot(pageClip, 0.5f);
    }

    /// <summary>Paper rustle for opening the book and turning pages.</summary>
    static AudioClip MakePage()
    {
        const int rate = 44100;
        int n = (int)(rate * 0.32f);
        float[] d = new float[n];
        System.Random rnd = new System.Random(13);
        float lp = 0f, hp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float noise = (float)(rnd.NextDouble() * 2.0 - 1.0);
            lp += (noise - lp) * 0.5f;
            hp = noise - lp;
            float env = Mathf.Sin(Mathf.Clamp01(t / 0.32f) * Mathf.PI) * (0.55f + 0.45f * Mathf.Sin(t * 70f));
            d[i] = Mathf.Clamp((hp * 0.6f + lp * 0.3f) * env * 0.6f, -1f, 1f);
        }
        AudioClip c = AudioClip.Create("bookPage", n, 1, rate, false);
        c.SetData(d, 0);
        return c;
    }
}
