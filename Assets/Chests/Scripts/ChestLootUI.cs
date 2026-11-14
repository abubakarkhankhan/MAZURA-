using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// The window that shows what is inside an opened MapChest: up to 8 slots.
/// Click a slot to take that item, or "TAKE ALL". Esc / E / X closes it.
/// Built at runtime on its own canvas; uses the Workshop sprites so the look matches.
/// </summary>
public class ChestLootUI : MonoBehaviour
{
    public static ChestLootUI Instance { get; private set; }
    public static bool IsOpen { get; private set; }
    /// <summary>Frame the window was last closed (so the same E press does not reopen it).</summary>
    public static int ClosedFrame = -10;

    static readonly Color Parchment = new Color(0.95f, 0.87f, 0.70f);
    static readonly Color Gold = new Color(1f, 0.80f, 0.42f);

    Canvas canvas;
    CanvasGroup group;
    RectTransform window;
    float openT;
    TMP_FontAsset font, numFont;
    MapChest chest;
    int openedFrame;

    class SlotUI { public Image frame, icon; public TextMeshProUGUI name, count; public Button button; public bool hover; }
    readonly List<SlotUI> slots = new List<SlotUI>();
    TextMeshProUGUI emptyText, hint;

    MonoBehaviour lookInputs;
    FieldInfo lookEnabledField, lookVectorField;
    AudioSource sfx;
    AudioClip takeClip;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (Instance != null) return;
        new GameObject("ChestLootUI").AddComponent<ChestLootUI>();
    }

    public static void Open(MapChest c) { if (Instance != null) Instance.DoOpen(c); }
    public static void Close() { if (Instance != null) Instance.DoClose(); }

    void Awake()
    {
        Instance = this;
        Font f = Resources.Load<Font>("WorkshopUI/Alegreya-SemiBold");
        if (f != null) font = TMP_FontAsset.CreateFontAsset(f);
        if (font == null) font = TMP_Settings.defaultFontAsset;
        numFont = TMP_Settings.defaultFontAsset != null ? TMP_Settings.defaultFontAsset : font;
        Build();
        sfx = gameObject.AddComponent<AudioSource>();
        sfx.playOnAwake = false;
        takeClip = MakeTake();
        group.alpha = 0f; group.blocksRaycasts = false; group.interactable = false;
        canvas.enabled = false;
    }

    void OnDestroy() { if (Instance == this) { Instance = null; IsOpen = false; } }

    // ---------------------------------------------------------------- helpers

    static Sprite Spr(string path) { return Resources.Load<Sprite>(path); }

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

    static Image Img(Transform parent, string name, Sprite s, float x, float y, float w, float h, bool aspect = false, bool raycast = false)
    {
        RectTransform rt = NewRect(name, parent);
        Place(rt, x, y, w, h);
        Image im = rt.gameObject.AddComponent<Image>();
        im.sprite = s; im.preserveAspect = aspect; im.raycastTarget = raycast;
        return im;
    }

    TextMeshProUGUI Txt(Transform parent, string name, string text, float x, float y, float w, float h, float size, Color c, TextAlignmentOptions align, bool numbers = false)
    {
        RectTransform rt = NewRect(name, parent);
        Place(rt, x, y, w, h);
        TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.font = numbers ? numFont : font;
        if (numbers) t.fontStyle = FontStyles.Bold;
        t.text = text; t.fontSize = size; t.color = c; t.alignment = align; t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.Normal;
        return t;
    }

    static ColorBlock Tints()
    {
        ColorBlock cb = ColorBlock.defaultColorBlock;
        cb.normalColor = new Color(0.9f, 0.9f, 0.9f);
        cb.highlightedColor = Color.white;
        cb.pressedColor = new Color(0.7f, 0.7f, 0.7f);
        cb.selectedColor = new Color(0.9f, 0.9f, 0.9f);
        cb.fadeDuration = 0.08f;
        return cb;
    }

    // ---------------------------------------------------------------- build

    void Build()
    {
        GameObject cgo = new GameObject("ChestCanvas", typeof(RectTransform));
        cgo.transform.SetParent(transform, false);
        canvas = cgo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 121;
        CanvasScaler cs = cgo.AddComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920f, 1080f);
        cs.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        cgo.AddComponent<GraphicRaycaster>();
        group = cgo.AddComponent<CanvasGroup>();

        RectTransform dimRt = NewRect("Dim", cgo.transform);
        dimRt.anchorMin = Vector2.zero; dimRt.anchorMax = Vector2.one; dimRt.offsetMin = dimRt.offsetMax = Vector2.zero;
        Image dim = dimRt.gameObject.AddComponent<Image>();
        dim.color = new Color(0.02f, 0.015f, 0.01f, 0.55f);

        window = NewRect("Window", cgo.transform);
        window.anchorMin = window.anchorMax = window.pivot = new Vector2(0.5f, 0.5f);
        window.sizeDelta = new Vector2(1920f, 1080f);

        Image panel = Img(window, "ChestPanel", Spr("WorkshopUI/grid_panel"), 510f, 236f, 900f, 600f);
        Transform pt = panel.transform;
        Txt(pt, "Title", "TREASURE CHEST", 0f, 26f, 900f, 60f, 48f, Gold, TextAlignmentOptions.Center);
        Txt(pt, "Subtitle", "Click an item to take it", 0f, 84f, 900f, 30f, 22f, Parchment * 0.85f, TextAlignmentOptions.Center);

        const float sw = 160f, sh = 160f, gap = 22f;
        float x0 = (900f - (sw * 4f + gap * 3f)) * 0.5f;
        for (int i = 0; i < 8; i++)
        {
            int idx = i;
            SlotUI s = new SlotUI();
            s.frame = Img(pt, "LootSlot_" + i, Spr("WorkshopUI/card_normal"), x0 + (i % 4) * (sw + gap), 128f + (i / 4) * (sh + gap), sw, sh, false, true);
            s.icon = Img(s.frame.transform, "Icon", null, 26f, 14f, sw - 52f, 92f, true);
            s.name = Txt(s.frame.transform, "Name", "", 6f, 108f, sw - 12f, 44f, 20f, Parchment, TextAlignmentOptions.Center);
            s.count = Txt(s.frame.transform, "Count", "", sw - 70f, 72f, 58f, 34f, 26f, Color.white, TextAlignmentOptions.Right, true);
            s.count.outlineWidth = 0.25f; s.count.outlineColor = new Color32(10, 6, 3, 255);
            s.button = s.frame.gameObject.AddComponent<Button>();
            s.button.colors = Tints();
            s.button.onClick.AddListener(() => Take(idx));
            EventTrigger et = s.frame.gameObject.AddComponent<EventTrigger>();
            AddTrigger(et, EventTriggerType.PointerEnter, () => { s.hover = true; Refresh(); });
            AddTrigger(et, EventTriggerType.PointerExit, () => { s.hover = false; Refresh(); });
            slots.Add(s);
        }
        emptyText = Txt(pt, "Empty", "The chest is empty.", 0f, 270f, 900f, 60f, 32f, Parchment * 0.8f, TextAlignmentOptions.Center);

        Image takeAll = Img(pt, "TakeAllButton", Spr("WorkshopUI/tab_selected"), 330f, 506f, 240f, 70f, false, true);
        Txt(takeAll.transform, "Label", "TAKE ALL", 0f, 0f, 240f, 70f, 30f, new Color(1f, 0.93f, 0.78f), TextAlignmentOptions.Center);
        Button tb = takeAll.gameObject.AddComponent<Button>();
        tb.colors = Tints();
        tb.onClick.AddListener(TakeAll);

        Image close = Img(window, "CloseButton", Spr("WorkshopUI/close"), 1360f, 200f, 84f, 84f, true, true);
        Button cb = close.gameObject.AddComponent<Button>();
        cb.colors = Tints();
        cb.onClick.AddListener(DoClose);

        hint = Txt(window, "Hint", "[E] / [Esc]  close", 510f, 846f, 900f, 34f, 22f, Parchment * 0.8f, TextAlignmentOptions.Center);

        if (EventSystem.current == null && Object.FindAnyObjectByType<EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }
    }

    static void AddTrigger(EventTrigger et, EventTriggerType type, System.Action a)
    {
        EventTrigger.Entry e = new EventTrigger.Entry { eventID = type };
        e.callback.AddListener(_ => a());
        et.triggers.Add(e);
    }

    // ---------------------------------------------------------------- open / close

    void DoOpen(MapChest c)
    {
        chest = c;
        if (IsOpen) { Refresh(); return; }
        IsOpen = true;
        openedFrame = Time.frameCount;
        canvas.enabled = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SetLook(false);
        foreach (SlotUI s in slots) s.hover = false;
        Refresh();
    }

    void DoClose()
    {
        if (!IsOpen) return;
        IsOpen = false;
        ClosedFrame = Time.frameCount;
        chest = null;
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

    void Refresh()
    {
        List<ChestLoot> items = chest != null ? chest.Loot : null;
        int n = items != null ? items.Count : 0;
        for (int i = 0; i < slots.Count; i++)
        {
            SlotUI s = slots[i];
            bool has = i < n;
            s.button.interactable = has;
            s.icon.enabled = has;
            s.frame.color = has ? Color.white : new Color(1f, 1f, 1f, 0.35f);
            s.frame.sprite = Spr(has && s.hover ? "WorkshopUI/card_selected" : "WorkshopUI/card_normal");
            if (!has) { s.name.text = ""; s.count.text = ""; continue; }
            ChestLoot l = items[i];
            s.icon.sprite = l.Icon();
            s.name.text = l.DisplayName();
            s.name.color = l.kind == ChestLoot.Kind.Map ? Gold : (l.kind == ChestLoot.Kind.Crafted ? new Color(1f, 0.75f, 0.55f) : Parchment);
            s.count.text = l.amount > 1 ? "x" + l.amount : "";
        }
        emptyText.enabled = n == 0;
    }

    void Take(int index)
    {
        if (chest == null) return;
        if (chest.Take(index)) Play(1f);
        Refresh();
    }

    void TakeAll()
    {
        if (chest == null) return;
        int got = 0;
        while (chest.Loot.Count > 0 && got < 20) { if (!chest.Take(0)) break; got++; }
        if (got > 0) Play(0.85f);
        Refresh();
        if (chest.Loot.Count == 0) DoClose();
    }

    void Play(float pitch)
    {
        if (sfx == null || takeClip == null) return;
        sfx.pitch = pitch;
        sfx.PlayOneShot(takeClip, 0.6f);
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        openT = Mathf.MoveTowards(openT, IsOpen ? 1f : 0f, dt / 0.18f);
        float k = openT * openT * (3f - 2f * openT);
        group.alpha = k;
        group.blocksRaycasts = IsOpen;
        group.interactable = IsOpen;
        window.localScale = Vector3.one * Mathf.Lerp(0.94f, 1f, k);
        if (!IsOpen && openT <= 0f && canvas.enabled) canvas.enabled = false;
        if (!IsOpen) return;

        if (chest == null) { DoClose(); return; }
        Keyboard kb = Keyboard.current;
        if (kb != null && Time.frameCount != openedFrame && (kb.escapeKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame)) { DoClose(); return; }
        if (Cursor.lockState != CursorLockMode.None) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
    }

    /// <summary>Soft coin/clink sound for taking an item.</summary>
    static AudioClip MakeTake()
    {
        const int rate = 44100;
        int n = (int)(rate * 0.3f);
        float[] d = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float v = Mathf.Sin(6.2832f * 1760f * t) * Mathf.Exp(-t * 18f) * 0.5f
                    + Mathf.Sin(6.2832f * 2640f * t) * Mathf.Exp(-t * 22f) * 0.3f
                    + (t > 0.06f ? Mathf.Sin(6.2832f * 2093f * (t - 0.06f)) * Mathf.Exp(-(t - 0.06f) * 16f) * 0.35f : 0f);
            d[i] = Mathf.Clamp(v * 0.7f, -1f, 1f);
        }
        AudioClip c = AudioClip.Create("chestTake", n, 1, rate, false);
        c.SetData(d, 0);
        return c;
    }
}
