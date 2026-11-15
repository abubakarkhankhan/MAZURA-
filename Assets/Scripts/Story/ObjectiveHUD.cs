using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Top-centre objective line ("Find the 4 pieces of the ancient map (1/4)") with a direction arrow.</summary>
public class ObjectiveHUD : MonoBehaviour
{
    Canvas canvas;
    TextMeshProUGUI head, text, dist;
    RectTransform arrow;
    Image arrowImg;
    string last;
    float pop;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (Object.FindAnyObjectByType<DungeonZone>() == null) return;
        new GameObject("ObjectiveHUD").AddComponent<ObjectiveHUD>();
    }

    void Awake()
    {
        GameObject cgo = new GameObject("ObjectiveCanvas", typeof(Canvas), typeof(CanvasScaler));
        cgo.transform.SetParent(transform, false);
        canvas = cgo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 8;
        CanvasScaler cs = cgo.GetComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; cs.referenceResolution = new Vector2(1920f, 1080f); cs.matchWidthOrHeight = 0.5f;

        TMP_FontAsset font = UIFont.Get();
        Image div = NewImg(cgo.transform, "Divider", Resources.Load<Sprite>("InventoryUI/decorative_gold_divider"), 26f, -340f, 430f, 20f);
        div.preserveAspect = true; div.color = new Color(1f, 1f, 1f, 0.85f);
        head = NewTxt(cgo.transform, "Head", "OBJECTIVE", 30f, -292f, 600f, 26f, 17f, new Color(1f, 0.8f, 0.4f), font);
        head.characterSpacing = 6f; head.fontStyle = FontStyles.Bold;
        text = NewTxt(cgo.transform, "Text", "", 30f, -318f, 560f, 40f, 22f, new Color(1f, 0.93f, 0.78f), font);
        text.fontStyle = FontStyles.Bold;
        dist = NewTxt(cgo.transform, "Dist", "", 70f, -370f, 200f, 30f, 20f, new Color(1f, 0.85f, 0.5f), font);
        dist.alignment = TextAlignmentOptions.MidlineLeft;
        arrowImg = NewImg(cgo.transform, "Arrow", MakeArrow(), 32f, -370f, 30f, 30f);
        arrowImg.color = new Color(1f, 0.8f, 0.35f);
        arrow = arrowImg.rectTransform;
        arrow.pivot = new Vector2(0.5f, 0.5f);
        arrow.anchoredPosition = new Vector2(48f, -370f);
    }

    static Image NewImg(Transform p, string n, Sprite s, float x, float y, float w, float h)
    {
        GameObject go = new GameObject(n, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform r = (RectTransform)go.transform; r.SetParent(p, false);
        r.anchorMin = r.anchorMax = new Vector2(0f, 1f); r.pivot = new Vector2(0f, 0.5f);
        r.anchoredPosition = new Vector2(x, y); r.sizeDelta = new Vector2(w, h);
        Image i = go.GetComponent<Image>(); i.sprite = s; i.raycastTarget = false;
        return i;
    }

    static TextMeshProUGUI NewTxt(Transform p, string n, string t, float x, float y, float w, float h, float size, Color c, TMP_FontAsset font)
    {
        GameObject go = new GameObject(n, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        RectTransform r = (RectTransform)go.transform; r.SetParent(p, false);
        r.anchorMin = r.anchorMax = new Vector2(0f, 1f); r.pivot = new Vector2(0f, 0.5f);
        r.anchoredPosition = new Vector2(x, y); r.sizeDelta = new Vector2(w, h);
        TextMeshProUGUI tm = go.GetComponent<TextMeshProUGUI>();
        if (font != null) tm.font = font;
        tm.text = t; tm.fontSize = size; tm.color = c; tm.alignment = TextAlignmentOptions.MidlineLeft; tm.raycastTarget = false;
        tm.enableWordWrapping = false;
        tm.outlineWidth = 0.22f; tm.outlineColor = new Color32(12, 6, 2, 255);
        return tm;
    }

    static Sprite MakeArrow()
    {
        const int S = 64;
        Texture2D tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                // arrow pointing up: a triangle with a notch
                float u = (x + 0.5f) / S * 2f - 1f, v = (y + 0.5f) / S;
                float half = (1f - v) * 0.85f;
                bool tri = Mathf.Abs(u) < half && v > 0.08f;
                bool notch = v < 0.38f && Mathf.Abs(u) < (0.38f - v) * 1.4f;
                float a = tri && !notch ? 1f : 0f;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f));
    }

    void Update()
    {
        DungeonQuest q = DungeonQuest.Instance;
        bool hide = q == null || StoryScreen.IsOpen || InventoryWindow.IsOpen || WorkshopUI.IsOpen;
        canvas.enabled = !hide;
        if (hide) return;

        string o = q.Objective;
        if (o != last) { last = o; text.text = o; pop = 1f; }
        pop = Mathf.MoveTowards(pop, 0f, Time.unscaledDeltaTime * 2f);
        text.transform.localScale = Vector3.one * (1f + 0.08f * pop);
        text.color = Color.Lerp(new Color(1f, 0.93f, 0.78f), new Color(1f, 0.75f, 0.3f), pop);

        Vector3 target;
        ToolController tc = ToolController.Instance;
        Camera cam = Camera.main;
        bool show = tc != null && cam != null && q.TryGetTarget(out target);
        arrow.gameObject.SetActive(show);
        dist.gameObject.SetActive(show);
        if (!show) return;
        q.TryGetTarget(out target);
        Vector3 d = target - tc.transform.position; d.y = 0f;
        Vector3 f = cam.transform.forward; f.y = 0f;
        float ang = Vector3.SignedAngle(f, d, Vector3.up);
        arrow.localRotation = Quaternion.Euler(0f, 0f, -ang);
        dist.text = Mathf.RoundToInt(d.magnitude) + " m";
    }
}
