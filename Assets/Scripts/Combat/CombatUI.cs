using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Combat feedback on screen. Created automatically when the game starts.
///   - floating damage numbers (white-gold, crits bigger and orange, kills red)
///   - red vignette when the player is hit, plus a slow heartbeat pulse at low health
///   - a small health bar over every enemy that was hit (dark frame, gold border, red fill, a pale "damage chunk" that drains)
/// </summary>
public class CombatUI : MonoBehaviour
{
    static CombatUI instance;

    // ---- vignette
    RawImage vignette;
    float flash;
    HealthManager playerHealth;

    // ---- bars
    class Bar
    {
        public EnemyBase enemy;
        public Transform root, fill, lag;
        public float lagValue = 1f;
        public float height;
    }
    readonly Dictionary<EnemyBase, Bar> bars = new Dictionary<EnemyBase, Bar>();
    readonly List<EnemyBase> dead = new List<EnemyBase>();
    Mesh barMesh;
    Material goldMat, bgMat, fillMat, lagMat;

    const float BarWidth = 1.1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (instance != null) return;
        instance = new GameObject("CombatUI").AddComponent<CombatUI>();
    }

    void Awake()
    {
        BuildVignette();
        barMesh = BuildBarMesh();
        goldMat = Unlit(new Color(0.85f, 0.68f, 0.28f));
        bgMat = Unlit(new Color(0.05f, 0.04f, 0.04f));
        fillMat = Unlit(new Color(0.78f, 0.1f, 0.1f));
        lagMat = Unlit(new Color(0.95f, 0.85f, 0.55f));
    }

    void OnEnable()
    {
        PlayerHurtFX.OnPlayerHit += OnPlayerHit;
        EnemyBase.OnAnyDamaged += OnEnemyDamaged;
    }

    void OnDisable()
    {
        PlayerHurtFX.OnPlayerHit -= OnPlayerHit;
        EnemyBase.OnAnyDamaged -= OnEnemyDamaged;
    }

    // ------------------------------------------------------------------ damage numbers

    public static void SpawnNumber(Vector3 pos, float amount, bool crit, bool kill)
    {
        GameObject go = new GameObject("DamageNumber");
        go.transform.position = pos + Random.insideUnitSphere * 0.15f;
        TextMeshPro t = go.AddComponent<TextMeshPro>();
        t.text = Mathf.RoundToInt(amount) + (crit ? "!" : "");
        t.alignment = TextAlignmentOptions.Center;
        t.fontStyle = FontStyles.Bold;
        t.fontSize = crit ? 5.5f : 4f;
        t.color = kill ? new Color(1f, 0.25f, 0.2f) : (crit ? new Color(1f, 0.55f, 0.15f) : new Color(1f, 0.92f, 0.65f));
        t.outlineWidth = 0.28f;
        t.outlineColor = new Color32(20, 10, 5, 255);
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.raycastTarget = false;
        Shader overlay = Shader.Find("TextMeshPro/Distance Field Overlay");
        if (overlay != null) t.fontMaterial.shader = overlay;   // never hidden by leaves or walls
        go.AddComponent<FloatingText>().Init(crit);
    }

    class FloatingText : MonoBehaviour
    {
        TextMeshPro text;
        float age, life = 0.9f;
        Vector3 drift;
        bool crit;

        public void Init(bool isCrit)
        {
            text = GetComponent<TextMeshPro>();
            crit = isCrit;
            drift = new Vector3(Random.Range(-0.35f, 0.35f), 1.5f, 0f);
        }

        void LateUpdate()
        {
            age += Time.unscaledDeltaTime;
            float k = age / life;
            if (k >= 1f) { Destroy(gameObject); return; }
            Camera c = Camera.main;
            if (c != null)
            {
                transform.rotation = c.transform.rotation;
                float dist = Vector3.Distance(c.transform.position, transform.position);
                float pop = k < 0.12f ? Mathf.Lerp(0.5f, 1.25f, k / 0.12f) : Mathf.Lerp(1.25f, 1f, Mathf.Clamp01((k - 0.12f) / 0.15f));
                transform.localScale = Vector3.one * Mathf.Clamp(dist * 0.16f, 0.5f, 2.2f) * pop * (crit ? 1.2f : 1f);
            }
            transform.position += new Vector3(drift.x * 0.3f, drift.y * (1f - k) * 0.7f, 0f) * Time.unscaledDeltaTime;
            Color col = text.color;
            col.a = k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f;
            text.color = col;
        }
    }

    // ------------------------------------------------------------------ vignette

    void BuildVignette()
    {
        GameObject cgo = new GameObject("HurtVignetteCanvas");
        cgo.transform.SetParent(transform, false);
        Canvas canvas = cgo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40;

        GameObject go = new GameObject("Vignette");
        go.transform.SetParent(cgo.transform, false);
        vignette = go.AddComponent<RawImage>();
        vignette.raycastTarget = false;
        vignette.texture = MakeVignetteTexture();
        vignette.color = new Color(1f, 1f, 1f, 0f);
        RectTransform rt = vignette.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static Texture2D MakeVignetteTexture()
    {
        const int s = 128;
        Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float dx = (x / (s - 1f)) * 2f - 1f, dy = (y / (s - 1f)) * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / 1.414f;
                float a = Mathf.Clamp01((d - 0.35f) / 0.65f);
                a = a * a;
                tex.SetPixel(x, y, new Color(0.7f, 0.02f, 0.03f, a));
            }
        tex.Apply();
        return tex;
    }

    void OnPlayerHit(Vector3 from, float amount)
    {
        flash = Mathf.Max(flash, Mathf.Clamp(0.3f + amount / 70f, 0.35f, 0.75f));
    }

    // ------------------------------------------------------------------ health bars

    void OnEnemyDamaged(EnemyBase e, float amount) { }   // health bars live in EnemyHealthUI now

    Transform MakeQuad(Transform parent, Material mat, Vector3 localPos, float width, float height)
    {
        GameObject go = new GameObject("q");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = new Vector3(width, height, 1f);
        go.AddComponent<MeshFilter>().sharedMesh = barMesh;
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        return go.transform;
    }

    static Mesh BuildBarMesh()
    {
        Mesh m = new Mesh { name = "BarQuad" };
        m.vertices = new Vector3[] { new Vector3(0, -0.5f, 0), new Vector3(1, -0.5f, 0), new Vector3(1, 0.5f, 0), new Vector3(0, 0.5f, 0) };
        m.triangles = new int[] { 0, 2, 1, 0, 3, 2 };
        m.RecalculateBounds();
        return m;
    }

    static Material Unlit(Color c)
    {
        Shader s = Shader.Find("Universal Render Pipeline/Unlit");
        if (s == null) s = Shader.Find("Unlit/Color");
        Material m = new Material(s);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);
        return m;
    }

    // ------------------------------------------------------------------ update

    void LateUpdate()
    {
        UpdateVignette();
    }

    void UpdateVignette()
    {
        flash = Mathf.MoveTowards(flash, 0f, Time.unscaledDeltaTime * 1.6f);
        float a = flash;

        if (playerHealth == null) playerHealth = FindAnyObjectByType<HealthManager>();
        if (playerHealth != null)
        {
            float frac = playerHealth.CurrentHealth / Mathf.Max(1f, playerHealth.maxHealth);
            if (playerHealth.IsDead) a = Mathf.Max(a, 0.85f);
            else if (frac < 0.3f)
            {
                float danger = 1f - frac / 0.3f;
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * (3f + danger * 3f));
                a = Mathf.Max(a, Mathf.Lerp(0.15f, 0.45f, pulse) * (0.5f + 0.5f * danger));
            }
        }
        vignette.color = new Color(1f, 1f, 1f, a);
    }

    void UpdateBars()
    {
        Camera cam = Camera.main;
        dead.Clear();
        foreach (KeyValuePair<EnemyBase, Bar> kv in bars)
        {
            EnemyBase e = kv.Key;
            Bar b = kv.Value;
            if (e == null || e.IsDead) { dead.Add(e); continue; }

            bool show = e.TimeSinceHit < 4f || (e.data.isBoss && e.IsHuntingPlayer);
            b.root.gameObject.SetActive(show);
            if (!show || cam == null) continue;

            float frac = Mathf.Clamp01(e.Health / e.data.maxHealth);
            b.lagValue = Mathf.MoveTowards(b.lagValue, frac, Time.deltaTime * 0.35f * (e.TimeSinceHit > 0.5f ? 1f : 0f));
            b.fill.localScale = new Vector3(BarWidth * frac, 0.1f, 1f);
            b.lag.localScale = new Vector3(BarWidth * Mathf.Max(b.lagValue, frac), 0.1f, 1f);

            b.root.position = e.transform.position + Vector3.up * b.height;
            Vector3 away = b.root.position - cam.transform.position;
            if (away.sqrMagnitude > 0.001f) b.root.rotation = Quaternion.LookRotation(away, Vector3.up);
            float s = Mathf.Clamp(away.magnitude * 0.09f, 0.8f, 2.4f);
            b.root.localScale = Vector3.one * s;
        }
        foreach (EnemyBase e in dead)
        {
            if (bars.ContainsKey(e)) { Destroy(bars[e].root.gameObject); bars.Remove(e); }
        }
    }
}
