using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Fake light shafts (god rays) for the maze. Soft additive beams that lean along the sun direction,
/// stay around the camera, and fade with the sun: strong in the morning and evening, soft at noon, gone at night.
/// Put this script on one empty GameObject (for example "LightShafts") and assign FX_Glow as Glow Material.
/// </summary>
public class LightShafts : MonoBehaviour
{
    public Material glowMaterial;
    public Light sunLight;
    public Camera cam;

    [Range(0f, 2f)] public float strength = 1f;
    public int beamCount = 16;
    [Tooltip("Beams appear inside this radius around the camera.")]
    public float radius = 18f;
    public Vector2 beamWidth = new Vector2(1.5f, 3.5f);
    public Vector2 beamLength = new Vector2(14f, 24f);
    [Tooltip("Slow shimmer of the beams (0 = still).")]
    public float shimmer = 0.25f;
    public float openCheckRadius = 0.7f;
    public float beamIntensity = 2f;

    Transform root;
    Material mat;
    Transform[] beams;
    Vector3[] offsets;
    float[] phase;
    float[] size;
    float[] lengths;

    void Start()
    {
        if (sunLight == null) sunLight = RenderSettings.sun;
        if (cam == null) cam = Camera.main;
        Shader sh = Shader.Find("Mazura/ParticleSoft");
        if (glowMaterial == null && sh != null)
        {
            glowMaterial = new Material(sh);
            glowMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            glowMaterial.SetFloat("_DstBlend", (float)BlendMode.One);
        }
        if (glowMaterial == null || sunLight == null || cam == null)
        {
            Debug.LogError("LightShafts: missing material, sun light or camera. Disabled.");
            enabled = false;
            return;
        }

        mat = new Material(glowMaterial) { mainTexture = MakeBeamTexture() };
        mat.SetFloat("_NearFade", 4f);
        mat.SetFloat("_Intensity", 1f);

        root = new GameObject("LightShafts_Runtime").transform;
        beams = new Transform[beamCount];
        offsets = new Vector3[beamCount];
        phase = new float[beamCount];
        size = new float[beamCount];
        lengths = new float[beamCount];

        System.Random rnd = new System.Random(777);
        for (int i = 0; i < beamCount; i++)
        {
            float a = (float)rnd.NextDouble() * Mathf.PI * 2f;
            float r = Mathf.Sqrt((float)rnd.NextDouble()) * radius;
            offsets[i] = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
            phase[i] = (float)rnd.NextDouble() * 10f;
            size[i] = Mathf.Lerp(beamWidth.x, beamWidth.y, (float)rnd.NextDouble());
            lengths[i] = Mathf.Lerp(beamLength.x, beamLength.y, (float)rnd.NextDouble());
            beams[i] = MakeBeam(i);
        }
    }

void LateUpdate()
    {
        if (root == null) return;
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        Vector3 toSun = -sunLight.transform.forward;
        float sunAbove = Mathf.Clamp01(toSun.y * 3f);
        float dayF = Mathf.InverseLerp(0.12f, 0.5f, sunLight.intensity);
        float low = 1f - Mathf.Clamp01(Mathf.Abs(toSun.y) * 1.4f);
        float f = dayF * sunAbove * Mathf.Lerp(0.55f, 1f, low) * strength;

        Color c = Color.Lerp(sunLight.color, new Color(1f, 0.95f, 0.8f), 0.5f);
        mat.SetColor("_Color", new Color(c.r, c.g, c.b, Mathf.Clamp01(0.35f * f)));
        mat.SetFloat("_Intensity", beamIntensity);

        bool visible = f > 0.01f;
        Vector3 center = cam.transform.position;
        center.y = 0f;
        Vector3 camPos = cam.transform.position;

        for (int i = 0; i < beams.Length; i++)
        {
            Vector3 basePos = center + offsets[i];
            bool open = visible && !Physics.CheckSphere(basePos + Vector3.up * 1.6f, openCheckRadius, ~0, QueryTriggerInteraction.Ignore);
            if (beams[i].gameObject.activeSelf != open) beams[i].gameObject.SetActive(open);
            if (!open) continue;

            Vector3 axis = toSun;
            Vector3 mid = basePos + axis * (lengths[i] * 0.5f);

            Vector3 toCam = camPos - mid;
            Vector3 side = Vector3.Cross(axis, toCam);
            if (side.sqrMagnitude < 0.0001f) side = Vector3.right;
            side.Normalize();
            Vector3 normal = Vector3.Cross(side, axis).normalized;

            Transform t = beams[i];
            t.position = mid;
            t.rotation = Quaternion.LookRotation(-normal, axis);
            float pulse = 1f + Mathf.Sin(Time.time * shimmer + phase[i]) * 0.12f;
            t.localScale = new Vector3(size[i] * pulse, lengths[i], 1f);
        }
    }

    void OnDestroy()
    {
        if (root != null) Destroy(root.gameObject);
    }

    Transform MakeBeam(int i)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "Beam" + i;
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(root, false);
        Renderer r = go.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        return go.transform;
    }

    // Soft beam: smooth across the width, brighter near the ground and fading out toward the top.
Texture2D MakeBeamTexture()
    {
        int w = 32, h = 128;
        Texture2D t = new Texture2D(w, h, TextureFormat.RGBA32, false);
        t.wrapMode = TextureWrapMode.Clamp;
        Color[] px = new Color[w * h];
        for (int y = 0; y < h; y++)
        {
            float v = (float)y / (h - 1);
            // visible along most of the beam: soft start near the ground, long soft fade toward the sun
            float along = Mathf.SmoothStep(0f, 0.12f, v) * (1f - Mathf.SmoothStep(0.45f, 1f, v));
            for (int x = 0; x < w; x++)
            {
                float u = Mathf.Abs((x + 0.5f) / w * 2f - 1f);
                float across = 1f - Mathf.SmoothStep(0f, 1f, u);
                across *= across;
                px[y * w + x] = new Color(1f, 1f, 1f, across * along);
            }
        }
        t.SetPixels(px);
        t.Apply();
        return t;
    }
}
