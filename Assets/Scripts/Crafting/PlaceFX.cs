using UnityEngine;

/// <summary>Small generated sounds and a dust puff shared by the placeable items (campfire, spike trap).</summary>
public static class PlaceFX
{
    static AudioClip thud, snap, creak;

    public static AudioClip Thud()
    {
        if (thud != null) return thud;
        const int sr = 22050;
        int n = (int)(sr * 0.35f);
        float[] d = new float[n];
        System.Random r = new System.Random(21);
        float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)sr;
            float env = Mathf.Exp(-t * 14f);
            float w = (float)(r.NextDouble() * 2.0 - 1.0);
            lp += (w - lp) * 0.12f;
            d[i] = (Mathf.Sin(2f * Mathf.PI * 85f * t) * 0.8f + lp * 0.6f) * env;
        }
        thud = AudioClip.Create("PlaceThud", n, 1, sr, false);
        thud.SetData(d, 0);
        return thud;
    }

    public static AudioClip Snap()
    {
        if (snap != null) return snap;
        const int sr = 22050;
        int n = (int)(sr * 0.3f);
        float[] d = new float[n];
        System.Random r = new System.Random(5);
        float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)sr;
            float w = (float)(r.NextDouble() * 2.0 - 1.0);
            lp += (w - lp) * 0.5f;
            float crack = (w - lp) * Mathf.Exp(-t * 38f);
            float knock = Mathf.Sin(2f * Mathf.PI * 190f * t) * Mathf.Exp(-t * 22f) * 0.6f;
            d[i] = Mathf.Clamp(crack * 1.2f + knock, -1f, 1f);
        }
        snap = AudioClip.Create("TrapSnap", n, 1, sr, false);
        snap.SetData(d, 0);
        return snap;
    }

    public static AudioClip Creak()
    {
        if (creak != null) return creak;
        const int sr = 22050;
        int n = (int)(sr * 0.5f);
        float[] d = new float[n];
        System.Random r = new System.Random(9);
        float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)n;
            float env = Mathf.Sin(t * Mathf.PI);
            float w = (float)(r.NextDouble() * 2.0 - 1.0);
            lp += (w - lp) * 0.06f;
            float tone = Mathf.Sin(2f * Mathf.PI * (140f + 90f * t) * i / sr);
            d[i] = (lp * 0.8f + tone * 0.18f) * env;
        }
        creak = AudioClip.Create("TrapBreak", n, 1, sr, false);
        creak.SetData(d, 0);
        return creak;
    }

    public static void Play(AudioClip clip, Vector3 at, float volume)
    {
        if (clip != null) AudioSource.PlayClipAtPoint(clip, at, volume);
    }

    /// <summary>One-shot dust cloud.</summary>
    public static void Dust(Vector3 at, float size, Color tint)
    {
        GameObject g = new GameObject("PlaceDust");
        g.transform.position = at;
        ParticleSystem ps = g.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.duration = 0.5f;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.3f);
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
        main.startColor = tint;
        main.gravityModifier = -0.05f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var em = ps.emission; em.enabled = false;
        var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Hemisphere; shape.radius = size * 0.5f;
        var col = ps.colorOverLifetime; col.enabled = true;
        Gradient gr = new Gradient();
        gr.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                   new[] { new GradientAlphaKey(0.7f, 0f), new GradientAlphaKey(0.35f, 0.4f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(gr);
        var sz = ps.sizeOverLifetime; sz.enabled = true;
        sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(1f, 1.6f)));
        ParticleSystemRenderer pr = g.GetComponent<ParticleSystemRenderer>();
        pr.material = SoftFX.Puff();
        pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ps.Emit(14);
        ps.Play();
        Object.Destroy(g, 2f);
    }
}
