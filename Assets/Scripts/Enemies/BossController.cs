using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Extra behaviour for a boss (put it next to EnemyBase on the boss prefab).
///   - Slam telegraph: when the wind-up starts, a red ring on the ground grows to the slam radius,
///     so the player sees exactly where NOT to stand. On impact: white flash, shockwave ring, dust, big shake.
///   - Phase 2 at half health: roars, glows hotter, moves and swings faster, calls minions.
///   - Death: "<NAME> SLAIN" banner and a bigger loot drop.
/// The boss gets its own copy of its EnemyData, so phase 2 never changes the asset.
/// </summary>
[RequireComponent(typeof(EnemyBase))]
public class BossController : MonoBehaviour
{
    [Header("Phase 2 (below 50% health)")]
    public float phase2SpeedMultiplier = 1.3f;
    public float phase2WindupMultiplier = 0.75f;
    [ColorUsage(false, true)] public Color phase2Glow = new Color(1f, 0.35f, 0.05f);
    public GameObject minionPrefab;
    public int minionCount = 2;

    [Header("Reward")]
    public ItemType rewardItem = ItemType.Ruby;
    public int rewardAmount = 3;

    EnemyBase enemy;
    bool phase2;
    Material ringMat;
    static Texture2D ringTex, waveTex;

    public bool InPhase2 { get { return phase2; } }

    void Awake()
    {
        enemy = GetComponent<EnemyBase>();
        if (enemy.data != null) enemy.data = Instantiate(enemy.data);   // private copy, safe to change
        Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (sh != null)
        {
            ringMat = new Material(sh);
            ringMat.SetFloat("_Surface", 1f);
            ringMat.SetFloat("_SrcBlend", 5f);
            ringMat.SetFloat("_DstBlend", 1f);   // additive glow
            ringMat.SetFloat("_ZWrite", 0f);
            ringMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            ringMat.SetOverrideTag("RenderType", "Transparent");
            ringMat.renderQueue = 3001;
        }
    }

    void OnEnable()
    {
        enemy.OnAttackStart += OnAttackStart;
        enemy.OnSlam += OnSlam;
        enemy.OnHalfHealth += OnHalf;
        EnemyBase.OnAnyDied += OnDied;
    }

    void OnDisable()
    {
        enemy.OnAttackStart -= OnAttackStart;
        enemy.OnSlam -= OnSlam;
        enemy.OnHalfHealth -= OnHalf;
        EnemyBase.OnAnyDied -= OnDied;
    }

    // ------------------------------------------------------------------ slam telegraph

    void OnAttackStart(EnemyBase e)
    {
        if (e.data.slamRadius <= 0f || ringMat == null) return;
        StartCoroutine(Telegraph(e.data.slamRadius, e.data.attackWindup));
    }

    IEnumerator Telegraph(float radius, float windup)
    {
        GameObject ring = MakeDecal("SlamTelegraph", RingTexture());
        GameObject disc = MakeDecal("SlamArea", WaveTexture());
        MaterialPropertyBlock mpb = new MaterialPropertyBlock();
        Renderer rr = ring.GetComponent<Renderer>(), dr = disc.GetComponent<Renderer>();
        float t = 0f;
        while (t < windup && !enemy.IsDead)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / windup);
            Vector3 c = Ground(transform.position);
            ring.transform.position = c; disc.transform.position = c;
            // the outer ring sits at the full radius and brightens; the inner fill grows toward it
            ring.transform.localScale = Vector3.one * radius * 2f;
            disc.transform.localScale = Vector3.one * radius * 2f * Mathf.Lerp(0.1f, 1f, k * k);
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * Mathf.Lerp(8f, 26f, k));
            mpb.SetColor("_BaseColor", new Color(1f, 0.15f, 0.08f, Mathf.Lerp(0.35f, 1f, k) * pulse));
            rr.SetPropertyBlock(mpb);
            mpb.SetColor("_BaseColor", new Color(1f, 0.1f, 0.05f, 0.35f * k));
            dr.SetPropertyBlock(mpb);
            yield return null;
        }
        Destroy(ring);
        Destroy(disc);
    }

    void OnSlam(EnemyBase e)
    {
        if (ringMat != null) StartCoroutine(Shockwave(e.data.slamRadius));
        CombatPolishBurst(Ground(transform.position) + Vector3.up * 0.2f, e.data.slamRadius);
    }

    IEnumerator Shockwave(float radius)
    {
        GameObject wave = MakeDecal("Shockwave", RingTexture());
        Renderer r = wave.GetComponent<Renderer>();
        MaterialPropertyBlock mpb = new MaterialPropertyBlock();
        Vector3 c = Ground(transform.position);
        float t = 0f;
        const float life = 0.45f;
        while (t < life)
        {
            t += Time.deltaTime;
            float k = t / life;
            wave.transform.position = c;
            wave.transform.localScale = Vector3.one * radius * 2f * Mathf.Lerp(0.6f, 1.5f, 1f - (1f - k) * (1f - k));
            mpb.SetColor("_BaseColor", new Color(1f, 0.85f, 0.6f, 1f - k));
            r.SetPropertyBlock(mpb);
            yield return null;
        }
        Destroy(wave);
    }

    static void CombatPolishBurst(Vector3 pos, float radius)
    {
        Material puff = SoftFX.Puff();
        if (puff == null) return;
        GameObject go = new GameObject("SlamDust");
        go.transform.position = pos;
        go.transform.rotation = Quaternion.LookRotation(Vector3.up);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.duration = 0.5f; main.loop = false; main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(radius * 0.8f, radius * 1.6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.3f);
        main.startColor = new Color(0.45f, 0.4f, 0.32f, 0.6f);
        main.gravityModifier = -0.02f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 40;
        var em = ps.emission; em.rateOverTime = 0f;
        em.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 34) });
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 0.6f; sh.radiusThickness = 0f;
        var vel = ps.limitVelocityOverLifetime; vel.enabled = true; vel.limit = 1.2f; vel.dampen = 0.15f;
        var sol = ps.sizeOverLifetime; sol.enabled = true; sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.8f));
        Gradient g = new Gradient();
        g.SetKeys(new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        var col = ps.colorOverLifetime; col.enabled = true; col.color = g;
        ParticleSystemRenderer pr = go.GetComponent<ParticleSystemRenderer>();
        pr.sharedMaterial = puff; pr.shadowCastingMode = ShadowCastingMode.Off;
        ps.Play();
        Destroy(go, 2.5f);
    }

    GameObject MakeDecal(string name, Texture2D tex)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = name;
        Destroy(go.GetComponent<Collider>());
        go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        Renderer r = go.GetComponent<Renderer>();
        Material m = new Material(ringMat);
        m.SetTexture("_BaseMap", tex);
        r.sharedMaterial = m;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        Destroy(m, 5f);
        return go;
    }

    static Vector3 Ground(Vector3 p)
    {
        RaycastHit hit;
        if (Physics.Raycast(p + Vector3.up * 1.5f, Vector3.down, out hit, 5f, ~(1 << 8), QueryTriggerInteraction.Ignore) && hit.collider.GetComponentInParent<EnemyBase>() == null)
            return hit.point + Vector3.up * 0.04f;
        return new Vector3(p.x, p.y + 0.04f, p.z);
    }

    /// <summary>A crisp ring with a soft glow, at the edge of the texture.</summary>
    static Texture2D RingTexture()
    {
        if (ringTex != null) return ringTex;
        const int s = 256;
        ringTex = new Texture2D(s, s, TextureFormat.RGBA32, true);
        ringTex.wrapMode = TextureWrapMode.Clamp;
        float c = (s - 1) * 0.5f;
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float r = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                float core = Mathf.Clamp01(1f - Mathf.Abs(r - 0.94f) / 0.025f);
                float glow = Mathf.Clamp01(1f - Mathf.Abs(r - 0.92f) / 0.12f) * 0.45f;
                float a = r > 1f ? 0f : Mathf.Max(core, glow);
                ringTex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        ringTex.Apply();
        return ringTex;
    }

    /// <summary>A filled disc that is brighter toward its rim (the danger area).</summary>
    static Texture2D WaveTexture()
    {
        if (waveTex != null) return waveTex;
        const int s = 128;
        waveTex = new Texture2D(s, s, TextureFormat.RGBA32, true);
        waveTex.wrapMode = TextureWrapMode.Clamp;
        float c = (s - 1) * 0.5f;
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float r = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                float a = r > 1f ? 0f : Mathf.Lerp(0.25f, 1f, r * r) * Mathf.Clamp01((1f - r) / 0.04f);
                waveTex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        waveTex.Apply();
        return waveTex;
    }

    // ------------------------------------------------------------------ phase 2

    void OnHalf(EnemyBase e)
    {
        if (phase2) return;
        phase2 = true;
        EnemyData d = e.data;
        d.walkSpeed *= phase2SpeedMultiplier;
        d.runSpeed *= phase2SpeedMultiplier;
        d.attackWindup *= phase2WindupMultiplier;
        d.attackCooldown *= 0.8f;
        d.glowColor = phase2Glow;
        d.tint = Color.Lerp(d.tint, new Color(1f, 0.55f, 0.4f), 0.35f);

        Animator a = GetComponentInChildren<Animator>();
        if (a != null && a.runtimeAnimatorController != null) a.SetTrigger("Roar");
        CameraShake cs = CameraShake.Instance;
        if (cs != null) cs.Shake(0.12f, 0.8f);

        if (minionPrefab != null)
            for (int i = 0; i < minionCount; i++)
            {
                Vector3 p = transform.position + Quaternion.Euler(0f, i * (360f / minionCount) + 45f, 0f) * Vector3.forward * 3.5f;
                UnityEngine.AI.NavMeshHit hit;
                if (!UnityEngine.AI.NavMesh.SamplePosition(p, out hit, 3f, UnityEngine.AI.NavMesh.AllAreas)) continue;
                GameObject m = Instantiate(minionPrefab, hit.position, transform.rotation);
                UnityEngine.AI.NavMeshAgent ag = m.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (ag != null) ag.Warp(hit.position);
                EnemyBase me = m.GetComponent<EnemyBase>();
                ToolController tc = ToolController.Instance;
                if (me != null && tc != null) me.Alert(tc.transform.position);
            }
    }

    // ------------------------------------------------------------------ death

    void OnDied(EnemyBase e)
    {
        if (e != enemy) return;
        NightUI.Banner(e.data.displayName.ToUpperInvariant() + " SLAIN", "The maze grows quiet", false);
        if (rewardAmount > 0) WorldPickup.Spawn(rewardItem, rewardAmount, transform.position + Vector3.up * 1f, null, null, null);
    }
}
