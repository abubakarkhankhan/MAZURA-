using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A wild healing herb in the maze. Walk into it to gather 1-2 Herb (goes into the Inventory).
/// It shrinks away, then grows back in the same place after 'regrowTime' seconds.
/// Soft green motes float above it so it can be spotted at night.
/// </summary>
public class HerbPlant : MonoBehaviour
{
    public int minAmount = 1;
    public int maxAmount = 2;
    [Tooltip("Metres from the player (on the ground) at which the herb is picked.")]
    public float pickRadius = 1.2f;
    [Tooltip("Seconds until a picked herb grows back.")]
    public float regrowTime = 120f;

    public static readonly List<HerbPlant> All = new List<HerbPlant>();

    Vector3 baseScale;
    bool picked;
    float regrowT;
    Transform player;
    HealthManager playerHealth;
    ParticleSystem motes;
    Renderer[] renderers;
    static AudioClip rustle;

    void Awake()
    {
        baseScale = transform.localScale;
        renderers = GetComponentsInChildren<Renderer>();
    }

    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); }

    void Start()
    {
        BuildMotes();
    }

    void Update()
    {
        if (picked)
        {
            regrowT += Time.deltaTime;
            if (regrowT >= regrowTime) { picked = false; StartCoroutine(Grow(true)); }
            return;
        }

        if (player == null)
        {
            ToolController tc = ToolController.Instance;
            if (tc == null) return;
            player = tc.transform;
            playerHealth = tc.GetComponent<HealthManager>();
        }
        if (playerHealth != null && playerHealth.IsDead) return;

        Vector3 d = player.position - transform.position;
        d.y = 0f;
        if (d.sqrMagnitude < pickRadius * pickRadius) Pick();
    }

    void Pick()
    {
        picked = true;
        regrowT = 0f;
        int n = Random.Range(minAmount, maxAmount + 1);
        if (Inventory.Instance != null) Inventory.Instance.Add(ItemType.Herb, n);
        if (rustle == null) rustle = MakeRustle();
        AudioSource.PlayClipAtPoint(rustle, transform.position + Vector3.up * 0.3f, 0.6f);
        if (motes != null) motes.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        StopAllCoroutines();
        StartCoroutine(Grow(false));
    }

    IEnumerator Grow(bool up)
    {
        float dur = up ? 1.2f : 0.3f;
        if (up) foreach (Renderer r in renderers) if (r != null && !(r is ParticleSystemRenderer)) r.enabled = true;
        for (float t = 0f; t < dur; t += Time.deltaTime)
        {
            float k = t / dur;
            float s = up ? Mathf.SmoothStep(0.05f, 1f, k) : Mathf.Lerp(1f, 0.05f, k * k);
            transform.localScale = baseScale * s;
            yield return null;
        }
        transform.localScale = baseScale * (up ? 1f : 0.05f);
        if (!up) foreach (Renderer r in renderers) if (r != null && !(r is ParticleSystemRenderer)) r.enabled = false;
        if (up && motes != null) motes.Play();
    }

    void BuildMotes()
    {
        GameObject go = new GameObject("HerbMotes");
        go.transform.SetParent(transform, false);
        go.transform.position = transform.position + Vector3.up * 0.15f;
        go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
        motes = go.AddComponent<ParticleSystem>();
        motes.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = motes.main;
        main.loop = true;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, 2.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.06f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.55f, 1f, 0.45f), new Color(0.85f, 1f, 0.6f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = 20;
        var em = motes.emission; em.rateOverTime = 4f;
        var sh = motes.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 0.25f;
        Gradient g = new Gradient();
        g.SetKeys(new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new GradientAlphaKey[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(0f, 1f) });
        var col = motes.colorOverLifetime; col.enabled = true; col.color = g;
        ParticleSystemRenderer pr = go.GetComponent<ParticleSystemRenderer>();
        pr.sharedMaterial = SoftFX.Spark();
        pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        motes.Play();
    }

    /// <summary>Short leafy rustle (filtered noise burst).</summary>
    static AudioClip MakeRustle()
    {
        const int rate = 44100;
        int n = (int)(rate * 0.35f);
        float[] d = new float[n];
        System.Random rnd = new System.Random(7);
        float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float noise = (float)(rnd.NextDouble() * 2.0 - 1.0);
            lp += (noise - lp) * 0.35f;
            float env = Mathf.Exp(-t * 9f) * (0.6f + 0.4f * Mathf.Sin(t * 90f));
            d[i] = Mathf.Clamp((noise - lp) * env * 0.7f, -1f, 1f);
        }
        AudioClip c = AudioClip.Create("herbRustle", n, 1, rate, false);
        c.SetData(d, 0);
        return c;
    }
}
