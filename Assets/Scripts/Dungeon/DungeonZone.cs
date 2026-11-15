using UnityEngine;

/// <summary>
/// The underground Dungeon area. While the player is inside, the sun is off and the air is dark and foggy.
/// Other systems ask DungeonZone.PlayerInside / DungeonZone.Contains(pos) (the enemy spawner, the quest).
/// </summary>
[DefaultExecutionOrder(1000)]
public class DungeonZone : MonoBehaviour
{
    public static DungeonZone Instance { get; private set; }
    public static bool PlayerInside { get; private set; }

    [Tooltip("World-space box of the whole dungeon (set by the DungeonBuilder).")]
    public Bounds area = new Bounds(new Vector3(430f, 2f, 30f), new Vector3(80f, 12f, 80f));
    public Color ambient = new Color(0.2f, 0.17f, 0.23f);
    public Color fogColor = new Color(0.025f, 0.02f, 0.03f);
    public float fogDensity = 0.035f;

    bool wasInside;
    GameObject[] skyStuff;

    void ShowSky(bool on)
    {
        if (skyStuff == null) skyStuff = new GameObject[] { GameObject.Find("LightShafts"), GameObject.Find("SkyDecor") };
        foreach (GameObject g in skyStuff) if (g != null) g.SetActive(on);
    }
    bool savedFog; FogMode savedMode; Color savedFogColor; float savedDensity, savedRefl;
    Light carry;
    CameraClearFlags savedClear; Color savedBg;

    void Awake() { Instance = this; }
    void OnDestroy() { if (Instance == this) { Instance = null; PlayerInside = false; } }

    public static bool Contains(Vector3 p) { return Instance != null && Instance.area.Contains(p); }

    void LateUpdate()
    {
        ToolController tc = ToolController.Instance;
        bool inside = tc != null && area.Contains(tc.transform.position);
        PlayerInside = inside;

        if (inside && !wasInside)
        {
            savedFog = RenderSettings.fog; savedMode = RenderSettings.fogMode;
            savedFogColor = RenderSettings.fogColor; savedDensity = RenderSettings.fogDensity;
            ShowSky(false);
            savedRefl = RenderSettings.reflectionIntensity;
            Camera cam = Camera.main;
            if (cam != null) { savedClear = cam.clearFlags; savedBg = cam.backgroundColor; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black; }
        }
        else if (!inside && wasInside)
        {
            RenderSettings.fog = savedFog; RenderSettings.fogMode = savedMode;
            RenderSettings.fogColor = savedFogColor; RenderSettings.fogDensity = savedDensity;
            ShowSky(true);
            RenderSettings.reflectionIntensity = savedRefl;
            if (RenderSettings.sun != null) RenderSettings.sun.enabled = true;
            if (carry != null) carry.enabled = false;
            Camera cam = Camera.main;
            if (cam != null) { cam.clearFlags = savedClear; cam.backgroundColor = savedBg; }
        }
        wasInside = inside;
        if (!inside) return;

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = ambient;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogDensity = fogDensity;
        if (RenderSettings.sun != null) RenderSettings.sun.enabled = false;
        RenderSettings.reflectionIntensity = 0f;

        // a soft light around the player so you can always see yourself
        if (carry == null)
        {
            carry = new GameObject("DungeonCarryLight").AddComponent<Light>();
            carry.type = LightType.Point; carry.color = new Color(1f, 0.82f, 0.62f);
            carry.range = 7f; carry.intensity = 1.1f; carry.shadows = LightShadows.None;
        }
        carry.enabled = true;
        carry.transform.position = tc.transform.position + Vector3.up * 2.2f - tc.transform.forward * 0.6f;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.6f, 0.3f, 1f, 0.6f);
        Gizmos.DrawWireCube(area.center, area.size);
    }
}
