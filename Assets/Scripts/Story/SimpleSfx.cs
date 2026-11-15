using UnityEngine;

/// <summary>Small generated sounds (no audio files needed): rumble, chime, click.</summary>
public static class SimpleSfx
{
    static AudioClip rumble, chime, click, thud;
    const int Rate = 44100;

    static AudioClip Make(string n, float seconds, System.Func<float, float> f)
    {
        int len = (int)(Rate * seconds);
        float[] d = new float[len];
        for (int i = 0; i < len; i++) d[i] = Mathf.Clamp(f(i / (float)Rate), -1f, 1f);
        AudioClip c = AudioClip.Create(n, len, 1, Rate, false);
        c.SetData(d, 0);
        return c;
    }

    public static AudioClip Rumble()
    {
        if (rumble != null) return rumble;
        System.Random r = new System.Random(3);
        float lp = 0f;
        rumble = Make("rumble", 2.2f, t =>
        {
            float n = (float)r.NextDouble() * 2f - 1f;
            lp += (n - lp) * 0.02f;
            float env = Mathf.Clamp01(t * 3f) * Mathf.Clamp01((2.2f - t) / 1.2f);
            return (lp * 3.5f + Mathf.Sin(t * 2f * Mathf.PI * 38f) * 0.25f) * env;
        });
        return rumble;
    }

    public static AudioClip Chime()
    {
        if (chime != null) return chime;
        chime = Make("chime", 1.4f, t =>
        {
            float e = Mathf.Exp(-t * 3.2f);
            return (Mathf.Sin(t * 2f * Mathf.PI * 880f) * 0.35f + Mathf.Sin(t * 2f * Mathf.PI * 1320f) * 0.22f + Mathf.Sin(t * 2f * Mathf.PI * 1760f) * 0.12f) * e;
        });
        return chime;
    }

    public static AudioClip Click()
    {
        if (click != null) return click;
        click = Make("click", 0.08f, t => Mathf.Sin(t * 2f * Mathf.PI * 1400f) * Mathf.Exp(-t * 70f) * 0.6f);
        return click;
    }

    public static AudioClip Thud()
    {
        if (thud != null) return thud;
        thud = Make("thud", 0.35f, t => Mathf.Sin(t * 2f * Mathf.PI * (90f - t * 120f)) * Mathf.Exp(-t * 12f) * 0.9f);
        return thud;
    }
}
