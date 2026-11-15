using UnityEngine;

/// <summary>Saved player settings (volume, music, graphics quality, fullscreen). Applied when the game starts.</summary>
public static class GameSettings
{
    public static float Volume { get { return PlayerPrefs.GetFloat("mz_volume", 0.8f); } set { PlayerPrefs.SetFloat("mz_volume", Mathf.Clamp01(value)); Apply(); } }
    public static float Music { get { return PlayerPrefs.GetFloat("mz_music", 0.6f); } set { PlayerPrefs.SetFloat("mz_music", Mathf.Clamp01(value)); Apply(); } }
    public static int Quality { get { return PlayerPrefs.GetInt("mz_quality", Mathf.Max(0, QualitySettings.names.Length - 1)); } set { PlayerPrefs.SetInt("mz_quality", value); Apply(); } }
    public static bool Fullscreen { get { return PlayerPrefs.GetInt("mz_full", 1) == 1; } set { PlayerPrefs.SetInt("mz_full", value ? 1 : 0); Apply(); } }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    public static void Apply()
    {
        AudioListener.volume = Volume;
        int q = Mathf.Clamp(Quality, 0, QualitySettings.names.Length - 1);
        if (QualitySettings.GetQualityLevel() != q) QualitySettings.SetQualityLevel(q, true);
        if (!Application.isEditor && Screen.fullScreen != Fullscreen) Screen.fullScreen = Fullscreen;
    }

    public static string QualityName { get { string[] n = QualitySettings.names; return n.Length == 0 ? "-" : n[Mathf.Clamp(Quality, 0, n.Length - 1)]; } }
    public static void NextQuality() { int n = QualitySettings.names.Length; if (n > 0) Quality = (Quality + 1) % n; }
}
