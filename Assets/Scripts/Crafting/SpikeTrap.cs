using System.Collections;
using UnityEngine;

/// <summary>
/// A placed wooden spike trap. Waits flat; when an enemy steps on it the stakes shoot up, hurt everything on it
/// and slow it down for a few seconds. Never hurts the player. Breaks after a number of uses.
/// The model is Assets/Crafting/SpikeTrap/SpikeTrap.fbx (made in Blender: child "Spikes" is the stake block).
/// </summary>
public class SpikeTrap : MonoBehaviour
{
    public static readonly System.Collections.Generic.List<SpikeTrap> All = new System.Collections.Generic.List<SpikeTrap>();

    [Header("Trap")]
    public float triggerRadius = 0.8f;
    public float damage = 45f;
    [Tooltip("Enemy speed multiplier while slowed.")]
    public float slowFactor = 0.35f;
    public float slowSeconds = 3f;
    public float rearmTime = 1.0f;
    public int uses = 6;

    public int UsesLeft { get; private set; }

    Transform spikes;
    Vector3 fullScale;
    bool busy;
    float readyAt;

    void Awake()
    {
        UsesLeft = uses;
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
            if (t.name == "Spikes") { spikes = t; break; }
        if (spikes != null)
        {
            fullScale = spikes.localScale;
            spikes.localScale = new Vector3(fullScale.x, fullScale.y * 0.04f, fullScale.z);
        }
        readyAt = Time.time + 0.8f;   // does not fire the instant it is put down
    }

    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); }

    void Update()
    {
        if (busy || Time.time < readyAt) return;
        Vector3 p = transform.position;
        foreach (EnemyBase e in EnemyBase.All)
        {
            if (e == null || e.IsDead || e.data == null) continue;
            Vector3 d = e.transform.position - p; d.y = 0f;
            if (d.sqrMagnitude < triggerRadius * triggerRadius && Mathf.Abs(e.transform.position.y - p.y) < 1.6f)
            {
                StartCoroutine(Stab());
                break;
            }
        }
    }

    IEnumerator Stab()
    {
        busy = true;
        PlaceFX.Play(PlaceFX.Snap(), transform.position, 0.9f);
        PlaceFX.Dust(transform.position + Vector3.up * 0.1f, 0.5f, new Color(0.65f, 0.55f, 0.42f, 1f));

        // spikes shoot up
        float t = 0f;
        const float up = 0.07f;
        while (t < up)
        {
            t += Time.deltaTime;
            SetSpikes(Mathf.SmoothStep(0.04f, 1.12f, Mathf.Clamp01(t / up)));
            yield return null;
        }
        SetSpikes(1f);

        // hurt + slow everything standing on it
        Vector3 p = transform.position;
        float r = triggerRadius + 0.25f;
        foreach (EnemyBase e in EnemyBase.All.ToArray())
        {
            if (e == null || e.IsDead || e.data == null) continue;
            Vector3 d = e.transform.position - p; d.y = 0f;
            if (d.sqrMagnitude > r * r) continue;
            e.TakeDamage(damage, p);
            float f = e.data.isBoss ? Mathf.Lerp(slowFactor, 1f, 0.5f) : slowFactor;
            e.Slow(f, slowSeconds);
        }

        UsesLeft--;
        yield return new WaitForSeconds(0.45f);

        if (UsesLeft <= 0)
        {
            PlaceFX.Play(PlaceFX.Creak(), transform.position, 0.8f);
            PlaceFX.Dust(transform.position + Vector3.up * 0.1f, 0.8f, new Color(0.5f, 0.4f, 0.3f, 1f));
            Vector3 s0 = transform.localScale;
            float k = 0f;
            while (k < 0.4f)
            {
                k += Time.deltaTime;
                transform.localScale = s0 * (1f - Mathf.Clamp01(k / 0.4f));
                yield return null;
            }
            Destroy(gameObject);
            yield break;
        }

        // retract
        float back = 0f;
        while (back < 0.35f)
        {
            back += Time.deltaTime;
            SetSpikes(Mathf.Lerp(1f, 0.04f, Mathf.SmoothStep(0f, 1f, back / 0.35f)));
            yield return null;
        }
        SetSpikes(0.04f);
        readyAt = Time.time + rearmTime;
        busy = false;
    }

    void SetSpikes(float y)
    {
        if (spikes == null) return;
        spikes.localScale = new Vector3(fullScale.x, fullScale.y * y, fullScale.z);
    }
}
