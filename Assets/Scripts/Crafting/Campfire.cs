using UnityEngine;

/// <summary>
/// A placed campfire: warm flickering light, scares light-fearing enemies (Shadow Wraith), and slowly heals the player
/// while he stands near it. Burns for burnSeconds, then dies down. The model/flame is RetroStyleGames' P_Campfire.
/// </summary>
public class Campfire : MonoBehaviour, IFearLight
{
    public static readonly System.Collections.Generic.List<Campfire> All = new System.Collections.Generic.List<Campfire>();

    [Header("Fire")]
    public float burnSeconds = 300f;
    public float scareRadius = 9f;
    public float healRadius = 5f;
    public float healPerSecond = 3f;
    public Color lightColor = new Color(1f, 0.56f, 0.22f);
    public float baseIntensity = 3.5f;
    public float baseRange = 13f;

    public Vector3 LightPosition { get { return transform.position + Vector3.up * 0.7f; } }
    public float ScareRadius { get { return scareRadius * Mathf.Lerp(0.5f, 1f, Mathf.Clamp01(remaining / 20f)); } }
    public bool LightActive { get { return remaining > 0f; } }

    float remaining;
    Light lamp;
    ParticleSystem[] fx;
    float seed, fade = 1f, noticeAt;
    bool wasNear;

    void Awake()
    {
        remaining = burnSeconds;
        seed = Random.value * 50f;
        GameObject l = new GameObject("FireLight");
        l.transform.SetParent(transform, false);
        l.transform.localPosition = new Vector3(0f, 0.7f, 0f);
        lamp = l.AddComponent<Light>();
        lamp.type = LightType.Point;
        lamp.color = lightColor;
        lamp.intensity = baseIntensity;
        lamp.range = baseRange;
        lamp.shadows = LightShadows.None;
        fx = GetComponentsInChildren<ParticleSystem>(true);
        // the pack's flame/smoke only start reliably when restarted after being spawned
        foreach (ParticleSystem ps in fx) { ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); ps.Play(true); }
    }

    void OnEnable() { All.Add(this); FearLight.Sources.Add(this); }
    void OnDisable() { All.Remove(this); FearLight.Sources.Remove(this); }

    void Update()
    {
        if (remaining > 0f)
        {
            remaining -= Time.deltaTime;
            if (remaining <= 0f) Die();
        }

        float dying = Mathf.Clamp01(remaining / 20f);
        float t = Time.time + seed;
        float n = Mathf.PerlinNoise(t * 6f, 1.7f), m = Mathf.PerlinNoise(t * 17f, 9.3f);
        float k = (0.8f + 0.35f * n + 0.15f * m) * Mathf.Lerp(0.2f, 1f, dying);
        lamp.intensity = baseIntensity * k * fade;
        lamp.range = baseRange * (0.94f + 0.1f * n) * Mathf.Lerp(0.6f, 1f, dying);

        if (remaining <= 0f)
        {
            fade = Mathf.MoveTowards(fade, 0f, Time.deltaTime * 0.6f);
            if (fade <= 0f && Time.time > noticeAt + 8f) { Destroy(gameObject, 0.1f); }
            return;
        }

        // warmth: heal the player while he is close
        ToolController tc = ToolController.Instance;
        if (tc == null) return;
        Vector3 d = tc.transform.position - transform.position; d.y = 0f;
        bool near = d.sqrMagnitude < healRadius * healRadius;
        if (near)
        {
            HealthManager hm = tc.GetComponent<HealthManager>();
            if (hm != null && !hm.IsDead && hm.CurrentHealth < hm.maxHealth) hm.Heal(healPerSecond * Time.deltaTime);
            if (!wasNear && Time.time > noticeAt) { HudMessage.Show("Campfire - warm and safe"); noticeAt = Time.time + 20f; }
        }
        wasNear = near;
    }

    void Die()
    {
        noticeAt = Time.time;
        foreach (ParticleSystem ps in fx)
            if (ps != null && ps.name.ToLowerInvariant().Contains("flame")) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        foreach (ParticleSystem ps in fx)
            if (ps != null && ps.name.ToLowerInvariant().Contains("smoke")) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }
}
