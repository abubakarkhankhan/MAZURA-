using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Extra combat juice. Created automatically when the game starts.
///   - golden sword trail while the weapon swings
///   - a bright slash streak on every hit (crescent sprite that flashes and fades)
///   - a short slow-motion beat + big shake when something dies near the player
///   - a death burst of embers and dark smoke
/// </summary>
public class CombatPolish : MonoBehaviour
{
    static CombatPolish instance;

    TrailRenderer trail;
    float trailOnAt, trailOffAt;
    Sprite slashSprite;
    Material sparkMat, puffMat;
    Coroutine slowRoutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (instance != null) return;
        instance = new GameObject("CombatPolish").AddComponent<CombatPolish>();
    }

    void Awake()
    {
        slashSprite = MakeSlashSprite();
        Shader pu = Shader.Find("Mazura/ParticleUnlit");
        if (pu != null)
        {
            sparkMat = SoftFX.Spark(); puffMat = SoftFX.Puff();
        }
    }

    void OnEnable()
    {
        ToolController.OnSwingStart += OnSwingStart;
        EnemyBase.OnAnyDamaged += OnDamaged;
        EnemyBase.OnAnyDied += OnDied;
    }

    void OnDisable()
    {
        ToolController.OnSwingStart -= OnSwingStart;
        EnemyBase.OnAnyDamaged -= OnDamaged;
        EnemyBase.OnAnyDied -= OnDied;
    }

    // ------------------------------------------------------------------ trail

    void OnSwingStart(ToolType tool)
    {
        if (tool != ToolType.Weapon) return;
        EnsureTrail();
        if (trail == null) return;
        trail.Clear();
        trail.emitting = false;
        trailOnAt = Time.time + 0.16f;
        trailOffAt = Time.time + 0.5f;
    }

    void EnsureTrail()
    {
        if (trail != null) return;
        ToolController tc = ToolController.Instance;
        if (tc == null || tc.weaponModel == null) return;

        // the tip of the blade = the point of the model's bounds that is farthest from its pivot
        Renderer[] rs = tc.weaponModel.GetComponentsInChildren<Renderer>(true);
        if (rs.Length == 0) return;
        Bounds b = rs[0].bounds;
        foreach (Renderer r in rs) b.Encapsulate(r.bounds);
        Vector3 pivot = tc.weaponModel.transform.position;
        Vector3 best = b.center;
        float bestD = -1f;
        for (int i = 0; i < 8; i++)
        {
            Vector3 c = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            float d = (c - pivot).sqrMagnitude;
            if (d > bestD) { bestD = d; best = c; }
        }

        GameObject tip = new GameObject("SwordTrailTip");
        tip.transform.SetParent(tc.weaponModel.transform, true);
        tip.transform.position = Vector3.Lerp(pivot, best, 0.92f);

        trail = tip.AddComponent<TrailRenderer>();
        trail.time = 0.2f;
        trail.minVertexDistance = 0.03f;
        trail.emitting = false;
        trail.shadowCastingMode = ShadowCastingMode.Off;
        trail.receiveShadows = false;
        trail.widthCurve = new AnimationCurve(new Keyframe(0f, 0.16f), new Keyframe(1f, 0f));
        Gradient g = new Gradient();
        g.SetKeys(new GradientColorKey[] { new GradientColorKey(new Color(1f, 0.95f, 0.75f), 0f), new GradientColorKey(new Color(1f, 0.6f, 0.2f), 1f) },
                  new GradientAlphaKey[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
        trail.colorGradient = g;
        Shader s = Shader.Find("Sprites/Default");
        if (s != null) trail.sharedMaterial = new Material(s);
    }

    void Update()
    {
        if (trail != null) trail.emitting = Time.time >= trailOnAt && Time.time <= trailOffAt;
    }

    // ------------------------------------------------------------------ slash streak

    void OnDamaged(EnemyBase e, float amount)
    {
        if (e == null) return;
        Collider c = e.GetComponentInChildren<Collider>();
        Vector3 pos = c != null ? new Vector3(c.bounds.center.x, c.bounds.min.y + c.bounds.size.y * 0.65f, c.bounds.center.z) : e.transform.position + Vector3.up * 1.2f;
        StartCoroutine(SlashStreak(pos, amount >= 40f));
    }

    IEnumerator SlashStreak(Vector3 pos, bool big)
    {
        GameObject go = new GameObject("SlashStreak");
        go.transform.position = pos;
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = slashSprite;
        sr.sortingOrder = 50;
        float roll = Random.Range(-55f, 55f);
        float size = big ? 1.35f : 1.0f;
        float t = 0f;
        const float life = 0.14f;
        while (t < life)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / life);
            Camera cam = Camera.main;
            if (cam != null) go.transform.rotation = cam.transform.rotation * Quaternion.Euler(0f, 0f, roll);
            go.transform.localScale = Vector3.one * size * Mathf.Lerp(0.55f, 1.15f, 1f - (1f - k) * (1f - k));
            Color col = big ? new Color(1f, 0.6f, 0.25f) : new Color(1f, 0.95f, 0.8f);
            col.a = 0.75f * (1f - k * k);
            sr.color = col;
            yield return null;
        }
        Destroy(go);
    }

    /// <summary>A thin bright crescent, fat in the middle and pointed at both ends.</summary>
    static Sprite MakeSlashSprite()
    {
        const int w = 128, h = 64;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = x / (w - 1f) * 2f - 1f;               // -1..1 along the slash
                float v = y / (h - 1f) * 2f - 1f;               // -1..1 across
                float center = -0.55f * (1f - u * u) + 0.0f;   // curved spine
                float thick = 0.34f * (1f - Mathf.Abs(u) * Mathf.Abs(u)) + 0.02f;
                float d = Mathf.Abs(v - center) / thick;
                float a = Mathf.Clamp01(1f - d);
                a = a * a * (3f - 2f * a);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 64f);
    }

    // ------------------------------------------------------------------ kills

    void OnDied(EnemyBase e)
    {
        if (e == null) return;
        ToolController tc = ToolController.Instance;
        float dist = tc != null ? Vector3.Distance(tc.transform.position, e.transform.position) : 99f;
        Vector3 pos = e.transform.position + Vector3.up * 1.0f;

        if (sparkMat != null) Burst(pos, Vector3.up, sparkMat, new Color(1f, 0.55f, 0.2f), 40, 1.5f, 5f, 0.04f, 0.09f, 1.3f, -0.15f, 70f);
        if (puffMat != null) Burst(pos, Vector3.up, puffMat, new Color(0.08f, 0.04f, 0.05f, 0.7f), 10, 0.4f, 1.4f, 0.5f, 1.0f, 1.4f, -0.05f, 80f);

        if (dist < 14f)
        {
            CameraShake cs = CameraShake.Instance;
            if (cs != null) cs.Shake(Mathf.Lerp(0.12f, 0.05f, dist / 14f), 0.35f);
            if (slowRoutine != null) StopCoroutine(slowRoutine);
            slowRoutine = StartCoroutine(SlowMo(0.3f, 0.18f));
        }
    }

    IEnumerator SlowMo(float scale, float realSeconds)
    {
        Time.timeScale = scale;
        yield return new WaitForSecondsRealtime(realSeconds);
        float t = 0f;
        while (t < 0.12f)
        {
            t += Time.unscaledDeltaTime;
            Time.timeScale = Mathf.Lerp(scale, 1f, t / 0.12f);
            yield return null;
        }
        Time.timeScale = 1f;
        slowRoutine = null;
    }

    static void Burst(Vector3 pos, Vector3 dir, Material mat, Color color, int count, float speedMin, float speedMax,
                      float sizeMin, float sizeMax, float life, float gravity, float angle)
    {
        GameObject go = new GameObject("DeathFX");
        go.transform.position = pos;
        go.transform.rotation = Quaternion.LookRotation(dir);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.duration = 0.6f; main.loop = false; main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.6f, life);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
        main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
        main.startColor = color;
        main.gravityModifier = gravity;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = count + 2;
        var em = ps.emission; em.rateOverTime = 0f;
        em.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, (short)count) });
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = angle; sh.radius = 0.3f;
        var sol = ps.sizeOverLifetime; sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.1f));
        Gradient g = new Gradient();
        g.SetKeys(new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        var col = ps.colorOverLifetime; col.enabled = true; col.color = g;
        ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
        ps.Play();
        Destroy(go, life + 1f);
    }
}

/// <summary>Round, soft-edged particle materials (URP Particles/Unlit) shared by the combat effects.</summary>
public static class SoftFX
{
    static Texture2D dot;
    static Material puff, spark;

    static Texture2D Dot()
    {
        if (dot != null) return dot;
        const int s = 64;
        dot = new Texture2D(s, s, TextureFormat.RGBA32, false);
        dot.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float dx = (x / (s - 1f)) * 2f - 1f, dy = (y / (s - 1f)) * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - d);
                a = a * a * (3f - 2f * a);
                dot.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        dot.Apply();
        return dot;
    }

    static Material Make(float srcBlend, float dstBlend)
    {
        Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (sh == null) return null;
        Material m = new Material(sh);
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.SetFloat("_SrcBlend", srcBlend);
        m.SetFloat("_DstBlend", dstBlend);
        m.SetFloat("_ZWrite", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = 3000;
        m.SetOverrideTag("RenderType", "Transparent");
        m.SetTexture("_BaseMap", Dot());
        m.SetColor("_BaseColor", Color.white);
        return m;
    }

    /// <summary>Soft alpha-blended puff (smoke, blood mist).</summary>
    public static Material Puff() { if (puff == null) puff = Resources.Load<Material>("Combat/FX_Puff"); if (puff == null) puff = Make(5f, 10f); return puff; }
    /// <summary>Soft additive glow (sparks, embers).</summary>
    public static Material Spark() { if (spark == null) spark = Resources.Load<Material>("Combat/FX_Spark"); if (spark == null) spark = Make(5f, 1f); return spark; }
}
