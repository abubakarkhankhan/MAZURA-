using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;

/// <summary>
/// Day / night cycle. The game always starts in DAY.
/// Day lasts dayDuration seconds, then night lasts nightDuration seconds, then it repeats
/// (defaults: 600 s + 600 s = a 20 minute cycle).
/// The Directional Light turns smoothly the whole time. During the last
/// 'transitionDuration' seconds of each phase (dusk at the end of the day, dawn at the end of the
/// night) the colours blend gradually, so nothing jumps.
///
/// Other scripts can use it through DayNightManager.Instance:
///   if (DayNightManager.Instance.IsNight) ...
///   DayNightManager.Instance.OnNightStart.AddListener(SpawnEnemies);
///   DayNightManager.Instance.OnDawn.AddListener(DespawnEnemies);
/// </summary>
public class DayNightManager : MonoBehaviour
{
    public enum Phase { Day, Night }

    public static DayNightManager Instance { get; private set; }

    [Header("Cycle (seconds)")]
    [Tooltip("Length of the day phase in seconds. 600 = 10 minutes.")]
    public float dayDuration = 600f;
    [Tooltip("Length of the night phase in seconds. 600 = 10 minutes.")]
    public float nightDuration = 600f;
    [Tooltip("Length of the smooth dusk (end of day) and dawn (end of night) blend, in seconds.")]
    public float transitionDuration = 30f;

    [Header("Testing")]
    [Tooltip("Time multiplier. 1 = normal. Set 20 to test quickly.")]
    public float debugSpeed = 1f;

    [Header("References")]
    [Tooltip("The Directional Light that acts as sun/moon. Empty = the scene's main directional light.")]
    [SerializeField] private Light sun;
    [Tooltip("HUD text that shows e.g. 'Day - 07:42 left'.")]
    [SerializeField] private TMP_Text phaseText;

    [Header("Look (the gradients/curve are read over one full cycle, left = night, middle = noon)")]
    [Tooltip("Sun / moon light colour.")]
    [SerializeField] private Gradient lightColor = DefaultLightColor();
    [Tooltip("Ambient (shadow fill) colour.")]
    [SerializeField] private Gradient ambientColor = DefaultAmbientColor();
    [Tooltip("Light intensity.")]
    [SerializeField] private AnimationCurve lightIntensity = DefaultIntensity();

    [Header("Sun path (the sun sweeps across the sky; SkyDecor turns the sky texture to match)")]
    [Tooltip("Height of the sun above the horizon (degrees) at sunrise and sunset.")]
    public float sunLowElevation = 14f;
    [Tooltip("Height of the sun above the horizon (degrees) at noon. The sky texture has its sun painted low, so keep this modest (30-40) or the horizon glow tilts.")]
    public float sunHighElevation = 34f;
    [Tooltip("Compass direction (degrees, 0 = +Z, 90 = +X) where the sun rises.")]
    public float sunriseAzimuth = 70f;
    [Tooltip("Compass direction where the sun sets. The sun sweeps from the rise to this direction during the day.")]
    public float sunsetAzimuth = 230f;
    [Tooltip("How far below the horizon (degrees) the light travels at midnight.")]
    public float nightDepth = 40f;

    [Header("Events")]
    [Tooltip("Fired once when the night begins.")]
    public UnityEvent OnNightStart = new UnityEvent();
    [Tooltip("Fired once when the day begins (dawn).")]
    public UnityEvent OnDawn = new UnityEvent();

    // Positions on the colour gradients (0..1) that mark the key moments of the cycle.
    private const float GradDawnStart = 0.18f;  // end of deep night, dawn starts
    private const float GradDayStart = 0.32f;   // morning, day phase begins
    private const float GradDayEnd = 0.62f;     // afternoon, dusk starts
    private const float GradNightStart = 0.80f; // dusk is over, night phase begins

    private Phase phase = Phase.Day;
    private float elapsed;           // seconds spent in the current phase
    private int shownSecond = -1;    // avoids rebuilding the HUD string every frame

    public bool IsNight { get { return phase == Phase.Night; } }
    public float PhaseProgress { get { return Mathf.Clamp01(elapsed / PhaseLength(phase)); } }
    public Phase CurrentPhase { get { return phase; } }
    public float PhaseTimeLeft { get { return Mathf.Max(0f, PhaseLength(phase) - elapsed); } }
    /// <summary>Position on the colour gradients (0..1): 0.0-0.18 midnight, 0.18-0.32 dawn/morning, 0.32-0.62 day, 0.62-0.80 evening, 0.80-1.0 night.</summary>
    public float GradientTime { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (sun == null) sun = RenderSettings.sun;
        if (sun == null)
        {
            foreach (Light l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l.type == LightType.Directional) { sun = l; break; }
            }
        }

        // Ambient colour only works in Flat mode.
        RenderSettings.ambientMode = AmbientMode.Flat;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        // Always start in the day.
        phase = Phase.Day;
        elapsed = 0f;
        Apply();
    }

    private void Update()
    {
        elapsed += Time.deltaTime * Mathf.Max(0f, debugSpeed);

        // 'while' so a very high debug speed can never skip a phase.
        float length = PhaseLength(phase);
        while (elapsed >= length)
        {
            elapsed -= length;
            SwitchPhase();
            length = PhaseLength(phase);
        }

        Apply();
    }

    // ---------------------------------------------------------------- public testing helpers

    /// <summary>Jump straight to the start of the night (fires OnNightStart if it was day).</summary>
    [ContextMenu("Jump To Night")]
    public void JumpToNight() { JumpTo(Phase.Night); }

    /// <summary>Jump straight to the start of the day (fires OnDawn if it was night).</summary>
    [ContextMenu("Jump To Day")]
    public void JumpToDay() { JumpTo(Phase.Day); }

    /// <summary>Jump to the start of the given phase.</summary>
    public void JumpTo(Phase target)
    {
        bool changed = phase != target;
        phase = target;
        elapsed = 0f;
        if (changed) RaisePhaseEvent();
        Apply();
    }

    // ---------------------------------------------------------------- internals

    private float PhaseLength(Phase p)
    {
        return Mathf.Max(1f, p == Phase.Day ? dayDuration : nightDuration);
    }

    private void SwitchPhase()
    {
        phase = phase == Phase.Day ? Phase.Night : Phase.Day;
        RaisePhaseEvent();
    }

    private void RaisePhaseEvent()
    {
        if (phase == Phase.Night) OnNightStart.Invoke();
        else OnDawn.Invoke();
    }

    // Updates light rotation, colours, ambient and HUD for the current moment.
    private void Apply()
    {
        float length = PhaseLength(phase);
        float progress = Mathf.Clamp01(elapsed / length);
        float blend = Mathf.Min(Mathf.Max(0f, transitionDuration), length * 0.5f); // blend length in this phase
        float coreLength = length - blend;                                         // seconds before the blend starts
        bool inBlend = blend > 0f && elapsed >= coreLength;
        float blendT = inBlend ? Mathf.Clamp01((elapsed - coreLength) / blend) : 0f;

        // g = where we are on the colour gradients (0..1). It is continuous at every phase change.
        float g;
        float az;   // compass direction of the sun (degrees)
        float el;   // height of the sun above the horizon (degrees, negative = below)
        if (phase == Phase.Day)
        {
            // Morning -> afternoon, then the dusk blend into night.
            g = inBlend ? Mathf.Lerp(GradDayEnd, GradNightStart, blendT)
                        : Mathf.Lerp(GradDayStart, GradDayEnd, elapsed / Mathf.Max(0.01f, coreLength));
            az = Mathf.Lerp(sunriseAzimuth, sunsetAzimuth, progress);
            el = Mathf.Lerp(sunLowElevation, sunHighElevation, Mathf.Sin(progress * Mathf.PI));
        }
        else
        {
            // Deep night (wraps past 1.0 back to 0), then the dawn blend into morning.
            float nightSpan = (1f - GradNightStart) + GradDawnStart;
            g = inBlend ? Mathf.Lerp(GradDawnStart, GradDayStart, blendT)
                        : (GradNightStart + nightSpan * (elapsed / Mathf.Max(0.01f, coreLength))) % 1f;
            // Keeps turning the same way: sunset direction -> sunrise direction (one more turn), dipping below the horizon.
            az = Mathf.Lerp(sunsetAzimuth, sunriseAzimuth + 360f, progress);
            el = sunLowElevation - (sunLowElevation + nightDepth) * Mathf.Sin(progress * Mathf.PI);
        }

        GradientTime = g;
        if (sun != null)
        {
            float azR = az * Mathf.Deg2Rad, elR = el * Mathf.Deg2Rad;
            Vector3 toSun = new Vector3(Mathf.Cos(elR) * Mathf.Sin(azR), Mathf.Sin(elR), Mathf.Cos(elR) * Mathf.Cos(azR));
            sun.transform.rotation = Quaternion.LookRotation(-toSun, Vector3.up);
            sun.color = lightColor.Evaluate(g);
            sun.intensity = lightIntensity.Evaluate(g);
        }
        RenderSettings.ambientLight = ambientColor.Evaluate(g);

        UpdateHud();
    }

    private void UpdateHud()
    {
        if (phaseText == null) return;

        int secondsLeft = Mathf.CeilToInt(PhaseTimeLeft);
        // Only rebuild the string when the shown second (or the phase) changes.
        int key = secondsLeft * 2 + (phase == Phase.Night ? 1 : 0);
        if (key == shownSecond) return;
        shownSecond = key;

        phaseText.text = string.Format("{0} - {1:00}:{2:00} left",
            phase == Phase.Day ? "Day" : "Night", secondsLeft / 60, secondsLeft % 60);
    }

    // ---------------------------------------------------------------- default look

    private static Gradient DefaultLightColor()
    {
        return MakeGradient(
            new Color(0.35f, 0.45f, 0.80f), 0.00f,  // night (cold moon)
            new Color(0.35f, 0.45f, 0.80f), 0.18f,
            new Color(1.00f, 0.55f, 0.40f), 0.24f,  // dawn
            new Color(1.00f, 0.93f, 0.80f), 0.32f,  // morning
            new Color(0.90f, 1.00f, 0.95f), 0.50f,  // noon (slightly green, forest)
            new Color(1.00f, 0.90f, 0.70f), 0.62f,  // afternoon
            new Color(1.00f, 0.50f, 0.25f), 0.72f,  // sunset
            new Color(0.35f, 0.45f, 0.80f), 0.80f); // night
    }

    private static Gradient DefaultAmbientColor()
    {
        return MakeGradient(
            new Color(0.030f, 0.040f, 0.090f), 0.00f,
            new Color(0.030f, 0.040f, 0.090f), 0.18f,
            new Color(0.180f, 0.120f, 0.120f), 0.24f,
            new Color(0.110f, 0.150f, 0.120f), 0.32f,
            new Color(0.120f, 0.170f, 0.130f), 0.50f,
            new Color(0.120f, 0.140f, 0.110f), 0.62f,
            new Color(0.170f, 0.100f, 0.090f), 0.72f,
            new Color(0.030f, 0.040f, 0.090f), 0.80f);
    }

    private static AnimationCurve DefaultIntensity()
    {
        return new AnimationCurve(
            new Keyframe(0.00f, 0.05f),
            new Keyframe(0.18f, 0.05f),
            new Keyframe(0.24f, 0.35f),
            new Keyframe(0.32f, 0.85f),
            new Keyframe(0.50f, 1.00f),
            new Keyframe(0.62f, 0.90f),
            new Keyframe(0.72f, 0.45f),
            new Keyframe(0.80f, 0.05f),
            new Keyframe(1.00f, 0.05f));
    }

    // Builds a gradient from (colour, time) pairs.
    private static Gradient MakeGradient(
        Color c0, float t0, Color c1, float t1, Color c2, float t2, Color c3, float t3,
        Color c4, float t4, Color c5, float t5, Color c6, float t6, Color c7, float t7)
    {
        Gradient g = new Gradient();
        g.colorKeys = new GradientColorKey[]
        {
            new GradientColorKey(c0, t0), new GradientColorKey(c1, t1),
            new GradientColorKey(c2, t2), new GradientColorKey(c3, t3),
            new GradientColorKey(c4, t4), new GradientColorKey(c5, t5),
            new GradientColorKey(c6, t6), new GradientColorKey(c7, t7)
        };
        g.alphaKeys = new GradientAlphaKey[]
        {
            new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f)
        };
        return g;
    }
}
