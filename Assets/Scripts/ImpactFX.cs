using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Makes every swing feel good. Created automatically when the game starts, nothing to set up.
/// Listens to ResourceNode / ToolController events and adds:
///   wood chips, bark and leaves, stone debris, sparks and gem glitter, dust puffs,
///   camera shake, a tiny hit-stop (the swing freezes for a moment on impact), and sounds.
/// The sounds are generated in code. To use real recordings, assign clips to the public fields below.
/// </summary>
public class ImpactFX : MonoBehaviour
{
    static ImpactFX instance;

    [Header("Optional: your own sounds (empty = generated sounds)")]
    public AudioClip chopClip;
    public AudioClip crackClip;
    public AudioClip treeLandClip;
    public AudioClip mineClip;
    public AudioClip rockBreakClip;
    public AudioClip swooshClip;

    Material chipMat, sparkMat, dustMat;
    Mesh cubeMesh, sphereMesh;
    Coroutine hitStopRoutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (instance != null) return;
        instance = new GameObject("ImpactFX").AddComponent<ImpactFX>();
    }

    void Awake()
    {
        cubeMesh = GetPrimitiveMesh(PrimitiveType.Cube);
        sphereMesh = GetPrimitiveMesh(PrimitiveType.Sphere);

        Shader lit = Shader.Find("Universal Render Pipeline/Particles/Lit");
        if (lit == null) lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit != null) chipMat = new Material(lit);

        Shader pu = Shader.Find("Mazura/ParticleUnlit");
        if (pu != null)
        {
            sparkMat = new Material(pu);   // additive glow
            sparkMat.SetFloat("_SrcBlend", 1f);
            sparkMat.SetFloat("_DstBlend", 1f);
            dustMat = new Material(pu);    // soft smoke
            dustMat.SetFloat("_SrcBlend", 5f);
            dustMat.SetFloat("_DstBlend", 10f);
        }

        BuildSounds();

        Camera c = Camera.main;
        if (c != null && c.GetComponent<CameraShake>() == null) c.gameObject.AddComponent<CameraShake>();
    }

    void OnEnable()
    {
        ResourceNode.OnAnyNodeHit += OnNodeHit;
        ResourceNode.OnTreeLanded += OnTreeLanded;
        ToolController.OnSwingStart += OnSwingStart;
    }

    void OnDisable()
    {
        ResourceNode.OnAnyNodeHit -= OnNodeHit;
        ResourceNode.OnTreeLanded -= OnTreeLanded;
        ToolController.OnSwingStart -= OnSwingStart;
    }

    // ------------------------------------------------------------------ events

    void OnSwingStart(ToolType tool)
    {
        ToolController tc = ToolController.Instance;
        if (tc == null) return;
        Play(swooshClip, tc.transform.position + Vector3.up, tool == ToolType.Weapon ? 0.7f : 0.45f, 0.9f, 1.15f);
    }

    void OnNodeHit(ResourceNode n, bool last, Vector3 hp)
    {
        Vector3 toPlayer = Vector3.up;
        ToolController tc = ToolController.Instance;
        if (tc != null)
        {
            Vector3 d = tc.transform.position - hp;
            d.y = 0f;
            toPlayer = d.normalized + Vector3.up * 0.5f;
        }

        if (n.nodeType == NodeType.Tree)
        {
            Burst(hp, toPlayer, cubeMesh, chipMat, new Color(0.74f, 0.54f, 0.31f), last ? 24 : 12, 2.5f, 5.5f, 0.02f, 0.05f, 1.6f, 1.2f, 40f, false, false, true);
            Burst(hp, toPlayer, cubeMesh, chipMat, new Color(0.33f, 0.22f, 0.13f), last ? 10 : 5, 1.5f, 3.5f, 0.03f, 0.06f, 1.6f, 1.2f, 50f, false, false, true);
            Play(chopClip, hp, last ? 1f : 0.85f, 0.9f, 1.1f);
            if (last) Play(crackClip, hp, 1f, 0.95f, 1.05f);
            Shake(last ? 0.06f : 0.03f, last ? 0.3f : 0.14f);
        }
        else
        {
            Color gem;
            bool isGem = GemColor(n, out gem);
            Burst(hp, toPlayer, cubeMesh, chipMat, new Color(0.6f, 0.57f, 0.52f), last ? 26 : 9, 2.5f, 6f, 0.03f, 0.08f, 1.8f, 1.5f, 55f, false, false, true);
            Burst(hp, toPlayer, cubeMesh, sparkMat, new Color(1f, 0.78f, 0.4f), last ? 18 : 10, 3f, 8f, 0.02f, 0.04f, 0.5f, 1.2f, 45f, true, false, false);
            if (isGem) Burst(hp, toPlayer, cubeMesh, sparkMat, gem, last ? 18 : 7, 1.5f, 4.5f, 0.03f, 0.06f, 0.9f, 0.6f, 70f, true, false, false);
            if (last)
            {
                Burst(n.transform.position + Vector3.up * 0.4f, Vector3.up, sphereMesh, dustMat, new Color(0.6f, 0.57f, 0.5f, 0.55f), 7, 0.5f, 1.4f, 0.5f, 0.9f, 1.3f, -0.05f, 80f, true, true, false);
                Play(rockBreakClip, hp, 1f, 0.95f, 1.05f);
            }
            Play(mineClip, hp, last ? 0.9f : 0.7f, 0.9f, 1.12f);
            Shake(last ? 0.09f : 0.03f, last ? 0.3f : 0.12f);
        }

        HitStop(last ? 0.09f : 0.055f);
    }

    void OnTreeLanded(ResourceNode n, Vector3 pos)
    {
        Burst(pos + Vector3.up * 0.2f, Vector3.up, sphereMesh, dustMat, new Color(0.5f, 0.46f, 0.38f, 0.6f), 10, 1f, 2.8f, 0.6f, 1.2f, 1.6f, -0.05f, 85f, true, true, false);
        Burst(pos + Vector3.up * 1.2f, Vector3.up, cubeMesh, chipMat, new Color(0.25f, 0.45f, 0.2f), 26, 1f, 4f, 0.03f, 0.07f, 2.5f, 0.8f, 70f, false, false, true);
        Play(treeLandClip, pos, 1f, 0.92f, 1.05f);

        ToolController tc = ToolController.Instance;
        if (tc != null)
        {
            float d = Vector3.Distance(tc.transform.position, pos);
            if (d < 25f) Shake(Mathf.Lerp(0.13f, 0.02f, d / 25f), 0.5f);
        }
    }

    // ------------------------------------------------------------------ helpers

    bool GemColor(ResourceNode n, out Color c)
    {
        c = Color.white;
        foreach (ResourceDrop d in n.drops)
        {
            if (d.item == ItemType.Ruby || d.item == ItemType.Emerald || d.item == ItemType.Sapphire || d.item == ItemType.Amber)
            {
                c = Inventory.ColorFor(d.item);
                return true;
            }
        }
        return false;
    }

    void Shake(float amp, float time)
    {
        CameraShake cs = CameraShake.Instance;
        if (cs != null) cs.Shake(amp, time);
    }

    void HitStop(float seconds)
    {
        ToolController tc = ToolController.Instance;
        if (tc == null || tc.animator == null) return;
        if (hitStopRoutine != null) StopCoroutine(hitStopRoutine);
        hitStopRoutine = StartCoroutine(HitStopRoutine(tc.animator, seconds));
    }

    IEnumerator HitStopRoutine(Animator a, float seconds)
    {
        a.speed = 0.05f;
        yield return new WaitForSecondsRealtime(seconds);
        a.speed = 1f;
        hitStopRoutine = null;
    }

    static Mesh GetPrimitiveMesh(PrimitiveType t)
    {
        GameObject go = GameObject.CreatePrimitive(t);
        Mesh m = go.GetComponent<MeshFilter>().sharedMesh;
        Destroy(go);
        return m;
    }

    void Burst(Vector3 pos, Vector3 dir, Mesh mesh, Material mat, Color color, int count,
               float speedMin, float speedMax, float sizeMin, float sizeMax, float life,
               float gravity, float angle, bool fade, bool grow, bool collide)
    {
        if (mat == null || mesh == null) return;

        GameObject go = new GameObject("FX");
        go.transform.position = pos;
        go.transform.rotation = dir.sqrMagnitude > 0.001f ? Quaternion.LookRotation(dir.normalized) : Quaternion.identity;

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 1f;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.6f, life);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
        main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
        main.startColor = color;
        main.gravityModifier = gravity;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startRotation3D = true;
        main.startRotationX = new ParticleSystem.MinMaxCurve(0f, 6.28f);
        main.startRotationY = new ParticleSystem.MinMaxCurve(0f, 6.28f);
        main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, 6.28f);
        main.maxParticles = count + 4;

        var em = ps.emission;
        em.rateOverTime = 0f;
        em.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, (short)count) });

        var sh = ps.shape;
        sh.enabled = true;
        sh.shapeType = ParticleSystemShapeType.Cone;
        sh.angle = angle;
        sh.radius = 0.05f;

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, grow ? AnimationCurve.Linear(0f, 0.4f, 1f, 1.8f) : AnimationCurve.Linear(0f, 1f, 1f, 0.2f));

        if (fade)
        {
            Gradient g = new Gradient();
            g.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = g;
        }

        if (collide)
        {
            var c = ps.collision;
            c.enabled = true;
            c.type = ParticleSystemCollisionType.World;
            c.mode = ParticleSystemCollisionMode.Collision3D;
            c.bounce = 0.35f;
            c.dampen = 0.4f;
            c.lifetimeLoss = 0.05f;
            c.radiusScale = 0.5f;
            c.quality = ParticleSystemCollisionQuality.Low;
            c.collidesWith = ~(1 << 8); // not the player layer
        }

        ParticleSystemRenderer rend = go.GetComponent<ParticleSystemRenderer>();
        rend.renderMode = ParticleSystemRenderMode.Mesh;
        rend.mesh = mesh;
        rend.sharedMaterial = mat;
        rend.shadowCastingMode = ShadowCastingMode.Off;
        rend.receiveShadows = false;

        ps.Play();
        Destroy(go, life + 1f);
    }

    void Play(AudioClip clip, Vector3 pos, float volume, float pitchMin, float pitchMax)
    {
        if (clip == null) return;
        GameObject go = new GameObject("SFX");
        go.transform.position = pos;
        AudioSource a = go.AddComponent<AudioSource>();
        a.clip = clip;
        a.volume = volume;
        a.pitch = Random.Range(pitchMin, pitchMax);
        a.spatialBlend = 0.8f;
        a.minDistance = 4f;
        a.maxDistance = 45f;
        a.Play();
        Destroy(go, clip.length / Mathf.Max(0.1f, a.pitch) + 0.2f);
    }

    // ------------------------------------------------------------------ generated sounds

    void BuildSounds()
    {
        System.Random rnd = new System.Random(7);
        float lp = 0f;

        if (chopClip == null)
        {
            lp = 0f;
            chopClip = MakeClip("chop", 0.32f, (t, k) =>
            {
                float n = (float)(rnd.NextDouble() * 2.0 - 1.0);
                lp += (n - lp) * 0.22f;
                float thump = Mathf.Sin(6.2832f * (120f - 70f * k) * t) * Mathf.Exp(-t * 17f);
                return lp * Mathf.Exp(-t * 26f) * 0.9f + thump * 0.9f + n * Mathf.Exp(-t * 95f) * 0.5f;
            });
        }
        if (crackClip == null)
        {
            lp = 0f;
            crackClip = MakeClip("crack", 0.55f, (t, k) =>
            {
                float n = (float)(rnd.NextDouble() * 2.0 - 1.0);
                lp += (n - lp) * 0.4f;
                return lp * Mathf.Exp(-t * 14f) * 0.8f + Mathf.Sin(6.2832f * (95f - 40f * k) * t) * Mathf.Exp(-t * 9f) * 0.7f;
            });
        }
        if (treeLandClip == null)
        {
            lp = 0f;
            treeLandClip = MakeClip("treeLand", 1.0f, (t, k) =>
            {
                float n = (float)(rnd.NextDouble() * 2.0 - 1.0);
                lp += (n - lp) * 0.06f;
                return Mathf.Sin(6.2832f * (58f - 24f * k) * t) * Mathf.Exp(-t * 4.5f) + lp * Mathf.Exp(-t * 7f) * 1.4f;
            });
        }
        if (mineClip == null)
        {
            mineClip = MakeClip("mine", 0.45f, (t, k) =>
            {
                float n = (float)(rnd.NextDouble() * 2.0 - 1.0);
                return n * Mathf.Exp(-t * 70f) * 0.5f
                     + Mathf.Sin(6.2832f * 1750f * t) * Mathf.Exp(-t * 16f) * 0.45f
                     + Mathf.Sin(6.2832f * 2630f * t) * Mathf.Exp(-t * 22f) * 0.3f
                     + Mathf.Sin(6.2832f * 220f * t) * Mathf.Exp(-t * 30f) * 0.4f;
            });
        }
        if (rockBreakClip == null)
        {
            rockBreakClip = MakeClip("rockBreak", 0.6f, (t, k) =>
            {
                float n = (float)(rnd.NextDouble() * 2.0 - 1.0);
                return n * Mathf.Exp(-t * 16f) * 0.8f
                     + Mathf.Sin(6.2832f * 85f * t) * Mathf.Exp(-t * 12f) * 0.7f
                     + Mathf.Sin(6.2832f * 1320f * t) * Mathf.Exp(-t * 22f) * 0.2f;
            });
        }
        if (swooshClip == null)
        {
            lp = 0f;
            swooshClip = MakeClip("swoosh", 0.35f, (t, k) =>
            {
                float n = (float)(rnd.NextDouble() * 2.0 - 1.0);
                float s = Mathf.Sin(k * Mathf.PI);
                lp += (n - lp) * (0.05f + 0.25f * s);
                return lp * s * s * 1.6f;
            });
        }
    }

    static AudioClip MakeClip(string clipName, float seconds, System.Func<float, float, float> sample)
    {
        const int rate = 44100;
        int n = (int)(rate * seconds);
        float[] data = new float[n];
        for (int i = 0; i < n; i++)
            data[i] = Mathf.Clamp(sample(i / (float)rate, i / (float)n), -1f, 1f);
        AudioClip c = AudioClip.Create(clipName, n, 1, rate, false);
        c.SetData(data, 0);
        return c;
    }
}
