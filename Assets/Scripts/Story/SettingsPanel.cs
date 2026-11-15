using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>The settings window (volume, music, quality, fullscreen), used by the main menu and the pause menu.</summary>
public class SettingsPanel
{
    readonly GameObject root;
    Button quality, full;

    public bool Visible { get { return root.activeSelf; } }

    public SettingsPanel(Transform canvas, System.Action onBack)
    {
        root = MenuKit.Rect("Settings", canvas, 0f, 0f, 0f, 0f).gameObject;
        RectTransform rr = (RectTransform)root.transform; rr.anchorMin = Vector2.zero; rr.anchorMax = Vector2.one;
        MenuKit.FullScreen("Shade", root.transform, new Color(0f, 0f, 0f, 0.6f)).raycastTarget = true;
        const float K = 0.62f;
        MenuKit.Img("Frame", root.transform, MenuKit.S("inventory_frame"), 0f, 0f, 1448f * K, 1086f * K).raycastTarget = true;
        TextMeshProUGUI t = MenuKit.Txt("Title", root.transform, "SETTINGS", 0f, (543f - 136f) * K, 500f, 60f, 36f, new Color(1f, 0.95f, 0.85f));
        t.fontStyle = FontStyles.Bold; t.characterSpacing = 8f;
        MenuKit.SliderRow("Sound", root.transform, 0f, 120f, GameSettings.Volume, v => GameSettings.Volume = v);
        MenuKit.SliderRow("Music", root.transform, 0f, 55f, GameSettings.Music, v => GameSettings.Music = v);
        quality = MenuKit.Btn("QUALITY: " + GameSettings.QualityName.ToUpperInvariant(), root.transform, 0f, -30f, 380f, () =>
        {
            GameSettings.NextQuality();
            MenuKit.SetLabel(quality, "QUALITY: " + GameSettings.QualityName.ToUpperInvariant());
        });
        full = MenuKit.Btn(FullText(), root.transform, 0f, -140f, 380f, () =>
        {
            GameSettings.Fullscreen = !GameSettings.Fullscreen;
            MenuKit.SetLabel(full, FullText());
        });
        MenuKit.Btn("BACK", root.transform, 0f, -255f, 260f, () => { Show(false); if (onBack != null) onBack(); }, "drop");
        root.SetActive(false);
    }

    static string FullText() { return GameSettings.Fullscreen ? "FULLSCREEN: ON" : "FULLSCREEN: OFF"; }

    public void Show(bool on) { root.SetActive(on); if (on) root.transform.SetAsLastSibling(); }
}
