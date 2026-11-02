using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Atmosphere particles for Mazura. Everything follows the camera and is created at game start:
///  - Dust / pollen motes (day, warm sunlight colour)
///  - Fireflies (evening and night, glow through Bloom)
///  - Drifting leaves (day)
///  - Low ground mist (stronger at night and early morning)
/// It reads the DayNightManager, so the effects fade in and out with the time of day.
/// Put this script on one empty GameObject (for example "AtmosphereFX").
/// </summary>
public class AtmosphereFX : MonoBehaviour
{
    [Header("Materials (Assets/Materials/Sky). Glow = additive, Soft = alpha blended")]
    public Material glowMaterial;
    public Material softMaterial;

    [Header("References (found automatically when empty)")]
    public DayNightManager dayNight;
    public Light sunLight;
    public Camera cam;

    [Header("Effects")]
    public bool dust = true;
    public bool fireflies = true;
    public bool leaves = true;
    public bool mist = true;
    [Range(0f, 2f)] public float amount = 1f;
    [Tooltip("Size of the box around the camera where particles appear (x, y, z).")]
    public Vector3 area = new Vector3(36f, 10f, 36f);

    Transform root;
    ParticleSystem motes, flies, leafPs, mistPs;
    Material motesMat, fliesMat, leafMat, mistMat;
    float motesRate, fliesRate, leafRate, mistRate;

    void Start()
    {
        if (dayNight == null) dayNight = FindAnyObjectByType<DayNightManager>();
        if (sunLight == null) sunLight = RenderSettings.sun;
        if (cam == null) cam = Camera.main;

        Shader sh = Shader.Find("Mazura/ParticleSoft");
        if (glowMaterial == null && sh != null) glowMaterial = MakeFallback(sh, BlendMode.SrcAlpha, BlendMode.One);
        if (softMaterial == null && sh != null) softMaterial = MakeFallback(sh, BlendMode.SrcAlpha, BlendMode.OneMinusSrcAlpha);
        if (glowMaterial == null || softMaterial == null || cam == null)
        {
            Debug.LogError("AtmosphereFX: missing materials or camera. Effects disabled.");
            enabled = false;
            return;
        }

        Texture2D softTex = MakeSoftTexture(64);
        Texture2D leafTex = MakeLeafTexture(64);

        motesMat = new Material(glowMaterial) { mainTexture = softTex };
        motesMat.SetFloat("_Intensity", 1.4f);
        fliesMat = new Material(glowMaterial) { mainTexture = softTex };
        fliesMat.SetFloat("_Intensity", 6f);
        leafMat = new Material(softMaterial) { mainTexture = leafTex };
        leafMat.SetFloat("_Intensity", 1f);
        mistMat = new Material(softMaterial) { mainTexture = softTex };
        mistMat.SetFloat("_NearFade", 5f);

        root = new GameObject("AtmosphereFX_Runtime").transform;
        root.position = cam.transform.position;

        if (dust) BuildMotes();
        if (fireflies) BuildFireflies();
        if (leaves) BuildLeaves();
        if (mist) BuildMist();
    }

    void LateUpdate()
    {
        if (root == null) return;
        if (cam == null) cam = Camera.main;
        if (cam == null) return;
        root.position = cam.transform.position;

        // 0 = night, 1 = full day
        float dayF = 0.5f;
        if (sunLight != null) dayF = Mathf.InverseLerp(0.12f, 0.5f, sunLight.intensity);
        float nightF = 1f - dayF;

        Color sunTint = sunLight != null ? Color.Lerp(Color.white, sunLight.color, 0.55f) : Color.white;
        Color fog = Color.Lerp(RenderSettings.fogColor, Color.white, 0.3f);

        Drive(motes, motesMat, motesRate, (0.15f + 0.85f * dayF) * amount, sunTint);
        Drive(flies, fliesMat, fliesRate, Mathf.Clamp01(nightF * 1.3f) * amount, new Color(0.75f, 1f, 0.35f));
        Drive(leafPs, leafMat, leafRate, dayF * amount, Color.white);
        Drive(mistPs, mistMat, mistRate, (0.45f + 0.55f * nightF) * amount, new Color(fog.r, fog.g, fog.b));
    }

    // Fades the whole system through the material tint and scales how many new particles are born.
    void Drive(ParticleSystem ps, Material m, float baseRate, float f, Color tint)
    {
        if (ps == null) return;
        f = Mathf.Clamp01(f);
        m.SetColor("_Color", new Color(tint.r, tint.g, tint.b, f));
        ParticleSystem.EmissionModule em = ps.emission;
        em.rateOverTime = baseRate * f;
    }

    void OnDestroy()
    {
        if (root != null) Destroy(root.gameObject);
    }

    // ---------------------------------------------------------------- systems

    void BuildMotes()
    {
        motes = MakeSystem("Motes", motesMat, 700, area, Vector3.zero);
        motesRate = 45f;
        ParticleSystem.MainModule m = motes.main;
        m.startLifetime = new ParticleSystem.MinMaxCurve(9f, 15f);
        m.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.09f);
        m.startColor = new Color(1f, 1f, 1f, 0.8f);
        SetNoise(motes, 0.35f, 0.25f);
        SetFade(motes, 0.25f, 0.3f);
        Prewarm(motes, 12f);
    }

    void BuildFireflies()
    {
        flies = MakeSystem("Fireflies", fliesMat, 160, new Vector3(area.x * 0.8f, 3.5f, area.z * 0.8f), new Vector3(0f, -0.4f, 0f));
        fliesRate = 7f;
        ParticleSystem.MainModule m = flies.main;
        m.startLifetime = new ParticleSystem.MinMaxCurve(10f, 18f);
        m.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.2f);
        m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.7f, 1f, 0.3f, 1f), new Color(1f, 0.9f, 0.4f, 1f));
        SetNoise(flies, 1.6f, 0.35f);

        // blinking glow
        Gradient g = new Gradient();
        g.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(0.1f, 0.25f),
                new GradientAlphaKey(1f, 0.4f), new GradientAlphaKey(0.15f, 0.58f), new GradientAlphaKey(1f, 0.75f),
                new GradientAlphaKey(0.2f, 0.88f), new GradientAlphaKey(0f, 1f)
            });
        ParticleSystem.ColorOverLifetimeModule col = flies.colorOverLifetime;
        col.enabled = true;
        col.color = g;
        Prewarm(flies, 14f);
    }

    void BuildLeaves()
    {
        leafPs = MakeSystem("Leaves", leafMat, 90, new Vector3(area.x, 2f, area.z), new Vector3(0f, 5f, 0f));
        leafRate = 2.5f;
        ParticleSystem.MainModule m = leafPs.main;
        m.startLifetime = new ParticleSystem.MinMaxCurve(12f, 18f);
        m.startSize = new ParticleSystem.MinMaxCurve(0.14f, 0.28f);
        m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        m.gravityModifier = 0.05f;
        m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.35f, 0.5f, 0.15f, 1f), new Color(0.65f, 0.5f, 0.15f, 1f));
        SetNoise(leafPs, 0.9f, 0.25f);
        ParticleSystem.RotationOverLifetimeModule rot = leafPs.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-1.6f, 1.6f);
        SetFade(leafPs, 0.1f, 0.15f);
        Prewarm(leafPs, 10f);
    }

    void BuildMist()
    {
        mistPs = MakeSystem("Mist", mistMat, 60, new Vector3(area.x, 0.8f, area.z), new Vector3(0f, -1.3f, 0f));
        mistRate = 1.6f;
        ParticleSystem.MainModule m = mistPs.main;
        m.startLifetime = new ParticleSystem.MinMaxCurve(15f, 22f);
        m.startSize = new ParticleSystem.MinMaxCurve(5f, 10f);
        m.startColor = new Color(1f, 1f, 1f, 0.12f);
        m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        SetNoise(mistPs, 0.5f, 0.12f);
        SetFade(mistPs, 0.3f, 0.35f);
        Prewarm(mistPs, 16f);
    }

    // ---------------------------------------------------------------- helpers

    ParticleSystem MakeSystem(string objName, Material mat, int max, Vector3 box, Vector3 localPos)
    {
        GameObject go = new GameObject(objName);
        go.transform.SetParent(root, false);
        go.transform.localPosition = localPos;
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule m = ps.main;
        m.loop = true;
        m.playOnAwake = true;
        m.simulationSpace = ParticleSystemSimulationSpace.World;
        m.maxParticles = max;
        m.startSpeed = 0f;

        ParticleSystem.ShapeModule sh = ps.shape;
        sh.enabled = true;
        sh.shapeType = ParticleSystemShapeType.Box;
        sh.scale = box;

        ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        return ps;
    }

    void SetNoise(ParticleSystem ps, float strength, float frequency)
    {
        ParticleSystem.NoiseModule n = ps.noise;
        n.enabled = true;
        n.strength = strength;
        n.frequency = frequency;
        n.scrollSpeed = 0.2f;
        n.damping = true;
        n.quality = ParticleSystemNoiseQuality.Low;
    }

    void SetFade(ParticleSystem ps, float fadeIn, float fadeOut)
    {
        Gradient g = new Gradient();
        g.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, fadeIn),
                new GradientAlphaKey(1f, 1f - fadeOut), new GradientAlphaKey(0f, 1f)
            });
        ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = g;
    }

    // Fills the area right away so the effect does not start empty.
    void Prewarm(ParticleSystem ps, float seconds)
    {
        ParticleSystem.EmissionModule em = ps.emission;
        float old = em.rateOverTime.constant;
        em.rateOverTime = (ps == flies ? fliesRate : ps == motes ? motesRate : ps == leafPs ? leafRate : mistRate) * 0.6f;
        ps.Simulate(seconds, true, true);
        ps.Play();
        em.rateOverTime = old;
    }

    Material MakeFallback(Shader sh, BlendMode src, BlendMode dst)
    {
        Material m = new Material(sh);
        m.SetFloat("_SrcBlend", (float)src);
        m.SetFloat("_DstBlend", (float)dst);
        return m;
    }

    // Round soft dot.
    Texture2D MakeSoftTexture(int size)
    {
        Texture2D t = new Texture2D(size, size, TextureFormat.RGBA32, false);
        t.wrapMode = TextureWrapMode.Clamp;
        Color[] px = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f;
                float v = (y + 0.5f) / size * 2f - 1f;
                float d = Mathf.Clamp01(1f - Mathf.Sqrt(u * u + v * v));
                float a = d * d * (3f - 2f * d);
                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        }
        t.SetPixels(px);
        t.Apply();
        return t;
    }

    // Simple leaf shape with a lighter middle line.
    Texture2D MakeLeafTexture(int size)
    {
        Texture2D t = new Texture2D(size, size, TextureFormat.RGBA32, false);
        t.wrapMode = TextureWrapMode.Clamp;
        Color[] px = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f;
                float v = (y + 0.5f) / size * 2f - 1f;
                float e = (u * u) / 0.22f + (v * v) / 0.95f;
                float a = 1f - Mathf.SmoothStep(0.75f, 1f, e);
                float rib = 1f - Mathf.Clamp01(Mathf.Abs(u) * 14f);
                float c = Mathf.Lerp(0.8f, 1f, rib);
                px[y * size + x] = new Color(c, c, c, a);
            }
        }
        t.SetPixels(px);
        t.Apply();
        return t;
    }
}
