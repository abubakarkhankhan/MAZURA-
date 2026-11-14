using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Anything that gives off a light that scares light-fearing enemies (the torch, later the campfire).</summary>
public interface IFearLight
{
    Vector3 LightPosition { get; }
    float ScareRadius { get; }
    bool LightActive { get; }
}

public static class FearLight
{
    public static readonly List<IFearLight> Sources = new List<IFearLight>();

    /// <summary>True when p is inside the light of any source. 'from' is the position of that light.</summary>
    public static bool Scares(Vector3 p, out Vector3 from)
    {
        from = p;
        for (int i = 0; i < Sources.Count; i++)
        {
            IFearLight s = Sources[i];
            if (s == null || !s.LightActive) continue;
            Vector3 c = s.LightPosition;
            Vector3 d = p - c; d.y = 0f;
            float r = s.ScareRadius;
            if (d.sqrMagnitude < r * r) { from = c; return true; }
        }
        return false;
    }
}

/// <summary>
/// The torch. Press T or 4 to take a crafted torch in your hand (T again puts it away).
/// Warm flickering light, flame, drifting embers and a crackle sound. It burns down only while it is in your hand
/// (burnSeconds); when it dies the next torch must be lit. Light-fearing enemies (Shadow Wraith) will not come near.
/// Drives the Animator bool "Torch" (torch idle / walk). Created automatically.
/// </summary>
public class TorchController : MonoBehaviour, IFearLight
{
    public static TorchController Instance { get; private set; }
    public static bool IsHeld { get { return Instance != null && Instance.held; } }

    [Header("Burning")]
    public float burnSeconds = 240f;
    public float scareRadius = 7f;
    [Header("Light")]
    public Color lightColor = new Color(1f, 0.62f, 0.26f);
    public float baseIntensity = 3f;
    public float baseRange = 11f;

    public Vector3 LightPosition { get { return flame != null ? flame.position : transform.position; } }
    public float ScareRadius { get { return scareRadius * Mathf.Lerp(0.6f, 1f, Mathf.Clamp01(remaining / 25f)); } }
    public bool LightActive { get { return held; } }
    public float Remaining { get { return remaining; } }

    static readonly int TorchHash = Animator.StringToHash("Torch");

    Transform model, flame;
    Light lamp;
    ParticleSystem flameFx, emberFx, burst;
    AudioSource loop, sfx;
    AudioClip fireClip, whooshClip, outClip;
    Animator anim;

    bool held;
    float remaining;
    ToolType previous = ToolType.Axe;
    float smoothK = 1f;

    // HUD
    RectTransform hud;
    CanvasGroup hudGroup;
    Image barFill;
    TextMeshProUGUI hudText, toastText;
    float toastUntil;
    int shownSec = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (Instance != null) return;
        new GameObject("TorchController").AddComponent<TorchController>();
    }

    void Awake()
    {
        Instance = this;
        BuildHud();
    }

    void OnEnable() { FearLight.Sources.Add(this); }
    void OnDisable() { FearLight.Sources.Remove(this); }

    // ------------------------------------------------------------------ setup

    void Setup(ToolController tc)
    {
        model = null;
        foreach (Transform t in tc.GetComponentsInChildren<Transform>(true)) if (t.name == "Tool_Torch") { model = t; break; }
        if (model == null) return;
        anim = tc.animator;

        // flame point = just above the top of the torch mesh
        GameObject f = new GameObject("Flame");
        flame = f.transform;
        flame.SetParent(model, false);
        flame.localPosition = new Vector3(0f, 0.17f, 0f);

        lamp = f.AddComponent<Light>();
        lamp.type = LightType.Point;
        lamp.color = lightColor;
        lamp.intensity = baseIntensity;
        lamp.range = baseRange;
        lamp.shadows = LightShadows.None;

        flameFx = MakeParticles(f.transform, "FlameFx", SoftFX.Spark(), true);
        emberFx = MakeParticles(f.transform, "EmberFx", SoftFX.Spark(), false);
        burst = MakeParticles(f.transform, "IgniteBurst", SoftFX.Spark(), false);
        var bm = burst.main; bm.loop = false; bm.playOnAwake = false;
        var be = burst.emission; be.enabled = false;

        fireClip = MakeFire();
        whooshClip = MakeWhoosh(false);
        outClip = MakeWhoosh(true);
        loop = f.AddComponent<AudioSource>();
        loop.clip = fireClip; loop.loop = true; loop.spatialBlend = 0f; loop.volume = 0.22f; loop.playOnAwake = false;
        sfx = gameObject.AddComponent<AudioSource>();   // on this always-active object so the 'put out' sound still plays
        sfx.spatialBlend = 0f; sfx.playOnAwake = false;

        model.gameObject.SetActive(false);
    }

    ParticleSystem MakeParticles(Transform parent, string name, Material mat, bool isFlame)
    {
        GameObject g = new GameObject(name);
        g.transform.SetParent(parent, false);
        ParticleSystem ps = g.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Local;
        main.playOnAwake = true;
        main.loop = true;
        main.maxParticles = 120;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient gr = new Gradient();
        var size = ps.sizeOverLifetime;
        size.enabled = true;
        var em = ps.emission;

        if (isFlame)
        {
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.28f, 0.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.45f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.13f, 0.2f);
            main.gravityModifier = -0.15f;
            main.startColor = new Color(1f, 0.8f, 0.35f, 0.9f);
            em.rateOverTime = 34f;
            shape.angle = 8f; shape.radius = 0.012f;
            gr.SetKeys(
                new[] { new GradientColorKey(new Color(1f, 0.95f, 0.6f), 0f), new GradientColorKey(new Color(1f, 0.5f, 0.12f), 0.45f), new GradientColorKey(new Color(0.55f, 0.12f, 0.02f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.12f), new GradientAlphaKey(0.45f, 0.65f), new GradientAlphaKey(0f, 1f) });
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.55f), new Keyframe(0.25f, 1f), new Keyframe(1f, 0.05f)));
        }
        else
        {
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.018f, 0.04f);
            main.gravityModifier = -0.08f;
            main.startColor = new Color(1f, 0.72f, 0.28f, 1f);
            em.rateOverTime = 7f;
            shape.angle = 28f; shape.radius = 0.02f;
            gr.SetKeys(
                new[] { new GradientColorKey(new Color(1f, 0.85f, 0.45f), 0f), new GradientColorKey(new Color(1f, 0.35f, 0.08f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) });
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.2f)));
        }
        col.color = new ParticleSystem.MinMaxGradient(gr);

        ParticleSystemRenderer pr = g.GetComponent<ParticleSystemRenderer>();
        pr.material = mat;
        pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        pr.receiveShadows = false;
        ps.Play();
        return ps;
    }

    // ------------------------------------------------------------------ update

    void Update()
    {
        ToolController tc = ToolController.Instance;
        if (tc == null) return;
        if (model == null) { Setup(tc); if (model == null) return; }

        Keyboard kb = Keyboard.current;
        bool canAct = kb != null && Cursor.lockState == CursorLockMode.Locked && !CraftingUI.IsOpen;
        if (canAct)
        {
            if (kb.tKey.wasPressedThisFrame) { if (held) Stow(tc, false); else TryLight(tc); }
            else if (kb.digit4Key.wasPressedThisFrame && !held) TryLight(tc);
        }

        // took the axe / pickaxe / sword again
        if (held && tc.CurrentTool != ToolType.None) SetHeld(false);

        if (anim != null) anim.SetBool(TorchHash, held);

        if (held)
        {
            remaining -= Time.deltaTime;
            if (remaining <= 0f)
            {
                remaining = 0f;
                Stow(tc, true);
                Toast("YOUR TORCH BURNED OUT");
            }
        }
        Animate();
        UpdateHud();
    }

    void TryLight(ToolController tc)
    {
        if (remaining <= 0f)
        {
            CraftingState cs = CraftingState.Instance;
            if (cs == null || cs.Count(CraftItem.Torch) <= 0)
            {
                Toast("NO TORCH  ·  CRAFT ONE AT A WORKBENCH");
                return;
            }
            if (!cs.Consume(CraftItem.Torch)) return;
            remaining = burnSeconds;
        }
        if (tc.CurrentTool != ToolType.None) previous = tc.CurrentTool;
        tc.Equip(ToolType.None);
        SetHeld(true);
        if (sfx != null) sfx.PlayOneShot(whooshClip, 0.7f);
        if (burst != null) burst.Emit(26);
    }

    void Stow(ToolController tc, bool burnedOut)
    {
        SetHeld(false);
        tc.Equip(previous == ToolType.None ? ToolType.Axe : previous);
        if (sfx != null) sfx.PlayOneShot(outClip, burnedOut ? 0.8f : 0.45f);
        if (burnedOut && burst != null) { }
    }

    void SetHeld(bool on)
    {
        held = on;
        if (model != null) model.gameObject.SetActive(on);
        if (loop != null)
        {
            if (on) loop.Play(); else loop.Stop();
        }
        if (on)
        {
            if (flameFx != null) flameFx.Play();
            if (emberFx != null) emberFx.Play();
        }
        smoothK = 1f;
    }

    void Animate()
    {
        if (!held || lamp == null) return;
        float t = Time.time;
        float n = Mathf.PerlinNoise(t * 7f, 0.3f);
        float m = Mathf.PerlinNoise(t * 19f, 7.1f);
        float dying = Mathf.Clamp01(remaining / 25f);
        float k = (0.78f + 0.42f * n + 0.16f * m) * Mathf.Lerp(0.4f, 1f, dying);
        if (dying < 1f && Random.value < 0.05f) k *= 0.3f;   // stutter when nearly out
        smoothK = Mathf.Lerp(smoothK, k, 1f - Mathf.Exp(-25f * Time.deltaTime));
        lamp.intensity = baseIntensity * smoothK;
        lamp.range = baseRange * (0.92f + 0.14f * n) * Mathf.Lerp(0.7f, 1f, dying);
        if (loop != null) loop.volume = 0.22f * Mathf.Lerp(0.4f, 1f, dying) * (0.85f + 0.3f * m);

        flameFx.transform.localScale = Vector3.one * (Mathf.Lerp(0.55f, 1f, dying) * (0.9f + 0.2f * n));
        var fe = flameFx.emission;
        fe.rateOverTime = 34f * Mathf.Lerp(0.5f, 1f, dying);
    }

    // ------------------------------------------------------------------ HUD

    void BuildHud()
    {
        RectTransform canvas = CombatSkin.MakeCanvas(transform, "TorchCanvas", 7);

        GameObject go = new GameObject("TorchHud", typeof(RectTransform));
        hud = (RectTransform)go.transform;
        hud.SetParent(canvas, false);
        CombatSkin.Place(hud, new Vector2(0.5f, 0f), new Vector2(0f, 190f), new Vector2(260f, 40f), new Vector2(0.5f, 0f));
        hudGroup = go.AddComponent<CanvasGroup>();
        hudGroup.blocksRaycasts = false;
        hudGroup.alpha = 0f;

        hudText = CombatSkin.Text(hud, "TORCH", 15f, new Color(1f, 0.8f, 0.45f), TextAlignmentOptions.Center);
        hudText.characterSpacing = 5f;
        CombatSkin.Place(hudText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 0f), new Vector2(260f, 22f), new Vector2(0.5f, 1f));

        Image frame = CombatSkin.Frame(hud, 232f, 14f);
        CombatSkin.Place(frame.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(232f, 14f), new Vector2(0.5f, 0f));
        barFill = CombatSkin.Fill(frame.rectTransform, 220f, 8f, new Color(1f, 0.6f, 0.2f));

        toastText = CombatSkin.Text(canvas, "", 24f, new Color(1f, 0.82f, 0.5f), TextAlignmentOptions.Center);
        toastText.characterSpacing = 5f;
        CombatSkin.Place(toastText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 255f), new Vector2(900f, 40f), new Vector2(0.5f, 0f));
        toastText.alpha = 0f;
    }

    void UpdateHud()
    {
        float target = held ? 1f : 0f;
        hudGroup.alpha = Mathf.MoveTowards(hudGroup.alpha, target, Time.unscaledDeltaTime * 5f);
        if (held)
        {
            float f = Mathf.Clamp01(remaining / burnSeconds);
            barFill.fillAmount = f;
            barFill.color = f < 0.15f ? Color.Lerp(new Color(0.9f, 0.15f, 0.1f), new Color(1f, 0.6f, 0.2f), f / 0.15f) : new Color(1f, 0.6f, 0.2f);
            int sec = Mathf.CeilToInt(remaining);
            if (sec != shownSec)
            {
                shownSec = sec;
                hudText.text = "TORCH  ·  " + (sec / 60) + ":" + (sec % 60).ToString("00");
            }
        }
        float ta = Time.unscaledTime < toastUntil ? 1f : 0f;
        toastText.alpha = Mathf.MoveTowards(toastText.alpha, ta, Time.unscaledDeltaTime * (ta > 0f ? 8f : 2f));
    }

    void Toast(string text)
    {
        toastText.text = text;
        toastUntil = Time.unscaledTime + 2.2f;
        toastText.alpha = 1f;
    }

    // ------------------------------------------------------------------ generated sounds

    static AudioClip MakeFire()
    {
        const int sr = 22050;
        int n = sr * 3;
        float[] d = new float[n];
        System.Random r = new System.Random(11);
        float lp = 0f, pop = 0f, lp2 = 0f;
        for (int i = 0; i < n; i++)
        {
            float w = (float)(r.NextDouble() * 2.0 - 1.0);
            lp += (w - lp) * 0.05f;
            lp2 += (lp - lp2) * 0.2f;
            if (r.NextDouble() < 0.0012) pop = 0.5f + (float)r.NextDouble() * 0.5f;
            pop *= 0.985f;
            float hiss = (w - lp) * 0.05f;
            d[i] = lp2 * 1.1f + hiss + w * pop * 0.35f;
        }
        int fade = 2000;
        for (int i = 0; i < fade; i++)
        {
            float a = i / (float)fade;
            d[i] *= a;
            d[n - 1 - i] *= a;
        }
        AudioClip c = AudioClip.Create("TorchFire", n, 1, sr, false);
        c.SetData(d, 0);
        return c;
    }

    static AudioClip MakeWhoosh(bool down)
    {
        const int sr = 22050;
        int n = (int)(sr * (down ? 0.45f : 0.55f));
        float[] d = new float[n];
        System.Random r = new System.Random(down ? 5 : 3);
        float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)n;
            float env = down ? Mathf.Pow(1f - t, 2f) * Mathf.Min(1f, t * 30f) : Mathf.Sin(t * Mathf.PI) * (1f - t * 0.4f);
            float cut = down ? Mathf.Lerp(0.35f, 0.04f, t) : Mathf.Lerp(0.03f, 0.4f, t);
            float w = (float)(r.NextDouble() * 2.0 - 1.0);
            lp += (w - lp) * cut;
            d[i] = lp * env * 1.4f;
        }
        AudioClip c = AudioClip.Create(down ? "TorchOut" : "TorchLight", n, 1, sr, false);
        c.SetData(d, 0);
        return c;
    }
}
