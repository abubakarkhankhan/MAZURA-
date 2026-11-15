using UnityEngine;

/// <summary>
/// Generated background music (no audio files): a calm pad for the day, a darker one at night
/// and a low drone with water drips in the dungeon. Cross-fades between them.
/// </summary>
public class AmbientMusic : MonoBehaviour
{
    const int Rate = 22050;
    AudioSource day, night, dungeon;
    static AudioClip dayClip, nightClip, dungeonClip;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (Object.FindAnyObjectByType<AmbientMusic>() != null) return;
        new GameObject("AmbientMusic").AddComponent<AmbientMusic>();
    }

    void Start()
    {
        if (dayClip == null) dayClip = Pad("music_day", new float[][] { new[] { 220f, 261.6f, 329.6f }, new[] { 174.6f, 220f, 261.6f }, new[] { 196f, 246.9f, 293.7f }, new[] { 164.8f, 207.7f, 246.9f } }, 0.9f, 0.0f);
        if (nightClip == null) nightClip = Pad("music_night", new float[][] { new[] { 146.8f, 174.6f, 220f }, new[] { 138.6f, 164.8f, 207.7f }, new[] { 130.8f, 155.6f, 196f }, new[] { 138.6f, 174.6f, 207.7f } }, 0.75f, 0.02f);
        if (dungeonClip == null) dungeonClip = Drone("music_dungeon");
        day = Src(dayClip); night = Src(nightClip); dungeon = Src(dungeonClip);
    }

    AudioSource Src(AudioClip c)
    {
        AudioSource s = gameObject.AddComponent<AudioSource>();
        s.clip = c; s.loop = true; s.volume = 0f; s.spatialBlend = 0f; s.playOnAwake = false;
        s.ignoreListenerPause = false;
        s.Play();
        return s;
    }

    void Update()
    {
        bool inDungeon = DungeonZone.PlayerInside;
        bool isNight = DayNightManager.Instance != null && DayNightManager.Instance.IsNight;
        bool menu = Object.FindAnyObjectByType<ToolController>() == null;
        float m = GameSettings.Music * 0.32f;
        Fade(day, (!inDungeon && (!isNight || menu)) ? m : 0f);
        Fade(night, (!inDungeon && isNight && !menu) ? m : 0f);
        Fade(dungeon, inDungeon ? m * 1.2f : 0f);
    }

    static void Fade(AudioSource s, float target) { if (s != null) s.volume = Mathf.MoveTowards(s.volume, target, Time.unscaledDeltaTime * 0.15f); }

    // soft chord pad: each chord 6 s, gentle attack, slow tremolo, a little wind noise
    static AudioClip Pad(string name, float[][] chords, float bright, float wobble)
    {
        const float chordLen = 6f;
        int len = (int)(Rate * chordLen * chords.Length);
        float[] d = new float[len];
        System.Random r = new System.Random(7);
        float lp = 0f;
        for (int i = 0; i < len; i++)
        {
            float t = i / (float)Rate;
            int ci = (int)(t / chordLen) % chords.Length;
            float local = t % chordLen;
            float env = Mathf.Clamp01(local / 1.6f) * Mathf.Clamp01((chordLen - local) / 1.6f);
            float v = 0f;
            float[] ch = chords[ci];
            for (int k = 0; k < ch.Length; k++)
            {
                float f = ch[k] * (1f + wobble * Mathf.Sin(t * 0.7f + k));
                v += Mathf.Sin(2f * Mathf.PI * f * t) * 0.22f;
                v += Mathf.Sin(2f * Mathf.PI * f * 2f * t) * 0.05f * bright;
                v += Mathf.Sin(2f * Mathf.PI * f * 0.5f * t) * 0.08f;
            }
            float trem = 0.85f + 0.15f * Mathf.Sin(t * 2f * Mathf.PI * 0.2f);
            float n = (float)r.NextDouble() * 2f - 1f;
            lp += (n - lp) * 0.01f;
            d[i] = (v * env * trem) * 0.5f + lp * 0.35f * (0.6f + 0.4f * Mathf.Sin(t * 0.3f));
        }
        // loop seam: fade the first/last 0.5 s together
        int x = Rate / 2;
        for (int i = 0; i < x; i++) { float a = i / (float)x; d[i] = d[i] * a + d[len - x + i] * (1f - a); }
        AudioClip c = AudioClip.Create(name, len - x, 1, Rate, false);
        float[] o = new float[len - x];
        System.Array.Copy(d, o, len - x);
        c.SetData(o, 0);
        return c;
    }

    static AudioClip Drone(string name)
    {
        const float seconds = 24f;
        int len = (int)(Rate * seconds);
        float[] d = new float[len];
        System.Random r = new System.Random(11);
        float lp = 0f;
        float nextDrip = 1.3f; float dripT = -1f; float dripF = 900f;
        for (int i = 0; i < len; i++)
        {
            float t = i / (float)Rate;
            float v = Mathf.Sin(2f * Mathf.PI * 55f * t) * 0.3f + Mathf.Sin(2f * Mathf.PI * 82.4f * t) * 0.16f + Mathf.Sin(2f * Mathf.PI * 110.2f * t + Mathf.Sin(t * 0.4f)) * 0.09f;
            v *= 0.75f + 0.25f * Mathf.Sin(t * 0.5f);
            float n = (float)r.NextDouble() * 2f - 1f;
            lp += (n - lp) * 0.004f;
            v += lp * 1.2f;
            if (t >= nextDrip) { dripT = t; dripF = 700f + (float)r.NextDouble() * 900f; nextDrip = t + 1.5f + (float)r.NextDouble() * 4f; }
            if (dripT >= 0f)
            {
                float dt = t - dripT;
                if (dt < 0.25f) v += Mathf.Sin(2f * Mathf.PI * (dripF + dt * 1800f) * dt) * Mathf.Exp(-dt * 30f) * 0.35f;
            }
            d[i] = v * 0.55f;
        }
        int x = Rate / 2;
        for (int i = 0; i < x; i++) { float a = i / (float)x; d[i] = d[i] * a + d[len - x + i] * (1f - a); }
        AudioClip c = AudioClip.Create(name, len - x, 1, Rate, false);
        float[] o = new float[len - x];
        System.Array.Copy(d, o, len - x);
        c.SetData(o, 0);
        return c;
    }
}
