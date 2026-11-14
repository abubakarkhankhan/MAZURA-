using StarterAssets;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Shield in the left hand (crafted at the Workshop or found in a chest).
/// Visible while the sword (key 3) is out. Hold RIGHT MOUSE to block:
/// the player raises the shield, walks slower and cannot swing.
/// A hit from the front while blocking does only 15% damage (sparks + clang).
/// Added to the player automatically; the model is the "Tool_Shield" object under the left hand.
/// </summary>
public class ShieldController : MonoBehaviour
{
    public static ShieldController Instance { get; private set; }
    public static bool IsBlocking { get { return Instance != null && Instance.blocking; } }

    /// <summary>True once the player owns a shield.</summary>
    public static bool Owned { get { return CraftingState.Instance != null && CraftingState.Instance.Count(CraftItem.Shield) > 0; } }

    [Range(0f, 1f)] public float blockReduction = 0.85f;
    [Tooltip("Hits within this angle of where the player faces are blocked.")]
    public float blockAngle = 75f;
    [Tooltip("Walk speed multiplier while blocking.")]
    public float moveSlow = 0.55f;

    static readonly int BlockHash = Animator.StringToHash("Block");

    GameObject shield;
    ToolController tc;
    ThirdPersonController tpc;
    HealthManager hm;
    float baseMove, baseSprint;
    bool blocking;
    AudioClip clang;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        ToolController t = Object.FindAnyObjectByType<ToolController>();
        if (t != null && t.GetComponent<ShieldController>() == null) t.gameObject.AddComponent<ShieldController>();
    }

    void Awake()
    {
        Instance = this;
        tc = GetComponent<ToolController>();
        tpc = GetComponent<ThirdPersonController>();
        hm = GetComponent<HealthManager>();
        if (tpc != null) { baseMove = tpc.MoveSpeed; baseSprint = tpc.SprintSpeed; }
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
            if (t.name == "Tool_Shield" && t.gameObject.activeInHierarchy == t.gameObject.activeSelf) { shield = t.gameObject; break; }
        if (shield == null)
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
                if (t.name == "Tool_Shield") { shield = t.gameObject; break; }
        clang = MakeClang();
    }

    void OnDisable() { SetBlocking(false); }

    void Update()
    {
        bool dead = hm != null && hm.IsDead;
        bool show = Owned && tc != null && tc.CurrentTool == ToolType.Weapon && !TorchController.IsHeld && !dead;
        if (shield != null && shield.activeSelf != show) shield.SetActive(show);

        Mouse mouse = Mouse.current;
        bool want = show && mouse != null && mouse.rightButton.isPressed
                    && Cursor.lockState == CursorLockMode.Locked && !PlacementController.IsPlacing;
        if (want != blocking) SetBlocking(want);
    }

    void SetBlocking(bool on)
    {
        blocking = on;
        Animator anim = tc != null ? tc.animator : null;
        if (anim != null) anim.SetBool(BlockHash, on);
        if (tpc != null)
        {
            tpc.MoveSpeed = on ? baseMove * moveSlow : baseMove;
            tpc.SprintSpeed = on ? baseMove * moveSlow : baseSprint;
        }
    }

    /// <summary>
    /// Called before the player takes damage. Returns the damage that gets through.
    /// 'from' is where the attack came from (the enemy position).
    /// </summary>
    public static float Filter(float amount, Vector3 from)
    {
        if (!IsBlocking) return amount;
        Transform me = Instance.transform;
        Vector3 to = from - me.position;
        to.y = 0f;
        if (to.sqrMagnitude < 0.0001f) return amount;
        if (Vector3.Angle(me.forward, to) > Instance.blockAngle) return amount;
        Instance.OnBlocked(from);
        return amount * (1f - Instance.blockReduction);
    }

    void OnBlocked(Vector3 from)
    {
        Vector3 at = shield != null ? shield.transform.position : transform.position + Vector3.up * 1.2f + transform.forward * 0.4f;
        AudioSource.PlayClipAtPoint(clang, at, 0.9f);
        Sparks(at, (from - at).normalized);
        CameraShake cs = CameraShake.Instance;
        if (cs != null) cs.Shake(0.05f, 0.18f);
        HudMessage.Float("Blocked!", new Color(0.75f, 0.88f, 1f));
    }

    static void Sparks(Vector3 at, Vector3 dir)
    {
        GameObject go = new GameObject("BlockSparks");
        go.transform.position = at;
        go.transform.rotation = Quaternion.LookRotation(dir.sqrMagnitude > 0.001f ? dir : Vector3.up);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = false; main.playOnAwake = false;
        main.duration = 0.3f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.05f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.9f, 0.5f), new Color(1f, 0.6f, 0.2f));
        main.gravityModifier = 1.2f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var em = ps.emission; em.rateOverTime = 0f;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 35f; sh.radius = 0.05f;
        ParticleSystemRenderer pr = go.GetComponent<ParticleSystemRenderer>();
        pr.sharedMaterial = SoftFX.Spark();
        pr.renderMode = ParticleSystemRenderMode.Stretch;
        pr.velocityScale = 0.04f;
        ps.Emit(22);
        Object.Destroy(go, 1f);
    }

    /// <summary>Wood-and-metal thunk with a short ring.</summary>
    static AudioClip MakeClang()
    {
        const int rate = 44100;
        int n = (int)(rate * 0.45f);
        float[] d = new float[n];
        System.Random rnd = new System.Random(21);
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float noise = (float)(rnd.NextDouble() * 2.0 - 1.0);
            float thunk = Mathf.Sin(6.2832f * 140f * t) * Mathf.Exp(-t * 30f) * 0.9f;
            float ring = (Mathf.Sin(6.2832f * 980f * t) * 0.35f + Mathf.Sin(6.2832f * 2350f * t) * 0.2f) * Mathf.Exp(-t * 9f);
            d[i] = Mathf.Clamp((thunk + ring + noise * Mathf.Exp(-t * 90f) * 0.5f) * 0.8f, -1f, 1f);
        }
        AudioClip c = AudioClip.Create("shieldBlock", n, 1, rate, false);
        c.SetData(d, 0);
        return c;
    }
}
