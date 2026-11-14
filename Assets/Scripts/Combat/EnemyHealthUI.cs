using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Screen-space health bars over enemies. Created automatically when the game starts.
/// Uses the same gold-framed sprites as the HUD, so it always has the same crisp size on screen,
/// never gets cut by the screen edge and never hides behind leaves.
/// An enemy shows its bar while it is awake (hunting, recently hit or close) and in view.
/// Bar: name + "HP 136/160", gold frame, crimson fill (hot orange pulse when low),
/// a pale "damage chunk" that drains behind the fill and a white flash on every hit.
/// </summary>
public class EnemyHealthBars : MonoBehaviour
{
    static EnemyHealthBars instance;

    class Bar
    {
        public EnemyBase enemy;
        public RectTransform root;
        public CanvasGroup group;
        public Image fill, lag, flash;
        public TextMeshProUGUI nameText, hpText;
        public float appear, pop, lagValue = 1f, lastHealth, height, dist;
        public int shownHp = -999;
    }

    public float viewRange = 24f;
    public float closeRange = 11f;

    const float BarW = 160f;
    const float BarH = 13f;

    readonly Dictionary<EnemyBase, Bar> bars = new Dictionary<EnemyBase, Bar>();
    readonly List<Bar> order = new List<Bar>();
    readonly List<EnemyBase> remove = new List<EnemyBase>();
    RectTransform canvasRect;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (instance != null) return;
        instance = new GameObject("EnemyHealthBars").AddComponent<EnemyHealthBars>();
    }

    void Awake()
    {
        canvasRect = CombatSkin.MakeCanvas(transform, "EnemyBarsCanvas", 5);
    }

    Bar Create(EnemyBase e)
    {
        Bar b = new Bar { enemy = e };

        float top = e.transform.position.y + 1.8f, maxY = float.MinValue;
        foreach (Renderer r in e.GetComponentsInChildren<Renderer>())
        {
            if (r is ParticleSystemRenderer) continue;
            maxY = Mathf.Max(maxY, r.bounds.max.y);
        }
        if (maxY > float.MinValue) top = maxY;
        b.height = top - e.transform.position.y + 0.25f;

        GameObject go = new GameObject("EnemyBar", typeof(RectTransform));
        b.root = (RectTransform)go.transform;
        b.root.SetParent(canvasRect, false);
        b.root.sizeDelta = new Vector2(BarW + 10f, 44f);
        b.root.pivot = new Vector2(0.5f, 0f);
        b.group = go.AddComponent<CanvasGroup>();
        b.group.blocksRaycasts = false;
        b.group.interactable = false;

        // name centred above the bar, HP number inside the bar
        b.nameText = CombatSkin.Text(b.root, e.data.displayName, 15f, CombatSkin.Cream, TextAlignmentOptions.Bottom);
        CombatSkin.Place(b.nameText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, BarH + 12f), new Vector2(BarW + 60f, 20f), new Vector2(0.5f, 0f));
        Image frame = CombatSkin.Frame(b.root, BarW + 10f, BarH + 10f);
        CombatSkin.Place(frame.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(BarW + 10f, BarH + 10f), new Vector2(0.5f, 0f));

        b.lag = CombatSkin.Fill(frame.rectTransform, BarW, BarH, CombatSkin.Chunk);
        b.fill = CombatSkin.Fill(frame.rectTransform, BarW, BarH, CombatSkin.Crimson);
        b.flash = CombatSkin.Fill(frame.rectTransform, BarW, BarH, new Color(1f, 1f, 1f, 0f));
        b.hpText = CombatSkin.Text(frame.rectTransform, "", 11f, Color.white, TextAlignmentOptions.Center);
        CombatSkin.Place(b.hpText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(BarW, BarH + 6f), new Vector2(0.5f, 0.5f));

        b.lagValue = Mathf.Clamp01(e.Health / e.data.maxHealth);
        b.lastHealth = e.Health;
        b.group.alpha = 0f;
        return b;
    }

    void LateUpdate()
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        ToolController tc = ToolController.Instance;
        Vector3 viewer = tc != null ? tc.transform.position : cam.transform.position;
        float dt = Time.unscaledDeltaTime;

        for (int i = 0; i < EnemyBase.All.Count; i++)
        {
            EnemyBase e = EnemyBase.All[i];
            if (e == null || e.IsDead || e.data == null || e.data.isBoss || bars.ContainsKey(e)) continue;   // bosses: BossBar
            bars[e] = Create(e);
        }

        Rect area = canvasRect.rect;
        remove.Clear();
        order.Clear();
        foreach (KeyValuePair<EnemyBase, Bar> kv in bars)
        {
            EnemyBase e = kv.Key;
            Bar b = kv.Value;
            if (e == null || e.IsDead) { remove.Add(e); continue; }

            b.dist = Vector3.Distance(viewer, e.transform.position);
            bool awake = e.IsHuntingPlayer || e.TimeSinceHit < 6f || b.dist < closeRange || e.data.isBoss;
            bool want = awake && b.dist < viewRange;

            Vector3 head = e.transform.position + Vector3.up * b.height;
            Vector3 sp = cam.WorldToScreenPoint(head);
            if (sp.z <= 0.1f) want = false;
            if (e == EnemyTargetFrame.Current && EnemyTargetFrame.Visible) want = false;   // already shown big at the top
            if (want && (!AimAssist.InSight(e) || Blocked(cam.transform.position, head, e))) want = false;   // never through walls

            b.appear = Mathf.MoveTowards(b.appear, want ? 1f : 0f, dt * (want ? 6f : 3f));
            b.group.alpha = b.appear;
            bool on = b.appear > 0.001f;
            if (b.root.gameObject.activeSelf != on) b.root.gameObject.SetActive(on);
            if (!on) continue;

            float frac = Mathf.Clamp01(e.Health / e.data.maxHealth);
            if (e.Health < b.lastHealth - 0.01f) b.pop = 1f;
            b.lastHealth = e.Health;
            b.pop = Mathf.MoveTowards(b.pop, 0f, dt * 3.5f);
            if (e.TimeSinceHit > 0.45f) b.lagValue = Mathf.MoveTowards(b.lagValue, frac, dt * 0.5f);
            if (b.lagValue < frac) b.lagValue = frac;

            b.fill.fillAmount = frac;
            b.lag.fillAmount = b.lagValue;
            b.flash.fillAmount = frac;
            b.flash.color = new Color(1f, 1f, 1f, b.pop * 0.85f);
            b.fill.color = CombatSkin.HealthColor(frac);

            int hp = Mathf.CeilToInt(Mathf.Max(0f, e.Health));
            if (hp != b.shownHp)
            {
                b.shownHp = hp;
                b.hpText.text = hp + " / " + Mathf.CeilToInt(e.data.maxHealth);
            }

            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, sp, null, out local);
            float halfW = (BarW + 10f) * 0.5f;
            // off the side of the screen, or under a HUD corner panel: fade out instead of piling onto the HUD
            if (local.x < area.xMin + halfW + 16f || local.x > area.xMax - halfW - 16f) want = false;
            if (local.x > area.xMax - 260f - halfW && local.y > area.yMax - 520f) want = false;   // resources panel (top right)
            if (local.x < area.xMin + 470f + halfW && local.y > area.yMax - 200f) want = false;   // health panel (top left)
            if (!want) b.appear = Mathf.MoveTowards(b.appear, 0f, dt * 6f);
            b.group.alpha = b.appear;
            local.x = Mathf.Clamp(local.x, area.xMin + halfW + 16f, area.xMax - halfW - 16f);
            local.y = Mathf.Clamp(local.y, area.yMin + 120f, area.yMax - 175f);   // stay under the HUD and target frame
            b.root.anchoredPosition = local + new Vector2(0f, 8f * (1f - b.appear));

            float s = Mathf.Lerp(1.05f, 0.72f, Mathf.Clamp01(b.dist / viewRange)) * (1f + 0.1f * b.pop);
            if (e.data.isBoss) s *= 1.25f;
            b.root.localScale = Vector3.one * s;
            order.Add(b);
        }

        // nearer enemies draw on top
        order.Sort((a, c) => c.dist.CompareTo(a.dist));
        for (int i = 0; i < order.Count; i++) order[i].root.SetSiblingIndex(i);

        foreach (EnemyBase e in remove)
        {
            Bar b = bars[e];
            if (b.root != null) Destroy(b.root.gameObject);
            bars.Remove(e);
        }
    }

    /// <summary>True when a wall (not the player, not the enemy itself) is between the camera and the point.</summary>
    static bool Blocked(Vector3 from, Vector3 to, EnemyBase self)
    {
        RaycastHit hit;
        Vector3 d = to - from;
        if (!Physics.Raycast(from, d.normalized, out hit, d.magnitude, ~(1 << 8), QueryTriggerInteraction.Ignore)) return false;
        return hit.collider.GetComponentInParent<EnemyBase>() != self;
    }
}

/// <summary>Shared look for the combat UI: the HUD's gold-framed sprites, colours and text style.</summary>
public static class CombatSkin
{
    public static readonly Color Cream = new Color(0.95f, 0.89f, 0.74f);
    public static readonly Color Gold = new Color(0.93f, 0.76f, 0.38f);
    public static readonly Color Crimson = new Color(0.78f, 0.1f, 0.09f);
    public static readonly Color Chunk = new Color(0.98f, 0.86f, 0.55f);

    static Sprite panel, barBg, barFill;

    static void Load()
    {
        if (panel == null) panel = Resources.Load<Sprite>("Combat/UI_Panel");
        if (barBg == null) barBg = Resources.Load<Sprite>("Combat/UI_BarBg");
        if (barFill == null) barFill = Resources.Load<Sprite>("Combat/UI_BarFill");
    }

    public static Color HealthColor(float frac)
    {
        if (frac >= 0.3f) return Crimson;
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 9f);
        return Color.Lerp(new Color(0.92f, 0.28f, 0.08f), new Color(1f, 0.58f, 0.16f), pulse);
    }

    public static RectTransform MakeCanvas(Transform parent, string name, int order)
    {
        GameObject cgo = new GameObject(name);
        cgo.transform.SetParent(parent, false);
        Canvas c = cgo.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = order;
        CanvasScaler sc = cgo.AddComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920f, 1080f);
        sc.matchWidthOrHeight = 0.5f;
        return (RectTransform)cgo.transform;
    }

    public static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size, Vector2 pivot)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    public static TextMeshProUGUI Text(Transform parent, string text, float size, Color color, TextAlignmentOptions align)
    {
        GameObject go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.fontStyle = FontStyles.Bold;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        t.raycastTarget = false;
        t.outlineWidth = 0.22f;
        t.outlineColor = new Color32(12, 8, 4, 255);
        return t;
    }

    /// <summary>The HUD bar background: dark inside, gold rim.</summary>
    public static Image Frame(Transform parent, float w, float h)
    {
        Load();
        GameObject go = new GameObject("Frame", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Image img = go.AddComponent<Image>();
        img.sprite = barBg;
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 2.6f;
        img.raycastTarget = false;
        if (barBg == null) img.color = new Color(0.08f, 0.06f, 0.05f, 0.9f);
        ((RectTransform)go.transform).sizeDelta = new Vector2(w, h);
        return img;
    }

    /// <summary>A horizontally filled bar centred in its parent.</summary>
    public static Image Fill(RectTransform parent, float w, float h, Color color)
    {
        Load();
        GameObject go = new GameObject("Fill", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Image img = go.AddComponent<Image>();
        img.sprite = barFill;
        img.type = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Horizontal;
        img.fillOrigin = 0;
        img.fillAmount = 1f;
        img.color = color;
        img.raycastTarget = false;
        Place((RectTransform)go.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(w, h), new Vector2(0.5f, 0.5f));
        return img;
    }

    /// <summary>The HUD panel (dark, gold border, rounded).</summary>
    public static Image Panel(Transform parent)
    {
        Load();
        GameObject go = new GameObject("Panel", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Image img = go.AddComponent<Image>();
        img.sprite = panel;
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 1.4f;
        img.raycastTarget = false;
        if (panel == null) img.color = new Color(0.06f, 0.05f, 0.04f, 0.85f);
        return img;
    }
}
