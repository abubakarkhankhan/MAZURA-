using System.Collections;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Big framed story screens: the intro, the death screen (try again) and the win screen.
/// Also the black screen fade used when the player goes down into the dungeon.
/// The game is paused while a screen is open.
/// </summary>
public class StoryScreen : MonoBehaviour
{
    public static StoryScreen Instance { get; private set; }
    public static bool IsOpen { get; private set; }
    public static bool IntroShown;

    public const string Intro =
        "You wake up on cold earth, deep inside the <color=#FFD27A>Dark Forest</color> - a living maze that no traveller has ever left.\n\n" +
        "The old stories say there is only one way out: through the <color=#FFD27A>Old Dungeon</color> beneath the forest. Its gate is sealed, and only the " +
        "<color=#FFD27A>4 pieces of the ancient map</color> can open it.\n\n" +
        "Gather wood and stone, craft tools and light, and survive the nights. Creatures hunt in the dark.\n\n" +
        "<size=78%><color=#C9B48A>WASD move  -  Shift sprint  -  LMB use tool  -  RMB block  -  E interact\nC craft  -  I inventory  -  B recipes  -  1-5 hotbar  -  Esc pause</color></size>";

    Canvas canvas;
    CanvasGroup group;
    RectTransform panel;
    TextMeshProUGUI title, body;
    Button b1, b2;
    TextMeshProUGUI b1t, b2t;
    Image fade;
    TMP_FontAsset font;
    System.Action a1, a2;
    float openT;
    MonoBehaviour lookInputs;

    static Sprite S(string n) { return Resources.Load<Sprite>("InventoryUI/" + n); }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (Instance != null) return;
        if (Object.FindAnyObjectByType<ToolController>() == null) return;   // not the game scene (e.g. main menu)
        new GameObject("StoryScreen").AddComponent<StoryScreen>();
    }

    void Awake()
    {
        Instance = this;
        Time.timeScale = 1f;
        Build();
        canvas.enabled = false;
        IsOpen = false;
    }

    void Start()
    {
        HealthManager hm = Object.FindAnyObjectByType<HealthManager>();
        if (hm != null) hm.OnDied.AddListener(() => StartCoroutine(DeathRoutine()));
        if (!IntroShown) { IntroShown = true; StartCoroutine(IntroLater()); }
    }

    IEnumerator IntroLater()
    {
        yield return null; yield return null;
        Show("MAZURA", Intro, "BEGIN", null, null, null, 25f);
    }

    IEnumerator DeathRoutine()
    {
        yield return new WaitForSecondsRealtime(2.2f);
        Show("YOU DIED", "The forest claims another wanderer...\n\n" + GameStats.Summary(),
            "TRY AGAIN", Restart, "MAIN MENU", ToMenu, 26f);
    }

    public static void ShowWin()
    {
        if (Instance == null) return;
        Instance.Show("YOU ESCAPED",
            "The Dungeon Lord has fallen and the portal carries you out of the maze.\nThe Dark Forest will not hold you again.\n\n" + GameStats.Summary(),
            "PLAY AGAIN", Restart, "MAIN MENU", ToMenu, 26f);
    }

    public static void Restart()
    {
        Time.timeScale = 1f;
        IntroShown = false;
        // after dying: go back to the last save (if there is one)
        if (GameSave.HasSave && (DungeonQuest.Instance == null || DungeonQuest.Instance.Stage != QuestStage.Won)) { GameSave.PendingLoad = true; IntroShown = true; }
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public static void ToMenu()
    {
        Time.timeScale = 1f;
        IntroShown = false;
        if (Application.CanStreamedLevelBeLoaded("MainMenu")) SceneManager.LoadScene("MainMenu");
        else Restart();
    }

    // ---------------------------------------------------------------- build
    RectTransform Rect(string n, Transform p, float x, float y, float w, float h)
    {
        RectTransform r = (RectTransform)new GameObject(n, typeof(RectTransform)).transform;
        r.SetParent(p, false);
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = new Vector2(x, y); r.sizeDelta = new Vector2(w, h);
        return r;
    }

    TextMeshProUGUI Txt(string n, Transform p, float x, float y, float w, float h, float size, Color c)
    {
        TextMeshProUGUI t = Rect(n, p, x, y, w, h).gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.fontSize = size; t.color = c; t.alignment = TextAlignmentOptions.Center; t.raycastTarget = false;
        return t;
    }

    Button Btn(string n, Transform p, float x, float y, out TextMeshProUGUI label)
    {
        RectTransform r = Rect(n, p, x, y, 250f, 110f);
        Image img = r.gameObject.AddComponent<Image>();
        img.sprite = S("use_normal"); img.preserveAspect = true;
        Button b = r.gameObject.AddComponent<Button>();
        SpriteState ss = new SpriteState();
        ss.highlightedSprite = S("use_hover"); ss.pressedSprite = S("use_hover"); ss.selectedSprite = S("use_normal");
        b.transition = Selectable.Transition.SpriteSwap; b.spriteState = ss;
        label = Txt("T", r, 0f, 2f, 210f, 60f, 26f, new Color(1f, 0.92f, 0.75f));
        label.fontStyle = FontStyles.Bold; label.outlineWidth = 0.2f; label.outlineColor = new Color32(20, 10, 4, 255);
        label.enableWordWrapping = false;
        return b;
    }

    void Build()
    {
        font = UIFont.Get();
        GameObject cgo = new GameObject("StoryCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        cgo.transform.SetParent(transform, false);
        canvas = cgo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 150;
        CanvasScaler cs = cgo.GetComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; cs.referenceResolution = new Vector2(1920f, 1080f);
        cs.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        group = cgo.GetComponent<CanvasGroup>();

        Image shade = Rect("Shade", cgo.transform, 0f, 0f, 0f, 0f).gameObject.AddComponent<Image>();
        shade.rectTransform.anchorMin = Vector2.zero; shade.rectTransform.anchorMax = Vector2.one;
        shade.color = new Color(0f, 0f, 0f, 0.72f);

        const float K = 0.7f;
        panel = Rect("Panel", cgo.transform, 0f, 0f, 1448f * K, 1086f * K);
        Image frame = panel.gameObject.AddComponent<Image>(); frame.sprite = S("inventory_frame"); frame.raycastTarget = true;
        title = Txt("Title", panel, 0f, (543f - 136f) * K, 520f, 64f, 40f, new Color(1f, 0.95f, 0.85f));
        title.fontStyle = FontStyles.Bold; title.characterSpacing = 8f; title.outlineWidth = 0.22f; title.outlineColor = new Color32(70, 25, 5, 255);
        title.enableWordWrapping = false;
        body = Txt("Body", panel, 0f, 25f, 1120f * K, 520f * K, 25f, new Color(0.95f, 0.88f, 0.74f));
        body.enableWordWrapping = true; body.lineSpacing = 6f;
        float by = (543f - 905f) * K;
        b1 = Btn("Btn1", panel, -150f, by, out b1t);
        b2 = Btn("Btn2", panel, 150f, by, out b2t);
        b1.onClick.AddListener(() => Choose(a1));
        b2.onClick.AddListener(() => Choose(a2));

        GameObject fgo = new GameObject("FadeCanvas", typeof(Canvas));
        fgo.transform.SetParent(transform, false);
        Canvas fc = fgo.GetComponent<Canvas>(); fc.renderMode = RenderMode.ScreenSpaceOverlay; fc.sortingOrder = 300;
        fade = Rect("Fade", fgo.transform, 0f, 0f, 0f, 0f).gameObject.AddComponent<Image>();
        fade.rectTransform.anchorMin = Vector2.zero; fade.rectTransform.anchorMax = Vector2.one;
        fade.color = new Color(0f, 0f, 0f, 0f); fade.raycastTarget = false;
    }

    // ---------------------------------------------------------------- show / hide
    void Show(string t, string text, string l1, System.Action c1, string l2, System.Action c2, float size)
    {
        if (InventoryWindow.IsOpen && InventoryWindow.Instance != null) InventoryWindow.Instance.Close();
        if (WorkshopUI.IsOpen) WorkshopUI.Close();
        title.text = t; body.text = text; body.fontSize = size;
        b1t.text = l1; a1 = c1;
        b2.gameObject.SetActive(l2 != null);
        if (l2 != null) { b2t.text = l2; a2 = c2; }
        RectTransform r1 = (RectTransform)b1.transform;
        r1.anchoredPosition = new Vector2(l2 != null ? -150f : 0f, r1.anchoredPosition.y);
        canvas.enabled = true; IsOpen = true; openT = 0f;
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        SetLook(false);
        if (EventSystem.current == null)
        {
            GameObject es = new GameObject("EventSystem", typeof(EventSystem));
            es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }
    }

    void Choose(System.Action a)
    {
        Hide();
        if (a != null) a();
    }

    void Hide()
    {
        canvas.enabled = false; IsOpen = false;
        Time.timeScale = 1f;
        HealthManager hm = Object.FindAnyObjectByType<HealthManager>();
        if (hm != null && hm.IsDead) return;
        Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
        SetLook(true);
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
        System.Type ty = lookInputs.GetType();
        FieldInfo f = ty.GetField("cursorInputForLook"); if (f != null) f.SetValue(lookInputs, on);
        f = ty.GetField("look"); if (f != null) f.SetValue(lookInputs, Vector2.zero);
        f = ty.GetField("move"); if (f != null) f.SetValue(lookInputs, Vector2.zero);
    }

    void Update()
    {
        if (!IsOpen) return;
        openT = Mathf.Min(1f, openT + Time.unscaledDeltaTime * 4f);
        float e = 1f - Mathf.Pow(1f - openT, 3f);
        group.alpha = e;
        panel.localScale = Vector3.one * Mathf.Lerp(0.9f, 1f, e);
        UnityEngine.InputSystem.Keyboard kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null && !b2.gameObject.activeSelf && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)) Choose(a1);
    }

    // ---------------------------------------------------------------- fade
    public static void FadeThrough(System.Action middle)
    {
        if (Instance == null) { if (middle != null) middle(); return; }
        Instance.StartCoroutine(Instance.FadeRoutine(middle));
    }

    IEnumerator FadeRoutine(System.Action middle)
    {
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime * 2.2f) { fade.color = new Color(0f, 0f, 0f, t); yield return null; }
        fade.color = Color.black;
        if (middle != null) middle();
        yield return new WaitForSecondsRealtime(0.35f);
        for (float t = 1f; t > 0f; t -= Time.unscaledDeltaTime * 1.6f) { fade.color = new Color(0f, 0f, 0f, t); yield return null; }
        fade.color = new Color(0f, 0f, 0f, 0f);
    }
}
