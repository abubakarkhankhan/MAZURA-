using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>The title screen: PLAY, HOW TO PLAY, SETTINGS, QUIT. Lives alone in the MainMenu scene.</summary>
public class MainMenu : MonoBehaviour
{
    public string gameScene = "SampleScene";

    Canvas canvas;
    RectTransform bgRect;
    GameObject mainGroup, helpGroup;
    SettingsPanel settings;
    CanvasGroup fader;
    float t;
    bool leaving, loadStarted;

    void Start()
    {
        RemoveGameSystems();
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        GameSettings.Apply();
        if (!LoadingScreen.BootDone) LoadingScreen.Splash();   // first start of the game
        MenuKit.EnsureEventSystem();
        canvas = MenuKit.MakeCanvas(transform, "MenuCanvas", 10);
        Transform c = canvas.transform;

        // slowly drifting background picture of the game
        Sprite bg = (Resources.Load<Sprite>("Menu/bg_main") ?? Resources.Load<Sprite>("Menu/menu_bg"));
        Image bgi = MenuKit.FullScreen("Background", c, Color.white);
        bgi.sprite = bg; bgi.preserveAspect = false;
        bgRect = bgi.rectTransform; bgRect.sizeDelta = new Vector2(160f, 90f);
        MenuKit.FullScreen("Vignette", c, new Color(0.02f, 0.01f, 0.0f, 0.35f));

        TextMeshProUGUI title = MenuKit.Txt("Title", c, "MAZURA", 0f, 330f, 1200f, 190f, 150f, new Color(1f, 0.86f, 0.5f));
        title.fontStyle = FontStyles.Bold; title.characterSpacing = 22f;
        title.outlineWidth = 0.18f; title.outlineColor = new Color32(40, 14, 2, 255);
        title.enableVertexGradient = true;
        title.colorGradient = new VertexGradient(new Color(1f, 0.93f, 0.62f), new Color(1f, 0.93f, 0.62f), new Color(0.9f, 0.55f, 0.18f), new Color(0.9f, 0.55f, 0.18f));
        TextMeshProUGUI sub = MenuKit.Txt("Sub", c, "ESCAPE THE DARK FOREST", 0f, 228f, 900f, 50f, 32f, MenuKit.Cream);
        sub.characterSpacing = 12f; sub.outlineWidth = 0.2f; sub.outlineColor = new Color32(20, 8, 2, 255);
        Image div = MenuKit.Img("Divider", c, MenuKit.S("decorative_gold_divider"), 0f, 190f, 640f, 36f); div.preserveAspect = true;

        mainGroup = MenuKit.Rect("Main", c, 0f, 0f, 0f, 0f).gameObject;
        Transform m = mainGroup.transform;
        if (GameSave.HasSave)
        {
            MenuKit.Btn("CONTINUE", m, 0f, 118f, 330f, Continue, "equip");
            TextMeshProUGUI info = MenuKit.Txt("SaveInfo", m, GameSave.Describe(), 0f, 40f, 700f, 28f, 19f, new Color(1f, 0.9f, 0.72f, 0.9f));
            info.outlineWidth = 0.2f; info.outlineColor = new Color32(10, 5, 2, 255);
            MenuKit.Btn("NEW GAME", m, 0f, -28f, 330f, Play);
            MenuKit.Btn("HOW TO PLAY", m, 0f, -143f, 330f, () => { mainGroup.SetActive(false); helpGroup.SetActive(true); });
            MenuKit.Btn("SETTINGS", m, 0f, -258f, 330f, () => { mainGroup.SetActive(false); settings.Show(true); });
            MenuKit.Btn("QUIT", m, 0f, -373f, 330f, Quit, "drop");
        }
        else
        {
            MenuKit.Btn("PLAY", m, 0f, 70f, 380f, Play, "equip");
            MenuKit.Btn("HOW TO PLAY", m, 0f, -65f, 380f, () => { mainGroup.SetActive(false); helpGroup.SetActive(true); });
            MenuKit.Btn("SETTINGS", m, 0f, -200f, 380f, () => { mainGroup.SetActive(false); settings.Show(true); });
            MenuKit.Btn("QUIT", m, 0f, -335f, 380f, Quit, "drop");
        }
        TextMeshProUGUI ver = MenuKit.Txt("Credit", c, "Final Year Project  -  Saad Abubakar  -  SUIT Peshawar", 0f, -500f, 1200f, 40f, 20f, new Color(1f, 0.9f, 0.75f, 0.7f));
        ver.outlineWidth = 0.2f; ver.outlineColor = new Color32(10, 5, 2, 255);

        // how to play
        helpGroup = MenuKit.Rect("Help", c, 0f, 0f, 0f, 0f).gameObject;
        RectTransform hr = (RectTransform)helpGroup.transform; hr.anchorMin = Vector2.zero; hr.anchorMax = Vector2.one;
        MenuKit.FullScreen("Shade", helpGroup.transform, new Color(0f, 0f, 0f, 0.6f)).raycastTarget = true;
        const float K = 0.7f;
        MenuKit.Img("Frame", helpGroup.transform, MenuKit.S("inventory_frame"), 0f, 0f, 1448f * K, 1086f * K).raycastTarget = true;
        TextMeshProUGUI ht = MenuKit.Txt("Title", helpGroup.transform, "HOW TO PLAY", 0f, (543f - 136f) * K, 500f, 60f, 34f, new Color(1f, 0.95f, 0.85f));
        ht.fontStyle = FontStyles.Bold; ht.characterSpacing = 6f;
        TextMeshProUGUI body = MenuKit.Txt("Body", helpGroup.transform,
            "<color=#FFD27A>Goal</color>  Find the 4 map pieces in chests across the maze. They open the gate of the Old Dungeon.\n" +
            "Inside, find the 3 keys, defeat the Dungeon Lord and step into the portal to escape.\n\n" +
            "<color=#FFD27A>Survive</color>  Chop trees and mine rocks for wood and stone. Craft tools, torches, traps and potions.\n" +
            "Build a Workbench to unlock better recipes. Nights are dangerous - stay near light.\n\n" +
            "<color=#FFD27A>Controls</color>\nWASD move   Shift sprint   Space jump   LMB use / attack   RMB block (shield)\n" +
            "E interact   C craft   I inventory   B recipe book   1-5 hotbar   Q switch weapon   F5 save   Esc pause",
            0f, 10f, 900f, 470f, 24f, MenuKit.Cream);
        body.enableWordWrapping = true; body.lineSpacing = 4f;
        MenuKit.Btn("BACK", helpGroup.transform, 0f, (543f - 905f) * K, 240f, () => { helpGroup.SetActive(false); mainGroup.SetActive(true); }, "drop");
        MenuKit.IconBtn("close", helpGroup.transform, 1448f * K * 0.5f - 70f, 1086f * K * 0.5f - 70f, 76f, () => { helpGroup.SetActive(false); mainGroup.SetActive(true); });
        helpGroup.SetActive(false);

        settings = new SettingsPanel(c, () => mainGroup.SetActive(true));

        GameObject f = MenuKit.FullScreen("Fade", c, Color.black).gameObject;
        fader = f.AddComponent<CanvasGroup>(); fader.alpha = 1f; fader.blocksRaycasts = false;
    }

    /// <summary>
    /// Game systems (inventory, crafting, hotbar...) create themselves in every scene. In the menu they are not
    /// needed and some of them lock / hide the mouse or add a second EventSystem, so the buttons stop working.
    /// </summary>
    void RemoveGameSystems()
    {
        bool haveEvents = false;
        foreach (GameObject g in gameObject.scene.GetRootGameObjects())
        {
            if (g == gameObject || g.GetComponent<Camera>() != null || g.GetComponent<AmbientMusic>() != null) continue;
            if (g.GetComponent<UnityEngine.EventSystems.EventSystem>() != null && !haveEvents) { haveEvents = true; continue; }
            Destroy(g);
        }
        Time.timeScale = 1f;
        AudioListener.pause = false;
    }

    void LateUpdate()
    {
        // nothing in the menu may lock or hide the mouse
        if (Cursor.lockState != CursorLockMode.None) Cursor.lockState = CursorLockMode.None;
        if (!Cursor.visible) Cursor.visible = true;
    }

    void Update()
    {
        t += Time.unscaledDeltaTime;
        if (bgRect != null)
        {
            float s = 1.06f + Mathf.Sin(t * 0.05f) * 0.04f;
            bgRect.localScale = Vector3.one * s;
            bgRect.anchoredPosition = new Vector2(Mathf.Sin(t * 0.07f) * 30f, Mathf.Cos(t * 0.05f) * 15f);
        }
        if (fader != null) fader.alpha = Mathf.MoveTowards(fader.alpha, leaving ? 1f : 0f, Time.unscaledDeltaTime * 1.5f);
        if (leaving && !loadStarted) { loadStarted = true; LoadingScreen.LoadScene(gameScene, GameSave.PendingLoad ? "continue" : "forest"); }
    }

    void Play()
    {
        if (leaving) return;
        StoryScreen.IntroShown = false;
        GameSave.PendingLoad = false;
        leaving = true;
        fader.blocksRaycasts = true;
    }

    void Continue()
    {
        if (leaving) return;
        StoryScreen.IntroShown = true;
        GameSave.PendingLoad = true;
        leaving = true;
        fader.blocksRaycasts = true;
    }

    void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
