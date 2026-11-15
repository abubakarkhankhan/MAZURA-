using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Small helpers to build the menu screens in the MAZURA style (wood + gold sprites).</summary>
public static class MenuKit
{
    public static Sprite S(string n) { return Resources.Load<Sprite>("InventoryUI/" + n); }
    public static readonly Color Cream = new Color(1f, 0.92f, 0.75f);
    public static readonly Color Gold = new Color(1f, 0.8f, 0.38f);

    public static Canvas MakeCanvas(Transform parent, string name, int order)
    {
        GameObject cgo = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        cgo.transform.SetParent(parent, false);
        Canvas c = cgo.GetComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder = order;
        CanvasScaler cs = cgo.GetComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; cs.referenceResolution = new Vector2(1920f, 1080f);
        cs.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        return c;
    }

    public static void EnsureEventSystem()
    {
        if (EventSystem.current != null || Object.FindAnyObjectByType<EventSystem>() != null) return;
        GameObject es = new GameObject("EventSystem", typeof(EventSystem));
        es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
    }

    public static RectTransform Rect(string n, Transform p, float x, float y, float w, float h)
    {
        RectTransform r = (RectTransform)new GameObject(n, typeof(RectTransform)).transform;
        r.SetParent(p, false);
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = new Vector2(x, y); r.sizeDelta = new Vector2(w, h);
        return r;
    }

    public static Image Img(string n, Transform p, Sprite s, float x, float y, float w, float h)
    {
        Image i = Rect(n, p, x, y, w, h).gameObject.AddComponent<Image>();
        i.sprite = s; i.raycastTarget = false;
        return i;
    }

    public static Image FullScreen(string n, Transform p, Color c)
    {
        Image i = Rect(n, p, 0f, 0f, 0f, 0f).gameObject.AddComponent<Image>();
        i.rectTransform.anchorMin = Vector2.zero; i.rectTransform.anchorMax = Vector2.one;
        i.color = c;
        return i;
    }

    public static TextMeshProUGUI Txt(string n, Transform p, string t, float x, float y, float w, float h, float size, Color c)
    {
        TextMeshProUGUI tm = Rect(n, p, x, y, w, h).gameObject.AddComponent<TextMeshProUGUI>();
        TMP_FontAsset f = UIFont.Get(); if (f != null) tm.font = f;
        tm.text = t; tm.fontSize = size; tm.color = c; tm.alignment = TextAlignmentOptions.Center; tm.raycastTarget = false;
        return tm;
    }

    public static Button Btn(string label, Transform p, float x, float y, float w, System.Action onClick, string art = "use")
    {
        RectTransform r = Rect("Btn_" + label, p, x, y, w, w * 0.47f);
        Image img = r.gameObject.AddComponent<Image>();
        img.sprite = S(art + "_normal"); img.preserveAspect = true;
        Button b = r.gameObject.AddComponent<Button>();
        SpriteState ss = new SpriteState();
        ss.highlightedSprite = S(art + "_hover"); ss.pressedSprite = S(art + "_hover"); ss.selectedSprite = S(art + "_normal"); ss.disabledSprite = S(art + "_disabled");
        b.transition = Selectable.Transition.SpriteSwap; b.spriteState = ss;
        TextMeshProUGUI t = Txt("T", r, label, 0f, 2f, w * 0.62f, w * 0.2f, w * 0.1f, Cream);
        t.fontStyle = FontStyles.Bold; t.outlineWidth = 0.2f; t.outlineColor = new Color32(20, 10, 4, 255); t.enableWordWrapping = false;
        t.enableAutoSizing = true; t.fontSizeMax = w * 0.1f; t.fontSizeMin = 12f;
        b.onClick.AddListener(() => { if (onClick != null) onClick(); });
        return b;
    }

    public static void SetLabel(Button b, string text) { TextMeshProUGUI t = b.GetComponentInChildren<TextMeshProUGUI>(); if (t != null) t.text = text; }

    /// <summary>A labelled slider (0..1) with wood look.</summary>
    public static Slider SliderRow(string label, Transform p, float x, float y, float value, System.Action<float> onChange)
    {
        RectTransform row = Rect("Row_" + label, p, x, y, 620f, 56f);
        TextMeshProUGUI l = Txt("L", row, label, -170f, 0f, 260f, 50f, 26f, Cream);
        l.alignment = TextAlignmentOptions.MidlineLeft;
        RectTransform sr = Rect("Slider", row, 150f, 0f, 320f, 26f);
        Slider s = sr.gameObject.AddComponent<Slider>();
        Image bg = Img("Bg", sr, null, 0f, 0f, 0f, 0f); bg.rectTransform.anchorMin = Vector2.zero; bg.rectTransform.anchorMax = Vector2.one; bg.rectTransform.sizeDelta = Vector2.zero;
        bg.color = new Color(0.08f, 0.05f, 0.03f, 0.95f);
        RectTransform fa = Rect("FillArea", sr, 0f, 0f, 0f, 0f); fa.anchorMin = new Vector2(0f, 0.25f); fa.anchorMax = new Vector2(1f, 0.75f); fa.sizeDelta = new Vector2(-12f, 0f);
        Image fill = Img("Fill", fa, null, 0f, 0f, 0f, 0f); fill.color = Gold; fill.rectTransform.sizeDelta = Vector2.zero;
        RectTransform ha = Rect("HandleArea", sr, 0f, 0f, 0f, 0f); ha.anchorMin = Vector2.zero; ha.anchorMax = Vector2.one; ha.sizeDelta = new Vector2(-24f, 0f);
        Image handle = Img("Handle", ha, S("quantity_badge"), 0f, 0f, 34f, 34f); handle.raycastTarget = true;
        s.fillRect = fill.rectTransform; s.handleRect = handle.rectTransform; s.targetGraphic = handle;
        s.minValue = 0f; s.maxValue = 1f; s.value = value;
        bg.raycastTarget = true;
        s.onValueChanged.AddListener(v => { if (onChange != null) onChange(v); });
        return s;
    }
}
