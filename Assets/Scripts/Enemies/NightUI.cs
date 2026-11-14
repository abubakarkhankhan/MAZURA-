using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Night/dawn presentation and danger warnings. Created automatically when the game starts.
///   - NightUI.Banner("THE NIGHT HAS COME", "Survive until dawn", true): a cinematic banner across the screen
///     (dark band, wide-spaced title, gold rules that grow out from the centre, a sound sting), then it fades.
///   - Danger indicator: a red chevron at the edge of the screen for every enemy that is hunting you
///     but is not on screen, pointing to where it is. Closer = bigger and faster pulse.
/// </summary>
public class NightUI : MonoBehaviour
{
    static NightUI instance;

    // banner
    RectTransform bannerRoot, ruleTop, ruleBottom;
    CanvasGroup bannerGroup;
    TextMeshProUGUI title, subtitle;
    float bannerT = -1f;
    const float FadeIn = 0.7f, Hold = 2.8f, FadeOut = 1.2f;
    AudioClip nightSting, dawnSting;
    AudioSource audioSource;

    // threat arrows
    RectTransform threatLayer;
    readonly List<Image> arrows = new List<Image>();
    Sprite arrowSprite;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (instance != null) return;
        instance = new GameObject("NightUI").AddComponent<NightUI>();
    }

    /// <summary>Shows the big banner. night = red title and the low sting, otherwise gold and the dawn chime.</summary>
    public static void Banner(string text, string sub, bool night)
    {
        if (instance == null) Boot();
        instance.ShowBanner(text, sub, night);
    }

    void Awake()
    {
        RectTransform canvas = CombatSkin.MakeCanvas(transform, "NightCanvas", 20);

        // ---- threat arrows (under the banner)
        GameObject tl = new GameObject("Threats", typeof(RectTransform));
        threatLayer = (RectTransform)tl.transform;
        threatLayer.SetParent(canvas, false);
        threatLayer.anchorMin = Vector2.zero; threatLayer.anchorMax = Vector2.one;
        threatLayer.offsetMin = threatLayer.offsetMax = Vector2.zero;
        arrowSprite = MakeChevron();

        // ---- banner
        GameObject b = new GameObject("Banner", typeof(RectTransform));
        bannerRoot = (RectTransform)b.transform;
        bannerRoot.SetParent(canvas, false);
        bannerRoot.anchorMin = new Vector2(0f, 0.5f); bannerRoot.anchorMax = new Vector2(1f, 0.5f);
        bannerRoot.pivot = new Vector2(0.5f, 0.5f);
        bannerRoot.anchoredPosition = new Vector2(0f, 120f);
        bannerRoot.sizeDelta = new Vector2(0f, 240f);
        bannerGroup = b.AddComponent<CanvasGroup>();
        bannerGroup.alpha = 0f;
        bannerGroup.blocksRaycasts = false;

        GameObject band = new GameObject("Band", typeof(RectTransform));
        band.transform.SetParent(bannerRoot, false);
        RawImage bandImg = band.AddComponent<RawImage>();
        bandImg.texture = MakeBand();
        bandImg.raycastTarget = false;
        RectTransform brt = (RectTransform)band.transform;
        brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one; brt.offsetMin = brt.offsetMax = Vector2.zero;

        title = CombatSkin.Text(bannerRoot, "", 64f, CombatSkin.Gold, TextAlignmentOptions.Center);
        CombatSkin.Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 14f), new Vector2(1600f, 90f), new Vector2(0.5f, 0.5f));
        title.outlineWidth = 0.18f;

        subtitle = CombatSkin.Text(bannerRoot, "", 21f, CombatSkin.Cream, TextAlignmentOptions.Center);
        subtitle.characterSpacing = 8f;
        subtitle.fontStyle = FontStyles.Italic;
        CombatSkin.Place(subtitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -52f), new Vector2(1200f, 34f), new Vector2(0.5f, 0.5f));

        ruleTop = Rule("RuleTop", 64f);
        ruleBottom = Rule("RuleBottom", -30f);

        // ---- sound
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        nightSting = MakeNightSting();
        dawnSting = MakeDawnSting();
    }

    RectTransform Rule(string name, float y)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(bannerRoot, false);
        Image img = go.AddComponent<Image>();
        img.color = CombatSkin.Gold;
        img.raycastTarget = false;
        RectTransform rt = (RectTransform)go.transform;
        CombatSkin.Place(rt, new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(560f, 2f), new Vector2(0.5f, 0.5f));
        return rt;
    }

    void ShowBanner(string text, string sub, bool night)
    {
        title.text = text;
        subtitle.text = sub;
        Color c = night ? new Color(0.86f, 0.2f, 0.14f) : new Color(1f, 0.84f, 0.5f);
        title.color = c;
        Color rc = night ? new Color(0.75f, 0.22f, 0.15f) : CombatSkin.Gold;
        ruleTop.GetComponent<Image>().color = rc;
        ruleBottom.GetComponent<Image>().color = rc;
        bannerT = 0f;
        audioSource.PlayOneShot(night ? nightSting : dawnSting, 0.9f);
    }

    void LateUpdate()
    {
        float dt = Time.unscaledDeltaTime;
        UpdateBanner(dt);
        UpdateThreats();
    }

    void UpdateBanner(float dt)
    {
        if (bannerT < 0f) return;
        bannerT += dt;
        float a;
        if (bannerT < FadeIn) a = bannerT / FadeIn;
        else if (bannerT < FadeIn + Hold) a = 1f;
        else a = 1f - (bannerT - FadeIn - Hold) / FadeOut;
        a = Mathf.Clamp01(a);
        bannerGroup.alpha = a * a * (3f - 2f * a);

        float k = Mathf.Clamp01(bannerT / (FadeIn + 0.6f));
        float ease = 1f - (1f - k) * (1f - k) * (1f - k);
        title.characterSpacing = Mathf.Lerp(34f, 16f, ease);
        title.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.12f, 1f, ease);
        float w = Mathf.Lerp(0f, 620f, ease);
        ruleTop.sizeDelta = new Vector2(w, 2f);
        ruleBottom.sizeDelta = new Vector2(w * 0.7f, 2f);

        if (bannerT > FadeIn + Hold + FadeOut) { bannerT = -1f; bannerGroup.alpha = 0f; }
    }

    // ------------------------------------------------------------------ threats

    void UpdateThreats()
    {
        Camera cam = Camera.main;
        ToolController tc = ToolController.Instance;
        int used = 0;
        if (cam != null && tc != null)
        {
            Rect area = threatLayer.rect;
            float rx = area.width * 0.44f, ry = area.height * 0.40f;
            foreach (EnemyBase e in EnemyBase.All)
            {
                if (used >= 6) break;
                if (e == null || e.IsDead || !e.IsHuntingPlayer) continue;
                Vector3 world = e.transform.position + Vector3.up * 1.2f;
                Vector3 sp = cam.WorldToViewportPoint(world);
                bool onScreen = sp.z > 0f && sp.x > 0.04f && sp.x < 0.96f && sp.y > 0.06f && sp.y < 0.94f;
                if (onScreen && AimAssist.InSight(e)) continue;   // you can see it: its health bar is enough

                // direction on screen (behind the camera = flip)
                Vector2 d = new Vector2(sp.x - 0.5f, sp.y - 0.5f);
                if (sp.z < 0f) d = -d;
                if (onScreen) d = new Vector2(sp.x - 0.5f, sp.y - 0.5f);   // on screen but behind a wall: point at it from the centre
                if (d.sqrMagnitude < 0.0001f) d = Vector2.down;
                d.Normalize();

                Image img = GetArrow(used++);
                RectTransform rt = img.rectTransform;
                rt.anchoredPosition = new Vector2(d.x * rx, d.y * ry);
                rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg - 90f);

                float dist = Vector3.Distance(tc.transform.position, e.transform.position);
                float near = 1f - Mathf.Clamp01((dist - 3f) / 22f);
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.Lerp(4f, 11f, near));
                rt.localScale = Vector3.one * Mathf.Lerp(0.75f, 1.25f, near) * (0.92f + 0.08f * pulse);
                img.color = new Color(0.95f, 0.18f, 0.12f, Mathf.Lerp(0.45f, 0.95f, near) * (0.7f + 0.3f * pulse));
            }
        }
        for (int i = used; i < arrows.Count; i++) if (arrows[i].enabled) arrows[i].enabled = false;
    }

    Image GetArrow(int i)
    {
        while (arrows.Count <= i)
        {
            GameObject go = new GameObject("Threat", typeof(RectTransform));
            go.transform.SetParent(threatLayer, false);
            Image img = go.AddComponent<Image>();
            img.sprite = arrowSprite;
            img.raycastTarget = false;
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(72f, 54f);
            arrows.Add(img);
        }
        arrows[i].enabled = true;
        return arrows[i];
    }

    // ------------------------------------------------------------------ generated art and sound

    /// <summary>Dark band: strongest in the middle, fading to nothing at the top/bottom and the sides.</summary>
    static Texture2D MakeBand()
    {
        const int w = 256, h = 64;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float v = 1f - Mathf.Abs(y / (h - 1f) * 2f - 1f);
                float u = 1f - Mathf.Abs(x / (w - 1f) * 2f - 1f);
                float a = Mathf.SmoothStep(0f, 1f, v) * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(u * 2.2f));
                tex.SetPixel(x, y, new Color(0.02f, 0.01f, 0.01f, a * 0.78f));
            }
        tex.Apply();
        return tex;
    }

    /// <summary>A chevron pointing up (rotated to face the threat).</summary>
    static Sprite MakeChevron()
    {
        const int w = 108, h = 80;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = (x / (w - 1f)) * 2f - 1f;          // -1..1
                float v = y / (h - 1f);                        // 0 bottom .. 1 top
                float spine = 0.92f - Mathf.Abs(u) * 0.62f;    // the V shape, tip at the top
                float d = Mathf.Abs(v - spine);
                float a = Mathf.Clamp01(1f - d / 0.19f);
                if (Mathf.Abs(u) > 0.96f) a = 0f;
                a = a * a * (3f - 2f * a);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
    }

    static AudioClip MakeNightSting()
    {
        const int rate = 44100;
        float seconds = 3.2f;
        int n = (int)(rate * seconds);
        float[] d = new float[n];
        System.Random rnd = new System.Random(21);
        float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float noise = (float)(rnd.NextDouble() * 2.0 - 1.0);
            lp += (noise - lp) * 0.015f;
            float env = Mathf.Min(1f, t / 0.05f) * Mathf.Exp(-t * 0.9f);
            float boom = Mathf.Sin(6.2832f * (48f - 10f * t / seconds) * t) * Mathf.Exp(-t * 1.6f);
            float drone = (Mathf.Sin(6.2832f * 73.4f * t) + 0.6f * Mathf.Sin(6.2832f * 77.8f * t) + 0.3f * Mathf.Sin(6.2832f * 110f * t)) * 0.35f;
            d[i] = Mathf.Clamp(boom * 0.9f + drone * env * 0.7f + lp * 6f * env, -1f, 1f);
        }
        AudioClip c = AudioClip.Create("nightSting", n, 1, rate, false);
        c.SetData(d, 0);
        return c;
    }

    static AudioClip MakeDawnSting()
    {
        const int rate = 44100;
        float seconds = 3.0f;
        int n = (int)(rate * seconds);
        float[] d = new float[n];
        float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f };   // C major, rising
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float v = 0f;
            for (int k = 0; k < notes.Length; k++)
            {
                float start = k * 0.22f;
                if (t < start) continue;
                float tt = t - start;
                v += Mathf.Sin(6.2832f * notes[k] * tt) * Mathf.Exp(-tt * 1.4f) * Mathf.Min(1f, tt / 0.02f) * 0.22f;
                v += Mathf.Sin(6.2832f * notes[k] * 2f * tt) * Mathf.Exp(-tt * 2.5f) * 0.05f;
            }
            d[i] = Mathf.Clamp(v, -1f, 1f);
        }
        AudioClip c = AudioClip.Create("dawnSting", n, 1, rate, false);
        c.SetData(d, 0);
        return c;
    }
}
