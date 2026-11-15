using TMPro;
using UnityEngine;

/// <summary>
/// "Build it bit by bit": a placed Campfire / Spike Trap / Workbench first appears as a small
/// construction site. While the player stands close (within buildRange) they hammer at it
/// (craft animation, knocks, dust) and it grows piece by piece. When the progress is full the
/// real, working object replaces the site. Walk away and the building pauses.
/// </summary>
public class ConstructionSite : MonoBehaviour
{
    /// <summary>True while the player is hammering at a site (blocks tools / placement).</summary>
    public static bool Busy { get; private set; }
    static ConstructionSite active;

    const float BuildRange = 3.2f;
    const float HitInterval = 0.6f;

    GameObject prefab;
    string label;
    float buildTime, progress, nextHit;
    Transform visual;
    Vector3 visualScale;
    TextMeshPro text;
    float punch;
    static AudioClip knock;

    /// <summary>Starts a construction site for 'prefab' at the given spot.</summary>
    public static ConstructionSite Begin(GameObject prefab, Vector3 pos, Quaternion rot, float seconds, string label)
    {
        // Build an inert copy of the prefab: spawn it under an inactive parent so no script runs,
        // then strip scripts, colliders, lights and effects.
        GameObject holder = new GameObject("Construction_" + label);
        holder.SetActive(false);
        holder.transform.SetPositionAndRotation(pos, rot);
        GameObject copy = Instantiate(prefab, holder.transform, false);
        copy.transform.localPosition = Vector3.zero;
        copy.transform.localRotation = Quaternion.identity;
        foreach (MonoBehaviour mb in copy.GetComponentsInChildren<MonoBehaviour>(true)) DestroyImmediate(mb);
        foreach (Collider c in copy.GetComponentsInChildren<Collider>(true)) DestroyImmediate(c);
        foreach (Light l in copy.GetComponentsInChildren<Light>(true)) DestroyImmediate(l);
        foreach (UnityEngine.AI.NavMeshObstacle o in copy.GetComponentsInChildren<UnityEngine.AI.NavMeshObstacle>(true)) DestroyImmediate(o);
        foreach (ParticleSystem ps in copy.GetComponentsInChildren<ParticleSystem>(true)) ps.gameObject.SetActive(false);

        ConstructionSite site = holder.AddComponent<ConstructionSite>();
        site.prefab = prefab;
        site.label = label;
        site.buildTime = Mathf.Max(0.5f, seconds);
        site.visual = copy.transform;
        site.visualScale = copy.transform.localScale;
        holder.SetActive(true);
        return site;
    }

    void Awake()
    {
        GameObject t = new GameObject("BuildText");
        t.transform.SetParent(transform, false);
        t.transform.localPosition = Vector3.up * 1.6f;
        text = t.AddComponent<TextMeshPro>();
        text.fontSize = 1.5f;
        text.alignment = TextAlignmentOptions.Center;
        text.fontStyle = FontStyles.Bold;
        text.color = new Color(1f, 0.85f, 0.5f);
        text.outlineWidth = 0.25f;
        text.outlineColor = new Color32(15, 8, 4, 255);
        text.rectTransform.sizeDelta = new Vector2(8f, 1.2f);
        if (knock == null) knock = MakeKnock();
    }

    void Start()
    {
        Apply();
        PlaceFX.Dust(transform.position + Vector3.up * 0.05f, 0.6f, new Color(0.6f, 0.52f, 0.4f, 1f));
    }

    void OnDestroy()
    {
        if (active == this) { active = null; Busy = false; }
    }

    void Update()
    {
        ToolController tc = ToolController.Instance;
        if (tc == null) return;
        HealthManager hm = tc.GetComponent<HealthManager>();
        Vector3 d = transform.position - tc.transform.position;
        d.y = 0f;
        bool near = d.magnitude <= BuildRange && (hm == null || !hm.IsDead);

        if (near && (active == null || active == this))
        {
            active = this;
            Busy = true;
            progress += Time.deltaTime / buildTime;

            // face the site while hammering
            if (d.sqrMagnitude > 0.01f)
            {
                Quaternion want = Quaternion.LookRotation(d.normalized, Vector3.up);
                tc.transform.rotation = Quaternion.RotateTowards(tc.transform.rotation, want, 360f * Time.deltaTime);
            }

            if (Time.time >= nextHit)
            {
                nextHit = Time.time + HitInterval;
                if (tc.animator != null) tc.animator.SetTrigger("Craft");
                PlaceFX.Play(knock, transform.position + Vector3.up * 0.4f, 0.75f);
                PlaceFX.Dust(transform.position + Vector3.up * 0.2f + Random.insideUnitSphere * 0.25f, 0.35f, new Color(0.65f, 0.55f, 0.42f, 1f));
                punch = 1f;
            }
        }
        else if (active == this)
        {
            active = null;
            Busy = false;
        }

        punch = Mathf.MoveTowards(punch, 0f, Time.deltaTime * 6f);
        Apply();

        if (progress >= 1f) Finish();
    }

    void Apply()
    {
        float p = Mathf.Clamp01(progress);
        // grows in visible steps, like pieces being added
        float stepped = Mathf.Floor(p * 6f) / 6f;
        float s = Mathf.Lerp(0.15f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.Lerp(stepped, p, 0.35f)));
        s *= 1f + punch * 0.06f;
        visual.localScale = visualScale * s;

        int pct = Mathf.FloorToInt(p * 100f);
        text.text = (active == this ? "BUILDING " : "<color=#C9B48A>WALK CLOSER TO BUILD</color>  ") + label + "  " + pct + "%";
        Camera cam = Camera.main;
        if (cam != null) text.transform.rotation = cam.transform.rotation;
    }

    void Finish()
    {
        Vector3 pos = transform.position;
        GameObject go = Instantiate(prefab, pos, transform.rotation);
        go.name = label.Replace(" ", "") + "_Built";
        PlaceFX.Play(PlaceFX.Thud(), pos, 0.95f);
        PlaceFX.Dust(pos + Vector3.up * 0.1f, 1f, new Color(0.62f, 0.54f, 0.42f, 1f));
        HudMessage.Show(label + " built");
        Destroy(gameObject);
    }

    /// <summary>Short wooden knock (hammer on wood).</summary>
    static AudioClip MakeKnock()
    {
        const int rate = 44100;
        int n = (int)(rate * 0.22f);
        float[] data = new float[n];
        System.Random rnd = new System.Random(5);
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float noise = (float)(rnd.NextDouble() * 2.0 - 1.0);
            float body = Mathf.Sin(6.2832f * 210f * t) * Mathf.Exp(-t * 28f) + Mathf.Sin(6.2832f * 470f * t) * Mathf.Exp(-t * 40f) * 0.5f;
            data[i] = Mathf.Clamp((body * 0.8f + noise * Mathf.Exp(-t * 150f) * 0.5f) * 0.8f, -1f, 1f);
        }
        AudioClip c = AudioClip.Create("buildKnock", n, 1, rate, false);
        c.SetData(data, 0);
        return c;
    }
}
