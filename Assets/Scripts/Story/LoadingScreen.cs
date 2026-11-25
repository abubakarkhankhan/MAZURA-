using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Full-screen loading screen with a background picture, a wood-and-gold progress bar, a tip and a turning rune.
///   LoadingScreen.Splash()                      - when the game starts (over the main menu)
///   LoadingScreen.LoadScene(scene, kind)        - PLAY / CONTINUE / TRY AGAIN / MAIN MENU (loads the scene in the background)
///   LoadingScreen.Transition(kind, text, act)   - forest &lt;-&gt; dungeon (no scene load; act runs while the screen is black)
/// kind: "menu", "forest", "continue", "dungeon".
/// </summary>
public class LoadingScreen : MonoBehaviour
{
    public static bool IsShowing { get; private set; }
    public static bool BootDone;
    static LoadingScreen inst;

    CanvasGroup group;
    Image bg, fill;
    TextMeshProUGUI subtitle, percent, tip;
    RectTransform spinner;
    float shown, target;

    static readonly string[] Tips =
    {
        "Chop trees and mine rocks early - every recipe needs wood and stone.",
        "Nights are dangerous. A campfire keeps enemies away and slowly heals you.",
        "The 4 map pieces are hidden in chests across the Dark Forest.",
        "Press Q to switch between the weapons you own.",
        "Press F5 or use the pause menu to save your game.",
        "Gem crystals glow in the dark - mine them with the pickaxe.",
        "Dungeon chests fill up again a few minutes after you empty them.",
        "Hold Shift to sprint, but watch your stamina bar.",
        "Right mouse button blocks with a shield.",
        "A Health Potion heals 60 HP at once - keep one for the Dungeon Lord.",
        "Build a Workbench to unlock better tools and weapons.",
        "The Battle Axe hits everything around you and slows enemies down.",
    };

    // ---------------------------------------------------------------- public api
    public static void Splash()
    {
        BootDone = true;
        LoadingScreen ls = Get();
        ls.StopAllCoroutines();
        ls.StartCoroutine(ls.SplashRoutine());
    }

    public static void LoadScene(string scene, string kind)
    {
        LoadingScreen ls = Get();
        ls.StopAllCoroutines();
        ls.StartCoroutine(ls.LoadRoutine(scene, kind));
    }

    public static void Transition(string kind, string text, System.Action middle)
    {
        LoadingScreen ls = Get();
        ls.StopAllCoroutines();
        ls.StartCoroutine(ls.TransitionRoutine(kind, text, middle));
    }

    // ---------------------------------------------------------------- routines
    IEnumerator SplashRoutine()
    {
        Setup("menu", "Loading...");
        group.alpha = 1f;
        yield return Progress(2.2f, 0f, 1f);
        yield return Fade(0f, 0.7f);
        Hide();
    }

    IEnumerator LoadRoutine(string scene, string kind)
    {
        Setup(kind, null);
        yield return Fade(1f, 0.35f);
        AsyncOperation op = SceneManager.LoadSceneAsync(scene);
        op.allowSceneActivation = false;
        float t = 0f;
        const float minTime = 2.4f;
        while (op.progress < 0.9f || t < minTime)
        {
            t += Time.unscaledDeltaTime;
            target = Mathf.Min(op.progress / 0.9f, t / minTime) * 0.92f;
            yield return null;
        }
        op.allowSceneActivation = true;
        while (!op.isDone) yield return null;
        // let the new scene start (and a save file load) before it is shown
        yield return Progress(0.7f, 0.92f, 1f);
        if (!StoryScreen.IsOpen) Time.timeScale = 1f;
        LockForGame();
        yield return Fade(0f, 0.6f);
        Hide();
        LockForGame();
    }

    IEnumerator TransitionRoutine(string kind, string text, System.Action middle)
    {
        Setup(kind, text);
        float oldScale = Time.timeScale;
        yield return Fade(1f, 0.4f);
        Time.timeScale = 0f;
        if (middle != null)
        {
            try { middle(); } catch (System.Exception e) { Debug.LogException(e); }
        }
        yield return Progress(1.8f, 0f, 1f);
        Time.timeScale = oldScale <= 0f ? 1f : oldScale;
        LockForGame();
        yield return Fade(0f, 0.5f);
        Hide();
        LockForGame();
    }

    IEnumerator Progress(float seconds, float from, float to)
    {
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            target = Mathf.Lerp(from, to, t / seconds);
            yield return null;
        }
        target = to; shown = Mathf.Max(shown, to - 0.02f);
        yield return null;
    }

    IEnumerator Fade(float to, float seconds)
    {
        float from = group.alpha;
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            group.alpha = Mathf.Lerp(from, to, t / seconds);
            yield return null;
        }
        group.alpha = to;
    }

    /// <summary>In the game the mouse must be locked and hidden (camera look, chests, crosshair) - the menu had left it free.</summary>
    static void LockForGame()
    {
        if (Object.FindAnyObjectByType<ToolController>() == null) return;          // main menu
        if (StoryScreen.IsOpen || PauseMenu.IsOpen) return;                         // they handle the mouse themselves
        HealthManager hm = Object.FindAnyObjectByType<HealthManager>();
        if (hm != null && hm.IsDead) return;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Hide()
    {
        IsShowing = false;
        group.alpha = 0f;
        group.blocksRaycasts = false;
    }

    void Update()
    {
        if (!IsShowing) return;
        shown = Mathf.MoveTowards(shown, target, Time.unscaledDeltaTime * 0.9f);
        if (fill != null) fill.fillAmount = shown;
        if (percent != null) percent.text = Mathf.RoundToInt(shown * 100f) + "%";
        if (spinner != null) spinner.Rotate(0f, 0f, -120f * Time.unscaledDeltaTime);
    }

    // ---------------------------------------------------------------- building
    static LoadingScreen Get()
    {
        if (inst != null) return inst;
        GameObject go = new GameObject("LoadingScreen");
        DontDestroyOnLoad(go);
        inst = go.AddComponent<LoadingScreen>();
        inst.Build();
        return inst;
    }

    void Setup(string kind, string text)
    {
        IsShowing = true;
        group.blocksRaycasts = true;
        shown = 0f; target = 0f;
        string pic = kind == "dungeon" ? "Menu/bg_load_dungeon" : kind == "menu" ? "Menu/bg_main" : "Menu/bg_load_forest";
        Sprite s = Resources.Load<Sprite>(pic);
        if (s != null) { bg.sprite = s; bg.color = Color.white; } else bg.color = Color.black;
        if (text == null)
        {
            if (kind == "dungeon") text = "Descending into the Old Dungeon...";
            else if (kind == "continue") text = "Continuing your journey...";
            else if (kind == "menu") text = "Loading...";
            else text = "Entering the Dark Forest...";
        }
        subtitle.text = text;
        tip.text = "<color=#FFD27A>TIP</color>   " + Tips[Random.Range(0, Tips.Length)];
        MenuKit.EnsureEventSystem();
    }

    void Build()
    {
        Canvas c = MenuKit.MakeCanvas(transform, "LoadingCanvas", 1000);
        group = c.GetComponent<CanvasGroup>();
        if (group == null) group = c.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f; group.blocksRaycasts = false; group.interactable = false;
        Transform root = c.transform;

        MenuKit.FullScreen("Black", root, Color.black).raycastTarget = true;
        bg = MenuKit.FullScreen("Background", root, Color.white);
        bg.preserveAspect = false; bg.raycastTarget = false;
        AspectRatioFitter fit = bg.gameObject.AddComponent<AspectRatioFitter>();
        fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fit.aspectRatio = 16f / 9f;

        // dark band at the bottom for the bar and the tip
        Image band = MenuKit.FullScreen("BottomShade", root, Color.white);
        band.sprite = Gradient(false); band.raycastTarget = false;
        RectTransform br = band.rectTransform;
        br.anchorMin = new Vector2(0f, 0f); br.anchorMax = new Vector2(1f, 0f); br.pivot = new Vector2(0.5f, 0f);
        br.sizeDelta = new Vector2(0f, 420f); br.anchoredPosition = Vector2.zero;
        Image top = MenuKit.FullScreen("TopShade", root, Color.white);
        top.sprite = Gradient(true); top.raycastTarget = false;
        RectTransform tr = top.rectTransform;
        tr.anchorMin = new Vector2(0f, 1f); tr.anchorMax = new Vector2(1f, 1f); tr.pivot = new Vector2(0.5f, 1f);
        tr.sizeDelta = new Vector2(0f, 260f); tr.anchoredPosition = Vector2.zero;

        // title
        TextMeshProUGUI title = MenuKit.Txt("Title", root, "MAZURA", 0f, 440f, 900f, 130f, 96f, new Color(1f, 0.86f, 0.5f));
        title.fontStyle = FontStyles.Bold; title.characterSpacing = 18f;
        title.outlineWidth = 0.18f; title.outlineColor = new Color32(40, 14, 2, 255);
        title.enableVertexGradient = true;
        title.colorGradient = new VertexGradient(new Color(1f, 0.93f, 0.62f), new Color(1f, 0.93f, 0.62f), new Color(0.9f, 0.55f, 0.18f), new Color(0.9f, 0.55f, 0.18f));

        subtitle = MenuKit.Txt("Subtitle", root, "", 0f, -300f, 1400f, 56f, 36f, new Color(1f, 0.9f, 0.7f));
        subtitle.fontStyle = FontStyles.Bold; subtitle.characterSpacing = 4f;
        subtitle.outlineWidth = 0.2f; subtitle.outlineColor = new Color32(15, 6, 2, 255);

        // progress bar: the wood-and-gold plank stretched long (9-sliced), with a golden fill inside
        Sprite frame = Resources.Load<Sprite>("Menu/UI/btn_plank_normal");
        Image fr = MenuKit.Img("BarFrame", root, frame, 0f, -372f, 1120f, 100f);
        fr.preserveAspect = false; fr.raycastTarget = false;
        if (frame != null && frame.border.sqrMagnitude > 0f) fr.type = Image.Type.Sliced;
        fill = MenuKit.Img("BarFill", root, BarSprite(), -2f, -372f, 1120f - 190f, 34f);
        fill.preserveAspect = false; fill.raycastTarget = false;
        fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal; fill.fillOrigin = 0; fill.fillAmount = 0f;
        percent = MenuKit.Txt("Percent", root, "0%", 0f, -370f, 300f, 40f, 26f, Color.white);
        percent.fontStyle = FontStyles.Bold; percent.outlineWidth = 0.25f; percent.outlineColor = new Color32(30, 12, 2, 255);

        tip = MenuKit.Txt("Tip", root, "", 0f, -452f, 1500f, 60f, 25f, MenuKit.Cream);
        tip.enableWordWrapping = true; tip.outlineWidth = 0.2f; tip.outlineColor = new Color32(10, 5, 2, 255);

        Sprite gear = Resources.Load<Sprite>("Menu/UI/icon_settings");
        if (gear != null)
        {
            Image sp = MenuKit.Img("Spinner", root, gear, 860f, -455f, 86f, 86f);
            sp.raycastTarget = false;
            spinner = sp.rectTransform;
        }
    }

    static Sprite Gradient(bool fromTop)
    {
        Texture2D t = new Texture2D(1, 64, TextureFormat.RGBA32, false);
        t.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < 64; y++)
        {
            float k = y / 63f;                 // 0 bottom .. 1 top
            float a = fromTop ? Mathf.SmoothStep(0f, 0.75f, k) : Mathf.SmoothStep(0.85f, 0f, k);
            t.SetPixel(0, y, new Color(0f, 0f, 0f, a));
        }
        t.Apply();
        return Sprite.Create(t, new Rect(0, 0, 1, 64), new Vector2(0.5f, 0.5f));
    }

    static Sprite BarSprite()
    {
        Texture2D t = new Texture2D(4, 32, TextureFormat.RGBA32, false);
        t.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < 32; y++)
        {
            float k = y / 31f;
            Color c = Color.Lerp(new Color(0.85f, 0.42f, 0.08f), new Color(1f, 0.85f, 0.35f), Mathf.Sin(k * Mathf.PI) * 0.9f + k * 0.1f);
            if (y > 20 && y < 25) c = Color.Lerp(c, new Color(1f, 0.97f, 0.8f), 0.6f);
            for (int x = 0; x < 4; x++) t.SetPixel(x, y, c);
        }
        t.Apply();
        return Sprite.Create(t, new Rect(0, 0, 4, 32), new Vector2(0.5f, 0.5f));
    }
}
