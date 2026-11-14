using UnityEngine;

/// <summary>
/// Shadow Wraith heartbeat: a low "lub-dub" that gets louder and faster the closer the Wraith is to the player.
/// The clip is generated in code, so no audio asset is needed (drop your own into 'clip' to replace it).
/// </summary>
[RequireComponent(typeof(EnemyBase))]
public class EnemyHeartbeat : MonoBehaviour
{
    public AudioClip clip;
    [Tooltip("Heard from this far away. 0 = use EnemyData.heartbeatRange.")]
    public float range = 0f;
    [Range(0f, 1f)] public float maxVolume = 0.9f;

    EnemyBase enemy;
    AudioSource src;
    Transform player;

    void Awake()
    {
        enemy = GetComponent<EnemyBase>();
        src = gameObject.AddComponent<AudioSource>();
        src.loop = true;
        src.spatialBlend = 0f;          // heard inside the head, like a real heartbeat
        src.playOnAwake = false;
        src.volume = 0f;
        src.clip = clip != null ? clip : MakeClip();
        src.Play();
    }

    void Update()
    {
        if (player == null && ToolController.Instance != null) player = ToolController.Instance.transform;
        float r = range > 0f ? range : (enemy.data != null ? enemy.data.heartbeatRange : 0f);
        if (player == null || r <= 0f || enemy.IsDead) { src.volume = Mathf.MoveTowards(src.volume, 0f, Time.deltaTime * 2f); return; }

        float d = Vector3.Distance(transform.position, player.position);
        float k = Mathf.Clamp01(1f - d / r);                    // 0 far ... 1 on top of you
        float target = maxVolume * k * k;                       // quadratic: silent far away, loud up close
        src.volume = Mathf.MoveTowards(src.volume, target, Time.deltaTime * 1.5f);
        src.pitch = Mathf.Lerp(0.8f, 1.35f, k);                 // faster when close
    }

    /// <summary>One second: a strong 'lub' and a softer 'dub'.</summary>
    static AudioClip MakeClip()
    {
        const int rate = 22050;
        float[] data = new float[rate];
        AddThump(data, rate, 0.00f, 1.0f, 58f);
        AddThump(data, rate, 0.24f, 0.7f, 48f);
        AudioClip c = AudioClip.Create("Heartbeat", data.Length, 1, rate, false);
        c.SetData(data, 0);
        return c;
    }

    static void AddThump(float[] d, int rate, float start, float amp, float freq)
    {
        int s = (int)(start * rate), n = (int)(0.16f * rate);
        for (int i = 0; i < n && s + i < d.Length; i++)
        {
            float t = i / (float)rate;
            float env = Mathf.Exp(-t * 28f) * Mathf.Min(1f, t * 400f);   // quick attack, fast decay
            d[s + i] += Mathf.Sin(2f * Mathf.PI * freq * t) * env * amp * 0.9f;
        }
    }
}
