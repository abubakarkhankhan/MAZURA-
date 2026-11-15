using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public enum BagKind { Empty, Resource, Crafted, Tool, Map }

/// <summary>One kind of thing the player can own. The backpack shows one stack per kind.</summary>
public struct BagItem
{
    public BagKind kind;
    public ItemType res;
    public CraftItem item;
    public ToolType tool;

    public string Key { get { return kind == BagKind.Empty ? "" : kind + "_" + res + "_" + item + "_" + tool; } }
    public bool IsEmpty { get { return kind == BagKind.Empty; } }

    public static BagItem Res(ItemType t) { return new BagItem { kind = BagKind.Resource, res = t }; }
    public static BagItem Craft(CraftItem c) { return new BagItem { kind = BagKind.Crafted, item = c }; }
    public static BagItem Tool(ToolType t) { return new BagItem { kind = BagKind.Tool, tool = t }; }
    public static BagItem MapPiece() { return new BagItem { kind = BagKind.Map }; }
    public static BagItem Wpn(CraftItem w) { return new BagItem { kind = BagKind.Tool, tool = ToolType.Weapon, item = w }; }
    public bool IsWeapon { get { return kind == BagKind.Tool && tool == ToolType.Weapon; } }
}

/// <summary>
/// Inventory window (I / Tab): a 20-slot backpack with everything the player owns (stacked),
/// the 5 hotbar slots under it, item details and USE / EQUIP / DROP.
/// Drag between backpack slots to sort, drag into the hotbar to put an item on keys 1-5,
/// drag out of the hotbar to take it off. Hover a backpack slot and press 1-5 to put it there.
/// The counts come from the real game systems (Inventory, CraftingState, MapManager).
/// </summary>
public class InventoryWindow : MonoBehaviour
{
    public static InventoryWindow Instance { get; private set; }
    public static bool IsOpen { get; private set; }

    public const int BagSize = 20;
    const int Cols = 5;

    static readonly Color Cream = new Color(1f, 0.9f, 0.7f);
    static readonly Color Gold = new Color(1f, 0.8f, 0.38f);
    static readonly Color Ink = new Color(0.25f, 0.15f, 0.07f);
    static readonly Color Dim = new Color(0.85f, 0.75f, 0.6f, 0.8f);

    // every kind of item, in the order new things are put into the backpack
    static readonly BagItem[] AllItems =
    {
        BagItem.Tool(ToolType.Axe), BagItem.Tool(ToolType.Pickaxe), BagItem.Wpn(CraftItem.Sword), BagItem.Wpn(CraftItem.Spear), BagItem.Wpn(CraftItem.BattleAxe), BagItem.Wpn(CraftItem.WarHammer), BagItem.Craft(CraftItem.Shield),
        BagItem.Craft(CraftItem.Torch), BagItem.Craft(CraftItem.HealthPotion), BagItem.Craft(CraftItem.HerbSalve),
        BagItem.Craft(CraftItem.Campfire), BagItem.Craft(CraftItem.SpikeTrap), BagItem.Craft(CraftItem.Workbench),
        BagItem.Res(ItemType.Wood), BagItem.Res(ItemType.Stone), BagItem.Res(ItemType.Herb), BagItem.Res(ItemType.Ruby),
        BagItem.Res(ItemType.Emerald), BagItem.Res(ItemType.Sapphire), BagItem.Res(ItemType.Amber), BagItem.MapPiece()
    };

    class SlotView
    {
        public bool hot; public int index;
        public RectTransform root;
        public Image frame, icon, qtyBadge;
        public TextMeshProUGUI qty;
        public bool hover;
    }

    class Btn
    {
        public Image img; public TextMeshProUGUI text;
        public Sprite normal, hover, disabled;
        public bool interactable = true, over;
    }

    readonly BagItem[] bag = new BagItem[BagSize];
    readonly SlotView[] bagViews = new SlotView[BagSize];
    readonly SlotView[] hotViews = new SlotView[Hotbar.Size];

    Canvas canvas;
    CanvasGroup group;
    RectTransform window;
    TMP_FontAsset font;
    Image dIcon; TextMeshProUGUI dName, dDesc, dCount, status, capacity;
    Btn bUse, bEquip, bDrop;
    Image dragIcon;
    bool selHot; int selIndex;
    SlotView dragFrom, hoverSlot;
    float openT, statusT;
    Sprite sNormal, sHover, sSelected;
    MonoBehaviour lookInputs;

    static Sprite S(string n) { return Resources.Load<Sprite>("InventoryUI/" + n); }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (Instance != null || S("inventory_frame") == null) return;
        new GameObject("InventoryWindow").AddComponent<InventoryWindow>();
    }

    void Awake()
    {
        Instance = this;
        foreach (InventoryUI old in Object.FindObjectsByType<InventoryUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            old.gameObject.SetActive(false);   // the old grid window is replaced by this one
        Build();
        canvas.enabled = false;
    }

    // ============================================================ data
    public static int Count(BagItem b)
    {
        CraftingState cs = CraftingState.Instance;
        switch (b.kind)
        {
            case BagKind.Resource: return Inventory.Instance != null ? Inventory.Instance.Get(b.res) : 0;
            case BagKind.Crafted: return cs != null ? cs.Count(b.item) : 0;
            case BagKind.Tool:
                if (b.tool == ToolType.Weapon) return cs != null && cs.Count(b.item) > 0 ? 1 : 0;
                return cs != null && cs.HasTool(b.tool) ? 1 : 0;
            case BagKind.Map: return MapManager.Instance != null ? MapManager.Instance.CollectedPieces : 0;
        }
        return 0;
    }

    // an item lives in ONE place: on the hotbar or in the backpack, never both
    static bool OnHotbar(BagItem b)
    {
        Hotbar hb = Hotbar.Instance;
        if (hb == null || b.IsEmpty) return false;
        for (int i = 0; i < Hotbar.Size; i++) if (FromHotbar(hb[i]).Key == b.Key) return true;
        return false;
    }

    void SyncBag()
    {
        for (int i = 0; i < BagSize; i++)
            if (!bag[i].IsEmpty && (Count(bag[i]) <= 0 || OnHotbar(bag[i]))) bag[i] = new BagItem();
        foreach (BagItem b in AllItems)
        {
            if (Count(b) <= 0 || OnHotbar(b)) continue;
            bool there = false;
            for (int i = 0; i < BagSize; i++) if (bag[i].Key == b.Key) { there = true; break; }
            if (there) continue;
            for (int i = 0; i < BagSize; i++) if (bag[i].IsEmpty) { bag[i] = b; break; }
        }
    }

    static bool ToHotbar(BagItem b, out HotbarEntry e)
    {
        e = new HotbarEntry();
        if (b.kind == BagKind.Tool) { e.kind = HotbarKind.Tool; e.tool = b.tool; return true; }
        if (b.kind != BagKind.Crafted) return false;
        switch (b.item)
        {
            case CraftItem.Torch: e.kind = HotbarKind.Torch; return true;
            case CraftItem.Campfire: case CraftItem.SpikeTrap: case CraftItem.Workbench: e.kind = HotbarKind.Place; e.item = b.item; return true;
            case CraftItem.HealthPotion: case CraftItem.HerbSalve: e.kind = HotbarKind.Use; e.item = b.item; return true;
        }
        return false;
    }

    static BagItem FromHotbar(HotbarEntry e)
    {
        switch (e.kind)
        {
            case HotbarKind.Tool: return e.tool == ToolType.Weapon ? BagItem.Wpn(Weapons.Info.item) : BagItem.Tool(e.tool);
            case HotbarKind.Torch: return BagItem.Craft(CraftItem.Torch);
            case HotbarKind.Place: case HotbarKind.Use: return BagItem.Craft(e.item);
        }
        return new BagItem();
    }

    public static string NameOf(BagItem b)
    {
        switch (b.kind)
        {
            case BagKind.Resource: return b.res.ToString();
            case BagKind.Tool: return b.tool == ToolType.Weapon ? WInfo(b).name : b.tool.ToString();
            case BagKind.Map: return "Map Pieces";
            case BagKind.Crafted:
                switch (b.item)
                {
                    case CraftItem.HealthPotion: return "Health Potion";
                    case CraftItem.HerbSalve: return "Herb Salve";
                    case CraftItem.SpikeTrap: return "Spike Trap";
                    case CraftItem.Shield: return "Wooden Shield";
                }
                return b.item.ToString();
        }
        return "";
    }

    public static Sprite IconOf(BagItem b)
    {
        string n = null;
        switch (b.kind)
        {
            case BagKind.Resource:
                switch (b.res)
                {
                    case ItemType.Wood: n = "HUD/in_resource_wood_log"; break;
                    case ItemType.Stone: n = "HUD/in_resource_stone"; break;
                    case ItemType.Herb: n = "HUD/in_resource_green_herb"; break;
                    case ItemType.Ruby: n = "HUD/in_resource_red_ruby"; break;
                    case ItemType.Emerald: n = "HUD/in_resource_green_emerald"; break;
                    case ItemType.Sapphire: n = "HUD/in_resource_blue_sapphire"; break;
                    case ItemType.Amber: n = "HUD/in_resource_orange_amber"; break;
                }
                break;
            case BagKind.Tool: n = b.tool == ToolType.Axe ? "HUD/in_item_axe" : b.tool == ToolType.Pickaxe ? "HUD/in_item_pickaxe" : WInfo(b).hudIcon; break;
            case BagKind.Map: n = "HUD/map_socket_gold"; break;
            case BagKind.Crafted:
                switch (b.item)
                {
                    case CraftItem.Torch: n = "HUD/in_item_torch"; break;
                    case CraftItem.Campfire: n = "HUD/in_item_campfire"; break;
                    case CraftItem.SpikeTrap: n = "HUD/in_item_spike_trap"; break;
                    case CraftItem.Workbench: n = "HUD/in_item_wooden_workbench"; break;
                    case CraftItem.Shield: n = "HUD/in_item_wooden_shield"; break;
                    case CraftItem.HealthPotion: n = "RecipeBookUI/Icon_Health_Potion"; break;
                    case CraftItem.HerbSalve: n = "RecipeBookUI/Icon_Herb_Salve"; break;
                    case CraftItem.Sword: n = "HUD/in_item_sword"; break;
                }
                break;
        }
        return n != null ? Resources.Load<Sprite>(n) : null;
    }

    static string Describe(BagItem b)
    {
        CraftingState cs = CraftingState.Instance;
        switch (b.kind)
        {
            case BagKind.Resource:
                switch (b.res)
                {
                    case ItemType.Wood: return "Chopped from trees. Used for almost every recipe.";
                    case ItemType.Stone: return "Mined from rocks. Used for tools, traps and the workbench.";
                    case ItemType.Herb: return "Picked from herb plants. Used for salves and potions.";
                    default: return "A rare crystal from the maze. Used for strong tool upgrades.";
                }
            case BagKind.Tool:
                string tier = cs != null ? "  (" + CraftingState.TierNames[Mathf.Clamp(cs.TierOf(b.tool), 0, 2)] + ")" : "";
                if (b.tool == ToolType.Axe) return "Chops trees for wood." + tier;
                if (b.tool == ToolType.Pickaxe) return "Breaks stones and crystals." + tier;
                return WInfo(b).desc + tier;
            case BagKind.Map: return "Pieces of the maze map. Find all 4 to find the way out.";
            case BagKind.Crafted:
                switch (b.item)
                {
                    case CraftItem.Torch: return "Lights the way at night and keeps some creatures away.";
                    case CraftItem.HealthPotion: return "Drink it to heal 60 health at once.";
                    case CraftItem.HerbSalve: return "Heals 30 health slowly over 4 seconds.";
                    case CraftItem.Campfire: return "Place it to rest and heal near the fire.";
                    case CraftItem.SpikeTrap: return "Place it on a path. Hurts enemies that walk over it.";
                    case CraftItem.Workbench: return "Place it to unlock the full Workshop.";
                    case CraftItem.Shield: return "Always ready: hold the right mouse button to block hits from the front.";
                }
                break;
        }
        return "";
    }

    static WeaponInfo WInfo(BagItem b)
    {
        WeaponKind? k = Weapons.KindOf(b.item);
        return Weapons.InfoOf(k.HasValue ? k.Value : WeaponKind.Sword);
    }

    static string QtyText(BagItem b)
    {
        int n = Count(b);
        if (b.kind == BagKind.Tool)
        {
            CraftingState cs = CraftingState.Instance;
            int t = cs != null ? cs.TierOf(b.tool) : 0;
            return t == 0 ? "" : (t == 1 ? "I" : "II");
        }
        if (b.kind == BagKind.Crafted && b.item == CraftItem.Shield) return "";
        if (b.kind == BagKind.Map) return n + "/4";
        return n > 0 ? n.ToString() : "";
    }

    BagItem Selected()
    {
        if (selHot) { Hotbar hb = Hotbar.Instance; return hb != null ? FromHotbar(hb[selIndex]) : new BagItem(); }
        return bag[selIndex];
    }

    // ============================================================ build
    RectTransform Rect(string name, Transform parent, float x, float y, float w, float h)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform r = (RectTransform)go.transform;
        r.SetParent(parent, false);
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = new Vector2(x, y);
        r.sizeDelta = new Vector2(w, h);
        return r;
    }

    Image Img(string name, Transform parent, Sprite s, float x, float y, float w, float h, bool raycast = false)
    {
        RectTransform r = Rect(name, parent, x, y, w, h);
        Image i = r.gameObject.AddComponent<Image>();
        i.sprite = s; i.raycastTarget = raycast;
        return i;
    }

    TextMeshProUGUI Txt(string name, Transform parent, string t, float x, float y, float w, float h, float size, Color c, TextAlignmentOptions al)
    {
        RectTransform r = Rect(name, parent, x, y, w, h);
        TextMeshProUGUI tm = r.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) tm.font = font;
        tm.text = t; tm.fontSize = size; tm.color = c; tm.alignment = al; tm.raycastTarget = false;
        return tm;
    }

    static void On(GameObject go, EventTriggerType type, System.Action<BaseEventData> a)
    {
        EventTrigger et = go.GetComponent<EventTrigger>();
        if (et == null) et = go.AddComponent<EventTrigger>();
        EventTrigger.Entry e = new EventTrigger.Entry { eventID = type };
        e.callback.AddListener(d => a(d));
        et.triggers.Add(e);
    }

    void LoadFont()
    {
        if (WorkshopUI.Instance != null)
        {
            FieldInfo f = typeof(WorkshopUI).GetField("font", BindingFlags.NonPublic | BindingFlags.Instance);
            if (f != null) font = f.GetValue(WorkshopUI.Instance) as TMP_FontAsset;
        }
        if (font == null) font = TMP_Settings.defaultFontAsset;
    }

    // frame art is 1448 x 1086, drawn at scale K; F(x,y) turns frame pixels into window units
    const float K = 0.83f;
    static Vector2 F(float fx, float fy) { return new Vector2((fx - 724f) * K, (543f - fy) * K); }

    void Build()
    {
        LoadFont();
        sNormal = S("slot_normal"); sHover = S("slot_hover"); sSelected = S("slot_selected");

        GameObject cgo = new GameObject("InventoryCanvas2", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        cgo.transform.SetParent(transform, false);
        canvas = cgo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 60;
        CanvasScaler cs = cgo.GetComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920f, 1080f);
        cs.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        group = cgo.GetComponent<CanvasGroup>();

        Image shade = Img("Shade", cgo.transform, null, 0f, 0f, 0f, 0f, true);
        shade.rectTransform.anchorMin = Vector2.zero; shade.rectTransform.anchorMax = Vector2.one; shade.rectTransform.sizeDelta = Vector2.zero;
        shade.color = new Color(0f, 0f, 0f, 0.55f);
        On(shade.gameObject, EventTriggerType.Drop, d => DropOnNothing());

        window = Rect("Window", cgo.transform, 0f, -10f, 1448f * K, 1086f * K);
        Img("Frame", window, S("inventory_frame"), 0f, 0f, 1448f * K, 1086f * K, true);

        Vector2 tp = F(724f, 136f);
        TextMeshProUGUI title = Txt("Title", window, "INVENTORY", tp.x, tp.y, 420f, 60f, 38f, new Color(1f, 0.95f, 0.85f), TextAlignmentOptions.Center);
        title.fontStyle = FontStyles.Bold; title.outlineWidth = 0.22f; title.outlineColor = new Color32(70, 25, 5, 255); title.characterSpacing = 6f;

        Vector2 cp = F(1352f, 118f);
        Image close = Img("Close", window, S("close_button_normal"), cp.x, cp.y, 64f, 64f, true);
        Sprite cn = S("close_button_normal"), ch = S("close_button_hover");
        On(close.gameObject, EventTriggerType.PointerEnter, d => close.sprite = ch);
        On(close.gameObject, EventTriggerType.PointerExit, d => close.sprite = cn);
        On(close.gameObject, EventTriggerType.PointerClick, d => Close());

        // ---- left: backpack grid 5 x 4 + hotbar row
        const float S0 = 100f, gap = 10f;
        Vector2 g0 = F(150f, 250f);   // top-left of the grid
        Txt("BagLabel", window, "BACKPACK", g0.x + 270f, g0.y + 18f, 540f, 30f, 22f, Gold, TextAlignmentOptions.MidlineLeft).characterSpacing = 4f;
        capacity = Txt("Capacity", window, "", g0.x + 270f, g0.y + 18f, 540f, 30f, 20f, Dim, TextAlignmentOptions.MidlineRight);
        for (int i = 0; i < BagSize; i++)
        {
            int c = i % Cols, r = i / Cols;
            bagViews[i] = MakeSlot(false, i, g0.x + S0 / 2f + c * (S0 + gap), g0.y - 12f - S0 / 2f - r * (S0 + gap), S0);
        }
        float hy = g0.y - 12f - 4f * (S0 + gap) - 44f;
        Txt("HotLabel", window, "HOTBAR   (keys 1 - 5)", g0.x + 270f, hy + 4f, 540f, 30f, 22f, Gold, TextAlignmentOptions.MidlineLeft).characterSpacing = 3f;
        for (int i = 0; i < Hotbar.Size; i++)
            hotViews[i] = MakeSlot(true, i, g0.x + S0 / 2f + i * (S0 + gap), hy - 22f - S0 / 2f, S0);

        // ---- right: details + buttons + help
        float rx = F(1040f, 0f).x;   // centre x of the right column
        float top = g0.y + 10f;
        const float dk = 0.72f;     // details art 540 x 198
        float dcy = top - 198f * dk / 2f;
        Image det = Img("Details", window, S("item_details_panel"), rx, dcy, 540f * dk, 198f * dk);
        dIcon = Img("DIcon", det.transform, null, (98f - 270f) * dk, (99f - 108f) * dk, 66f, 66f);
        dIcon.preserveAspect = true;
        dName = Txt("DName", det.transform, "", 58f, 32f, 250f, 30f, 22f, Ink, TextAlignmentOptions.MidlineLeft);
        dName.fontStyle = FontStyles.Bold; dName.enableWordWrapping = false;
        dDesc = Txt("DDesc", det.transform, "", 58f, -18f, 250f, 70f, 16f, Ink, TextAlignmentOptions.TopLeft);
        dCount = Txt("DCount", window, "", rx, dcy - 198f * dk / 2f - 18f, 410f, 28f, 20f, Cream, TextAlignmentOptions.Center);

        float by = dcy - 198f * dk / 2f - 70f;
        bUse = MakeButton("USE", "use", rx - 138f, by, UseSelected);
        bEquip = MakeButton("EQUIP", "equip", rx, by, EquipSelected);
        bDrop = MakeButton("DROP", "drop", rx + 138f, by, DropSelected);
        status = Txt("Status", window, "", rx, by - 56f, 420f, 34f, 20f, Gold, TextAlignmentOptions.Center);

        Image div = Img("Divider", window, S("decorative_gold_divider"), rx, by - 96f, 410f, 30f);
        div.preserveAspect = true;
        Txt("Help", window,
            "<b>Drag</b> items to sort your backpack.\n<b>Drag into the hotbar</b> to use them with keys 1-5.\n<b>Drag out of the hotbar</b> to take them off.\nHover an item and press <b>1-5</b> to put it on that key.\n<b>Double-click</b> to use or equip.",
            rx, by - 210f, 410f, 190f, 18f, Dim, TextAlignmentOptions.Top);

        dragIcon = Img("DragIcon", cgo.transform, null, 0f, 0f, 84f, 84f);
        dragIcon.preserveAspect = true; dragIcon.enabled = false;
    }

    SlotView MakeSlot(bool hot, int index, float x, float y, float size)
    {
        SlotView v = new SlotView { hot = hot, index = index };
        v.root = Rect((hot ? "Hot" : "Bag") + (index + 1), window, x, y, size, size);
        v.frame = Img("Frame", v.root, sNormal, 0f, 0f, size, size * 204f / 217f, true);
        v.icon = Img("Icon", v.root, null, 0f, 2f, size * 0.66f, size * 0.66f);
        v.icon.preserveAspect = true;
        if (hot)
        {
            Image badge = Img("Badge", v.root, S("number_badge_1"), -size / 2f + 15f, size / 2f - 17f, 30f, 29f);
            TextMeshProUGUI num = Txt("Num", badge.transform, (index + 1).ToString(), 0f, 1f, 28f, 28f, 17f, Cream, TextAlignmentOptions.Center);
            num.fontStyle = FontStyles.Bold;
        }
        v.qtyBadge = Img("QtyBadge", v.root, S("quantity_badge"), size / 2f - 17f, -size / 2f + 19f, 34f, 34f);
        v.qty = Txt("Qty", v.qtyBadge.transform, "", 0f, 1f, 34f, 28f, 15f, Gold, TextAlignmentOptions.Center);
        v.qty.enableAutoSizing = true; v.qty.fontSizeMin = 9f; v.qty.fontSizeMax = 15f;
        v.qty.fontStyle = FontStyles.Bold; v.qty.enableWordWrapping = false;

        GameObject f = v.frame.gameObject;
        On(f, EventTriggerType.PointerEnter, d => { v.hover = true; hoverSlot = v; });
        On(f, EventTriggerType.PointerExit, d => { v.hover = false; if (hoverSlot == v) hoverSlot = null; });
        On(f, EventTriggerType.PointerClick, d =>
        {
            PointerEventData p = (PointerEventData)d;
            selHot = v.hot; selIndex = v.index;
            if (p.clickCount >= 2) QuickAction();
        });
        On(f, EventTriggerType.BeginDrag, d => BeginDrag(v));
        On(f, EventTriggerType.Drag, d => { if (dragIcon.enabled) dragIcon.rectTransform.position = ((PointerEventData)d).position; });
        On(f, EventTriggerType.Drop, d => DropOn(v));
        On(f, EventTriggerType.EndDrag, d => EndDrag());
        return v;
    }

    Btn MakeButton(string label, string art, float x, float y, System.Action onClick)
    {
        Btn b = new Btn();
        b.normal = S(art + "_normal"); b.hover = S(art + "_hover"); b.disabled = S(art + "_disabled");
        b.img = Img("Btn_" + label, window, b.normal, x, y, 152f, 86f, true);
        b.img.preserveAspect = true;
        b.text = Txt("T", b.img.transform, label, 0f, 1f, 118f, 46f, 21f, Cream, TextAlignmentOptions.Center);
        b.text.fontStyle = FontStyles.Bold; b.text.outlineWidth = 0.2f; b.text.outlineColor = new Color32(20, 10, 4, 255);
        b.text.enableWordWrapping = false;
        On(b.img.gameObject, EventTriggerType.PointerEnter, d => { b.over = true; Paint(b); });
        On(b.img.gameObject, EventTriggerType.PointerExit, d => { b.over = false; Paint(b); });
        On(b.img.gameObject, EventTriggerType.PointerClick, d => { if (b.interactable) onClick(); });
        return b;
    }

    static void Paint(Btn b)
    {
        b.img.sprite = !b.interactable ? b.disabled : b.over ? b.hover : b.normal;
        b.text.color = b.interactable ? Cream : new Color(0.78f, 0.78f, 0.78f, 0.7f);
    }

    // ============================================================ open / close
    public static void Toggle() { if (Instance == null) return; if (IsOpen) Instance.Close(); else Instance.Open(); }

    public void Open()
    {
        if (IsOpen) return;
        IsOpen = true;
        SyncBag();
        canvas.enabled = true;
        openT = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SetLook(false);
        if (EventSystem.current == null)
        {
            GameObject es = new GameObject("EventSystem", typeof(EventSystem));
            es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }
        status.text = "";
        Hotbar hb = Hotbar.Instance;
        if (hb != null && hb.Selected >= 0) { selHot = true; selIndex = hb.Selected; }
        else { selHot = false; selIndex = 0; }
        Refresh();
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        EndDrag();
        canvas.enabled = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        SetLook(true);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }

    void SetLook(bool on)
    {
        if (lookInputs == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                foreach (MonoBehaviour m in player.GetComponents<MonoBehaviour>())
                    if (m != null && m.GetType().Name == "StarterAssetsInputs") { lookInputs = m; break; }
        }
        if (lookInputs == null) return;
        System.Type t = lookInputs.GetType();
        FieldInfo f = t.GetField("cursorInputForLook"); if (f != null) f.SetValue(lookInputs, on);
        f = t.GetField("look"); if (f != null) f.SetValue(lookInputs, Vector2.zero);
        f = t.GetField("move"); if (f != null) f.SetValue(lookInputs, Vector2.zero);
    }

    void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return;
        bool key = kb.iKey.wasPressedThisFrame || kb.tabKey.wasPressedThisFrame;
        if (IsOpen)
        {
            if (key || kb.escapeKey.wasPressedThisFrame) { Close(); return; }
            SyncBag();
            // hover a backpack item and press 1-5: put it on that hotbar key
            for (int i = 0; i < Hotbar.Size; i++)
                if (kb[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame)
                {
                    if (hoverSlot != null && !hoverSlot.hot) PutOnHotbar(hoverSlot.index, i);
                    else { selHot = true; selIndex = i; }
                }
            openT = Mathf.Min(1f, openT + Time.unscaledDeltaTime * 7f);
            float e = 1f - Mathf.Pow(1f - openT, 3f);
            group.alpha = e;
            window.localScale = Vector3.one * Mathf.Lerp(0.92f, 1f, e);
            if (statusT > 0f) { statusT -= Time.unscaledDeltaTime; if (statusT <= 0f) status.text = ""; }
            Refresh();
        }
        else if (key)
        {
            if (Cursor.lockState != CursorLockMode.Locked) return;
            if (WorkshopUI.IsOpen || ChestLootUI.IsOpen || RecipeBookUI.IsOpen || ConstructionSite.Busy || PlacementController.IsPlacing) return;
            Open();
        }
    }

    // ============================================================ view
    void PaintSlot(SlotView v, BagItem b, bool selected, bool active)
    {
        bool has = !b.IsEmpty;
        v.frame.sprite = selected ? sSelected : v.hover ? sHover : sNormal;
        float w = v.root.sizeDelta.x;
        v.frame.rectTransform.sizeDelta = selected
            ? new Vector2(w * (177f / 217f) * (256f / 171f), w * (177f / 217f) * (250f / 171f))
            : new Vector2(w, w * 204f / 217f);
        v.frame.color = (has || selected || v.hover) ? Color.white : new Color(0.62f, 0.58f, 0.55f, 1f);
        v.icon.enabled = has;
        if (has) v.icon.sprite = IconOf(b);
        v.icon.color = dragFrom == v ? new Color(1f, 1f, 1f, 0.3f) : Color.white;
        string q = has ? QtyText(b) : "";
        v.qty.text = q;
        v.qtyBadge.enabled = q.Length > 0;
        v.qtyBadge.color = active ? new Color(0.6f, 1f, 0.6f) : Color.white;
    }

    void Refresh()
    {
        Hotbar hb = Hotbar.Instance;
        int active = hb != null ? hb.Selected : -1;
        int used = 0;
        for (int i = 0; i < BagSize; i++)
        {
            if (!bag[i].IsEmpty) used++;
            PaintSlot(bagViews[i], bag[i], !selHot && selIndex == i, false);
        }
        for (int i = 0; i < Hotbar.Size; i++)
            PaintSlot(hotViews[i], hb != null ? FromHotbar(hb[i]) : new BagItem(), selHot && selIndex == i, i == active);
        capacity.text = used + " / " + BagSize;

        BagItem s = Selected();
        bool any = !s.IsEmpty;
        dIcon.enabled = any;
        if (any) dIcon.sprite = IconOf(s);
        dName.text = any ? NameOf(s) : "Empty slot";
        dDesc.text = any ? Describe(s) : "Nothing here. Collect, craft or loot items and they appear in your backpack.";
        int n = any ? Count(s) : 0;
        dCount.text = !any ? "" : s.kind == BagKind.Tool || (s.kind == BagKind.Crafted && s.item == CraftItem.Shield) ? "Owned" : s.kind == BagKind.Map ? "Found " + n + " of 4" : "You have " + n;

        bool isPlace = s.kind == BagKind.Crafted && (s.item == CraftItem.Campfire || s.item == CraftItem.SpikeTrap || s.item == CraftItem.Workbench);
        bool isDrink = s.kind == BagKind.Crafted && (s.item == CraftItem.HealthPotion || s.item == CraftItem.HerbSalve);
        bool isTorch = s.kind == BagKind.Crafted && s.item == CraftItem.Torch;
        bool isTool = s.kind == BagKind.Tool;

        bUse.interactable = isPlace || isDrink;
        bUse.text.text = isPlace ? "PLACE" : "USE";
        bEquip.interactable = isTool || isTorch;
        bool equipped = (isTool && ToolController.Instance != null && ToolController.Instance.CurrentTool == s.tool && !TorchController.IsHeld && (!s.IsWeapon || Weapons.Info.item == s.item)) || (isTorch && TorchController.IsHeld);
        bEquip.text.text = equipped ? "UNEQUIP" : "EQUIP";
        bDrop.interactable = any && (s.kind == BagKind.Resource || isPlace || isDrink || isTorch);
        Paint(bUse); Paint(bEquip); Paint(bDrop);
    }

    void Say(string msg) { status.text = msg; statusT = 2.5f; }

    // ============================================================ actions
    void QuickAction()
    {
        BagItem s = Selected();
        if (s.kind == BagKind.Tool || (s.kind == BagKind.Crafted && s.item == CraftItem.Torch)) EquipSelected();
        else UseSelected();
    }

    void EquipSelected()
    {
        BagItem s = Selected();
        ToolController tc = ToolController.Instance;
        if (tc == null) return;
        if (s.kind == BagKind.Tool)
        {
            if (TorchController.IsHeld) TorchController.Toggle();
            bool on = tc.CurrentTool == s.tool && (!s.IsWeapon || Weapons.Info.item == s.item);
            if (s.IsWeapon && !on) { WeaponKind? wk = Weapons.KindOf(s.item); if (wk.HasValue) Weapons.Select(wk.Value); }
            tc.Equip(on ? ToolType.None : s.tool);
            Say(on ? NameOf(s) + " put away" : NameOf(s) + " equipped");
        }
        else if (s.kind == BagKind.Crafted && s.item == CraftItem.Torch)
        {
            TorchController.Toggle();
            Say(TorchController.IsHeld ? "Torch lit" : "Torch put away");
        }
    }

    void UseSelected()
    {
        BagItem s = Selected();
        CraftingState cs = CraftingState.Instance;
        if (s.kind != BagKind.Crafted || cs == null) return;
        switch (s.item)
        {
            case CraftItem.HealthPotion: if (cs.UsePotion()) Say("+60 health"); break;
            case CraftItem.HerbSalve: if (cs.UseSalve()) Say("Healing..."); break;
            case CraftItem.Campfire: case CraftItem.SpikeTrap: case CraftItem.Workbench:
                if (PlacementController.Instance == null) return;
                Close();
                PlacementController.Instance.BeginItem(s.item);
                break;
        }
    }

    void DropSelected()
    {
        BagItem s = Selected();
        CraftingState cs = CraftingState.Instance;
        bool ok = false;
        if (s.kind == BagKind.Resource && Inventory.Instance != null && Inventory.Instance.Get(s.res) > 0) { Inventory.Instance.Remove(s.res, 1); ok = true; }
        else if (s.kind == BagKind.Crafted && cs != null && s.item != CraftItem.Shield)
        {
            if (s.item == CraftItem.Torch && TorchController.IsHeld && cs.Count(CraftItem.Torch) <= 1) TorchController.Toggle();
            ok = cs.Consume(s.item);
        }
        Say(ok ? "Dropped 1 " + NameOf(s) : "Can't drop that");
    }

    void PutOnHotbar(int bagIndex, int slot)
    {
        Hotbar hb = Hotbar.Instance;
        BagItem b = bag[bagIndex];
        if (hb == null || b.IsEmpty) return;
        HotbarEntry e;
        if (!ToHotbar(b, out e)) { Say(NameOf(b) + " can't go on the hotbar"); return; }
        BagItem old = FromHotbar(hb[slot]);
        if (b.IsWeapon) { WeaponKind? wk = Weapons.KindOf(b.item); if (wk.HasValue) Weapons.Select(wk.Value); }
        hb.Assign(slot, e);
        bag[bagIndex] = old;   // whatever was on that key goes back into the backpack here
        selHot = true; selIndex = slot;
        Say(NameOf(b) + " -> key " + (slot + 1));
    }

    // ============================================================ drag & drop
    void BeginDrag(SlotView v)
    {
        BagItem b = v.hot ? (Hotbar.Instance != null ? FromHotbar(Hotbar.Instance[v.index]) : new BagItem()) : bag[v.index];
        if (b.IsEmpty) return;
        dragFrom = v;
        dragIcon.sprite = IconOf(b);
        dragIcon.enabled = true;
        dragIcon.transform.SetAsLastSibling();
    }

    void DropOn(SlotView to)
    {
        SlotView from = dragFrom;
        if (from == null || from == to) return;
        Hotbar hb = Hotbar.Instance;
        if (!from.hot && !to.hot)
        {
            BagItem t = bag[from.index]; bag[from.index] = bag[to.index]; bag[to.index] = t;
            selHot = false; selIndex = to.index;
        }
        else if (!from.hot && to.hot) PutOnHotbar(from.index, to.index);
        else if (from.hot && to.hot) { if (hb != null) hb.Swap(from.index, to.index); selHot = true; selIndex = to.index; }
        else if (from.hot && !to.hot)
        {
            if (hb == null) return;
            BagItem b = FromHotbar(hb[from.index]);
            BagItem there = bag[to.index];
            HotbarEntry back;
            if (!there.IsEmpty && ToHotbar(there, out back)) { hb.Assign(from.index, back); bag[to.index] = b; }   // swap the two
            else
            {
                hb.Clear(from.index);
                if (there.IsEmpty) bag[to.index] = b;
            }
            selHot = false; selIndex = to.index;
            Say(NameOf(b) + " moved to the backpack");
        }
    }

    void DropOnNothing()
    {
        if (dragFrom != null && dragFrom.hot && Hotbar.Instance != null) Hotbar.Instance.Clear(dragFrom.index);
    }

    void EndDrag()
    {
        dragFrom = null;
        if (dragIcon != null) dragIcon.enabled = false;
    }
}
