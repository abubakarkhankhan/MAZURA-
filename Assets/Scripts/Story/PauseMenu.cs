using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Esc in the game: RESUME, SETTINGS, MAIN MENU, QUIT. The game is paused while it is open.</summary>
public class PauseMenu : MonoBehaviour
{
    public static bool IsOpen { get; private set; }

    Canvas canvas;
    GameObject group;
    SettingsPanel settings;
    bool lockedLastFrame;
    MonoBehaviour lookInputs;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (Object.FindAnyObjectByType<ToolController>() == null) return;
        if (Object.FindAnyObjectByType<PauseMenu>() != null) return;
        new GameObject("PauseMenu").AddComponent<PauseMenu>();
    }

    void Awake()
    {
        IsOpen = false;
        canvas = MenuKit.MakeCanvas(transform, "PauseCanvas", 140);
        Transform c = canvas.transform;
        group = MenuKit.Rect("Group", c, 0f, 0f, 0f, 0f).gameObject;
        RectTransform gr = (RectTransform)group.transform; gr.anchorMin = Vector2.zero; gr.anchorMax = Vector2.one;
        MenuKit.FullScreen("Shade", group.transform, new Color(0f, 0f, 0f, 0.6f)).raycastTarget = true;
        const float K = 0.8f;
        MenuKit.Img("Frame", group.transform, MenuKit.S("inventory_frame"), 0f, 0f, 1448f * K, 1086f * K).raycastTarget = true;
        TextMeshProUGUI t = MenuKit.Txt("Title", group.transform, "PAUSED", 0f, (543f - 136f) * K, 400f, 56f, 34f, new Color(1f, 0.95f, 0.85f));
        t.fontStyle = FontStyles.Bold; t.characterSpacing = 8f;
        MenuKit.Btn("RESUME", group.transform, 0f, 165f, 300f, Close, "equip");
        MenuKit.Btn("SAVE GAME", group.transform, 0f, 60f, 300f, () => { if (GameSave.Instance != null && GameSave.Instance.TrySave(false)) Close(); });
        MenuKit.Btn("SETTINGS", group.transform, 0f, -45f, 300f, () => { group.SetActive(false); settings.Show(true); });
        MenuKit.Btn("MAIN MENU", group.transform, 0f, -150f, 300f, () => { Close(); StoryScreen.ToMenu(); });
        MenuKit.Btn("QUIT GAME", group.transform, 0f, -255f, 300f, Quit, "drop");
        settings = new SettingsPanel(c, () => group.SetActive(true));
        canvas.enabled = false;
    }

    void Update()
    {
        Keyboard kb = Keyboard.current;
        bool locked = Cursor.lockState == CursorLockMode.Locked;
        if (kb != null && kb.escapeKey.wasPressedThisFrame)
        {
            if (IsOpen) { if (settings.Visible) { settings.Show(false); group.SetActive(true); } else Close(); }
            else if (locked && lockedLastFrame && !StoryScreen.IsOpen && !PlacementController.IsPlacing) Open();
        }
        lockedLastFrame = locked;
    }

    void Open()
    {
        HealthManager hm = Object.FindAnyObjectByType<HealthManager>();
        if (hm != null && hm.IsDead) return;
        IsOpen = true;
        canvas.enabled = true; group.SetActive(true); settings.Show(false);
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        SetLook(false);
        MenuKit.EnsureEventSystem();
        AudioListener.pause = true;
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        canvas.enabled = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
        SetLook(true);
        lockedLastFrame = false;
    }

    void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
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
}
