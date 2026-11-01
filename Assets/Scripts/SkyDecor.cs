using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Visible sky for Mazura: a sun by day, a moon and stars by night, and drifting clouds by day.
/// Everything follows the camera, so it always looks far away. The objects are created when the
/// game starts, so nothing extra stays in the scene. It reads the DayNightManager and its
/// Directional Light, so the sky always matches the time of day.
/// Put this script on one empty GameObject (for example "SkyDecor").
/// </summary>
public class SkyDecor : MonoBehaviour
{
    [Header("References (found automatically when empty)")]
    public DayNightManager dayNight;
    public Light sunLight;
    public Camera cam;

    [Header("Sky objects")]
    public float skyRadius = 380f;
    public int starCount = 350;
    public int cloudCount = 12;
    [Tooltip("Cloud drift in degrees per second.")]
    public float cloudSpeed = 0.6f;
    public float sunSize = 40f;
    public float moonSize = 30f;

    [Header("Background and fog colour (follows day and night)")]
    public bool tintBackgroundAndFog = true;
    public Color daySky = new Color(0.10f, 0.13f, 0.11f);
    [Tooltip("ON: the day fog/background colour is the fog colour already set in Lighting, so the forest keeps its dark look.")]
    public bool useSceneFogForDay = true;

    [Header("Cloud models (optional, 3D clouds from the Stylized Nature MegaKit)")]
    public GameObject[] cloudPrefabs;
    [Tooltip("Width of one cloud in world units (random between these two).")]
    public Vector2 cloudWidth = new Vector2(110f, 190f);
    public Color nightSky = new Color(0.015f, 0.02f, 0.045f);

    Transform root, cloudPivot, sunT, moonT;
    float baseYaw;
    Material sunMat, moonMat, starMat, cloudMat, cloudMeshMat;

    void Start()
    {
        if (dayNight == null) dayNight = FindAnyObjectByType<DayNightManager>();
        if (sunLight == null) sunLight = RenderSettings.sun;
        if (cam == null) cam = Camera.main;
        if (sunLight != null) baseYaw = sunLight.transform.eulerAngles.y;
        if (useSceneFogForDay) daySky = RenderSettings.fogColor;

        Shader sh = Shader.Find("Mazura/SkyUnlit");
        if (sh == null || sunLight == null || dayNight == null || cam == null)
        {
            Debug.LogError("SkyDecor: missing shader, light, DayNightManager or camera. Sky disabled.");
            enabled = false;
            return;
        }

        sunMat = MakeMat(sh, BlendMode.One, BlendMode.One);                    // additive glow
        moonMat = MakeMat(sh, BlendMode.One, BlendMode.One);
        starMat = MakeMat(sh, BlendMode.One, BlendMode.One);
        cloudMat = MakeMat(sh, BlendMode.SrcAlpha, BlendMode.OneMinusSrcAlpha); // soft cloud
        Shader cloudSh = Shader.Find("Mazura/SkyCloud");
        if (cloudSh != null) cloudMeshMat = new Material(cloudSh);

        root = new GameObject("SkyDecor_Runtime").transform;
        System.Random rnd = new System.Random(12345);

        sunT = MakeSphere("Sun", sunMat, root);
        sunT.localScale = Vector3.one * sunSize;
        moonT = MakeSphere("Moon", moonMat, root);
        moonT.localScale = Vector3.one * moonSize;

        BuildStars(rnd);
        BuildClouds(rnd);
    }

    void LateUpdate()
    {
        if (root == null) return;
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        root.position = cam.transform.position;
        cloudPivot.Rotate(Vector3.up, cloudSpeed * Time.deltaTime, Space.World);

        // 0 = night, 1 = full day (read from the light, so it follows the day/night gradients)
        float dayF = Mathf.InverseLerp(0.12f, 0.5f, sunLight.intensity);

        // ---- Sun: sits in the direction the light comes from
        Vector3 sunDir = -sunLight.transform.forward;
        float sunAbove = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.02f, 0.12f, sunDir.y));
        sunT.position = root.position + sunDir * skyRadius;
        Color sunCol = Color.Lerp(sunLight.color, new Color(1f, 0.95f, 0.7f), 0.5f) * (dayF * sunAbove * 1.3f);
        sunCol.a = 1f;
        sunMat.SetColor("_Color", sunCol);

        // ---- Moon: travels across the sky during the night
        float prog = dayNight.CurrentPhase == DayNightManager.Phase.Night ? dayNight.PhaseProgress : 0f;
        float moonPitch = Mathf.Lerp(20f, 160f, prog);
        Vector3 moonDir = -(Quaternion.Euler(moonPitch, baseYaw, 0f) * Vector3.forward);
        float moonAbove = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.02f, 0.12f, moonDir.y));
        moonT.position = root.position + moonDir * skyRadius;
        Color moonCol = new Color(0.75f, 0.85f, 1f) * ((1f - dayF) * moonAbove);
        moonCol.a = 1f;
        moonMat.SetColor("_Color", moonCol);

        // ---- Stars: only at night, with a slight twinkle
        float starF = (1f - dayF) * (1f - dayF);
        float twinkle = 0.88f + 0.12f * Mathf.Sin(Time.time * 2.3f);
        Color starCol = new Color(0.85f, 0.9f, 1f) * (starF * twinkle);
        starCol.a = 1f;
        starMat.SetColor("_Color", starCol);

        // ---- Clouds: bright by day, faint dark shapes at night
        Color dayCloud = Color.Lerp(sunLight.color, Color.white, 0.6f);
        Color nightCloud = new Color(0.12f, 0.15f, 0.25f);
        Color cloudCol = Color.Lerp(nightCloud, dayCloud, dayF);
        cloudCol.a = Mathf.Lerp(0.18f, 0.9f, dayF);
        cloudMat.SetColor("_Color", cloudCol);
        if (cloudMeshMat != null) cloudMeshMat.SetColor("_Color", cloudCol);

        // ---- Background and fog colour follow the time of day
        if (tintBackgroundAndFog)
        {
            Color sky = Color.Lerp(nightSky, daySky, dayF);
            cam.backgroundColor = sky;
            RenderSettings.fogColor = sky;
        }
    }

    void OnDestroy()
    {
        if (root != null) Destroy(root.gameObject);
    }

    // ---------------------------------------------------------------- builders

    void BuildStars(System.Random rnd)
    {
        int n = Mathf.Max(0, starCount);
        Vector3[] verts = new Vector3[n * 4];
        int[] tris = new int[n * 6];
        for (int i = 0; i < n; i++)
        {
            Vector3 dir = new Vector3((float)(rnd.NextDouble() * 2 - 1), (float)(0.03 + rnd.NextDouble() * 0.97), (float)(rnd.NextDouble() * 2 - 1)).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, dir);
            if (right.sqrMagnitude < 0.0001f) right = Vector3.right;
            right.Normalize();
            Vector3 up = Vector3.Cross(dir, right).normalized;
            float s = skyRadius * (0.0022f + (float)rnd.NextDouble() * 0.0028f);
            Vector3 c = dir * skyRadius;
            int v = i * 4;
            verts[v] = c - right * s - up * s;
            verts[v + 1] = c + right * s - up * s;
            verts[v + 2] = c + right * s + up * s;
            verts[v + 3] = c - right * s + up * s;
            int t = i * 6;
            tris[t] = v; tris[t + 1] = v + 2; tris[t + 2] = v + 1;
            tris[t + 3] = v; tris[t + 4] = v + 3; tris[t + 5] = v + 2;
        }
        Mesh mesh = new Mesh();
        mesh.name = "Stars";
        mesh.vertices = verts;
        mesh.triangles = tris;
        mesh.bounds = new Bounds(Vector3.zero, Vector3.one * skyRadius * 2.5f);

        GameObject go = new GameObject("Stars");
        go.transform.SetParent(root, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = starMat;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;
    }

void BuildClouds(System.Random rnd)
    {
        cloudPivot = new GameObject("Clouds").transform;
        cloudPivot.SetParent(root, false);

        bool haveModels = cloudMeshMat != null && cloudPrefabs != null && cloudPrefabs.Length > 0;
        for (int c = 0; c < cloudCount; c++)
        {
            float az = (float)rnd.NextDouble() * 360f;
            float elev = 12f + (float)rnd.NextDouble() * 38f;
            Vector3 dir = Quaternion.Euler(-elev, az, 0f) * Vector3.forward;
            Transform group = new GameObject("Cloud" + c).transform;
            group.SetParent(cloudPivot, false);
            group.localPosition = dir * (skyRadius * 0.92f);

            GameObject prefab = haveModels ? cloudPrefabs[c % cloudPrefabs.Length] : null;
            if (prefab != null) BuildCloudModel(group, prefab, rnd);
            else BuildCloudBlobs(group, rnd);
        }
    }

    // One cloud from a 3D model (Cloud_1..3). Scaled so its width is about cloudWidth, whatever the import scale is.
    void BuildCloudModel(Transform group, GameObject prefab, System.Random rnd)
    {
        GameObject go = Instantiate(prefab, group);
        go.name = "CloudModel";
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f);

        Renderer[] rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return;
        Bounds b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        float w = Mathf.Max(b.size.x, b.size.z);
        float target = Mathf.Lerp(cloudWidth.x, cloudWidth.y, (float)rnd.NextDouble());
        if (w > 0.0001f) go.transform.localScale = go.transform.localScale * (target / w);

        foreach (Renderer r in rs)
        {
            Material[] mats = new Material[r.sharedMaterials.Length];
            for (int i = 0; i < mats.Length; i++) mats[i] = cloudMeshMat;
            r.sharedMaterials = mats;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
        }
        foreach (Collider col in go.GetComponentsInChildren<Collider>()) Destroy(col);
    }

    // Old simple blob cloud, used only when no cloud models are assigned.
    void BuildCloudBlobs(Transform group, System.Random rnd)
    {
        int blobs = 4 + rnd.Next(3);
        for (int b = 0; b < blobs; b++)
        {
            Transform blob = MakeSphere("Blob", cloudMat, group);
            blob.localPosition = new Vector3((float)(rnd.NextDouble() * 2 - 1) * 60f, (float)(rnd.NextDouble() * 2 - 1) * 6f, (float)(rnd.NextDouble() * 2 - 1) * 35f);
            blob.localScale = new Vector3(45f + (float)rnd.NextDouble() * 45f, 14f + (float)rnd.NextDouble() * 8f, 30f + (float)rnd.NextDouble() * 15f);
        }
    }

    Transform MakeSphere(string objName, Material m, Transform parent)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = objName;
        Destroy(go.GetComponent<Collider>());
        Renderer r = go.GetComponent<Renderer>();
        r.sharedMaterial = m;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    Material MakeMat(Shader sh, BlendMode src, BlendMode dst)
    {
        Material m = new Material(sh);
        m.SetFloat("_SrcBlend", (float)src);
        m.SetFloat("_DstBlend", (float)dst);
        return m;
    }
}
