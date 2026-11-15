using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Player melee. Created automatically when the game starts.
/// ToolController raises OnToolHit when a swing lands; this finds the enemies in front of the player,
/// damages them and plays the feedback: sparks, damage number, hit-stop, camera shake and a hit sound.
/// </summary>
public class PlayerCombat : MonoBehaviour
{
    public static PlayerCombat Instance { get; private set; }

    [Header("Damage")]
    public float swordDamage = 25f;
    public float axeDamage = 12f;
    public float pickaxeDamage = 8f;
    [Range(0f, 1f)] public float critChance = 0.15f;
    public float critMultiplier = 1.75f;

    [Header("Reach")]
    public float range = 2.6f;
    [Range(20f, 180f)] public float arc = 130f;
    public int maxTargets = 3;

    Material sparkMat, puffMat;
    AudioClip hitClip, critClip;
    Coroutine stopRoutine;
    readonly List<EnemyBase> found = new List<EnemyBase>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (Instance != null) return;
        new GameObject("PlayerCombat").AddComponent<PlayerCombat>();
    }

    void Awake()
    {
        Instance = this;
        sparkMat = SoftFX.Spark();
        puffMat = SoftFX.Puff();
        hitClip = MakeHit(false);
        critClip = MakeHit(true);
    }

    void OnEnable() { ToolController.OnToolHit += OnToolHit; }
    void OnDisable() { ToolController.OnToolHit -= OnToolHit; }

    void OnToolHit(ToolType tool)
    {
        ToolController tc = ToolController.Instance;
        if (tc == null || tool == ToolType.None) return;

        bool isWeapon = tool == ToolType.Weapon;
        WeaponInfo wi = Weapons.Info;
        float baseDamage = (isWeapon ? wi.damage : (tool == ToolType.Axe ? axeDamage : pickaxeDamage)) * CraftingState.DamageMultiplier(tool);
        float useRange = isWeapon ? wi.range : range, useArc = isWeapon ? wi.arc : arc;
        int useMax = isWeapon ? wi.maxTargets : maxTargets;
        Transform p = tc.transform;
        Vector3 origin = p.position + Vector3.up * 1f;

        found.Clear();
        Collider[] hits = Physics.OverlapSphere(origin, useRange, ~0, QueryTriggerInteraction.Ignore);
        foreach (Collider c in hits)
        {
            EnemyBase e = c.GetComponentInParent<EnemyBase>();
            if (e == null || e.IsDead || found.Contains(e)) continue;
            Vector3 to = e.transform.position - p.position;
            to.y = 0f;
            if (to.sqrMagnitude > 0.04f && Vector3.Angle(p.forward, to) > useArc * 0.5f) continue;
            if (EnemyBase.WallBetween(origin + Vector3.up * 0.3f, ChestPoint(e))) continue;   // no hits through maze walls
            found.Add(e);
        }
        if (found.Count == 0) return;

        found.Sort((a, b) => (a.transform.position - p.position).sqrMagnitude.CompareTo((b.transform.position - p.position).sqrMagnitude));

        bool anyCrit = false;
        int n = Mathf.Min(useMax, found.Count);
        for (int i = 0; i < n; i++)
        {
            EnemyBase e = found[i];
            bool crit = Random.value < critChance;
            float dmg = Mathf.Round(baseDamage * Random.Range(0.9f, 1.1f) * (crit ? critMultiplier : 1f));
            Vector3 point = ChestPoint(e);
            bool wasAlive = !e.IsDead;
            e.TakeDamage(dmg, p.position);
            if (isWeapon && wi.slowTime > 0f && !e.IsDead) e.Slow(wi.slow, wi.slowTime);   // battle axe slows, war hammer stuns
            CombatUI.SpawnNumber(point + Vector3.up * 0.3f, dmg, crit, wasAlive && e.IsDead);
            Sparks(point, (point - origin).normalized, crit);
            Play(crit ? critClip : hitClip, point, crit ? 1f : 0.85f);
            if (crit) anyCrit = true;
        }

        CameraShake cs = CameraShake.Instance;
        if (cs != null) cs.Shake((anyCrit ? 0.075f : 0.04f) + (isWeapon ? wi.shake : 0f), anyCrit ? 0.22f : 0.14f);
        HitStop(tc.animator, anyCrit ? 0.1f : 0.06f);
    }

    static Vector3 ChestPoint(EnemyBase e)
    {
        Collider c = e.GetComponentInChildren<Collider>();
        if (c != null) { Bounds b = c.bounds; return new Vector3(b.center.x, b.min.y + b.size.y * 0.65f, b.center.z); }
        return e.transform.position + Vector3.up * 1.2f;
    }

    void HitStop(Animator a, float seconds)
    {
        if (a == null) return;
        if (stopRoutine != null) StopCoroutine(stopRoutine);
        stopRoutine = StartCoroutine(HitStopRoutine(a, seconds));
    }

    IEnumerator HitStopRoutine(Animator a, float seconds)
    {
        a.speed = 0.05f;
        yield return new WaitForSecondsRealtime(seconds);
        a.speed = 1f;
        stopRoutine = null;
    }

    // ------------------------------------------------------------------ feedback

    void Sparks(Vector3 pos, Vector3 dir, bool crit)
    {
        if (sparkMat == null) return;
        Burst(pos, dir, sparkMat, crit ? new Color(1f, 0.55f, 0.15f) : new Color(1f, 0.82f, 0.45f), crit ? 28 : 14, 3f, crit ? 9f : 7f, 0.035f, 0.07f, 0.55f, 1.4f, 55f);
        if (puffMat != null) Burst(pos, Vector3.up, puffMat, new Color(0.35f, 0.04f, 0.04f, 0.55f), crit ? 8 : 4, 0.6f, 1.6f, 0.25f, 0.5f, 0.6f, 0.1f, 70f);
    }

    static void Burst(Vector3 pos, Vector3 dir, Material mat, Color color, int count, float speedMin, float speedMax,
                      float sizeMin, float sizeMax, float life, float gravity, float angle)
    {
        GameObject go = new GameObject("HitFX");
        go.transform.position = pos;
        go.transform.rotation = dir.sqrMagnitude > 0.001f ? Quaternion.LookRotation(dir) : Quaternion.identity;
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 0.6f;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.6f, life);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
        main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
        main.startColor = color;
        main.gravityModifier = gravity;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = count + 2;

        var em = ps.emission;
        em.rateOverTime = 0f;
        em.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, (short)count) });

        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Cone;
        sh.angle = angle;
        sh.radius = 0.05f;

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.1f));

        Gradient g = new Gradient();
        g.SetKeys(new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = g;

        ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        if (speedMax > 4f) { r.renderMode = ParticleSystemRenderMode.Stretch; r.lengthScale = 2.5f; r.velocityScale = 0.06f; }

        ps.Play();
        Destroy(go, life + 0.8f);
    }

    static void Play(AudioClip clip, Vector3 pos, float volume)
    {
        if (clip == null) return;
        GameObject go = new GameObject("SFX");
        go.transform.position = pos;
        AudioSource a = go.AddComponent<AudioSource>();
        a.clip = clip;
        a.volume = volume;
        a.pitch = Random.Range(0.92f, 1.1f);
        a.spatialBlend = 0.85f;
        a.minDistance = 4f;
        a.maxDistance = 40f;
        a.Play();
        Destroy(go, clip.length + 0.2f);
    }

    /// <summary>Blade hitting flesh: a wet thud, a bright metal ring, a short noise burst. The crit version rings longer.</summary>
    static AudioClip MakeHit(bool crit)
    {
        const int rate = 44100;
        float seconds = crit ? 0.5f : 0.34f;
        int n = (int)(rate * seconds);
        float[] d = new float[n];
        System.Random rnd = new System.Random(crit ? 11 : 5);
        float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float noise = (float)(rnd.NextDouble() * 2.0 - 1.0);
            lp += (noise - lp) * 0.3f;
            float thud = Mathf.Sin(6.2832f * (150f - 90f * (i / (float)n)) * t) * Mathf.Exp(-t * 22f);
            float ring = (Mathf.Sin(6.2832f * 1900f * t) * 0.5f + Mathf.Sin(6.2832f * 2850f * t) * 0.3f) * Mathf.Exp(-t * (crit ? 10f : 26f));
            float v = lp * Mathf.Exp(-t * 40f) * 0.9f + thud * 0.9f + ring * (crit ? 0.5f : 0.3f) + noise * Mathf.Exp(-t * 160f) * 0.5f;
            d[i] = Mathf.Clamp(v, -1f, 1f);
        }
        AudioClip c = AudioClip.Create(crit ? "hitCrit" : "hit", n, 1, rate, false);
        c.SetData(d, 0);
        return c;
    }
}
