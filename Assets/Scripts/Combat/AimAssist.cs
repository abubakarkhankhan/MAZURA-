using StarterAssets;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Third-person aim, like in shooters. Created automatically when the game starts.
///   - A crosshair in the middle of the screen (ring + dot).
///   - It turns gold when an enemy is under it, red when that enemy is inside sword reach.
///   - It kicks when you swing, and a hit-marker (X) flashes when the swing lands (bigger and red on a kill).
///   - When you swing, the player turns to where the crosshair points; if an enemy is under the crosshair
///     and just out of reach he steps in a little, so a swing in the right direction lands.
/// </summary>
public class AimAssist : MonoBehaviour
{
    public static AimAssist Instance { get; private set; }
    /// <summary>The enemy currently under the crosshair (can be null).</summary>
    public static EnemyBase Target { get { return Instance != null ? Instance.target : null; } }

    [Tooltip("How far the crosshair can pick an enemy.")]
    public float aimRange = 12f;
    [Tooltip("An enemy this close (in degrees) to the screen centre counts as aimed at.")]
    public float aimAngle = 7f;
    [Tooltip("On a swing the player steps in up to this far to reach the aimed enemy.")]
    public float maxLunge = 1.1f;

    EnemyBase target;
    RectTransform canvasRect, cross, marker;
    Image crossImg, markerImg;
    float kick, hitFlash, killFlash, hot;
    Transform player;
    CharacterController cc;
    ThirdPersonController tpc;
    System.Reflection.FieldInfo targetRotField;
    Vector3 turnTo; float turnLeft, lungeLeft; Vector3 lungeStep;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (Instance != null) return;
        new GameObject("AimAssist").AddComponent<AimAssist>();
    }

    void Awake()
    {
        Instance = this;
        canvasRect = CombatSkin.MakeCanvas(transform, "CrosshairCanvas", 4);
        cross = MakeImage("Crosshair", MakeCrosshair(), 54f, out crossImg);
        marker = MakeImage("HitMarker", MakeHitMarker(), 62f, out markerImg);
        markerImg.color = new Color(1f, 1f, 1f, 0f);
        targetRotField = typeof(ThirdPersonController).GetField("_targetRotation", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    }

    RectTransform MakeImage(string name, Sprite sprite, float size, out Image img)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rt = (RectTransform)go.transform;
        rt.SetParent(canvasRect, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(size, size);
        img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        return rt;
    }

    void OnEnable()
    {
        ToolController.OnSwingStart += OnSwing;
        EnemyBase.OnAnyDamaged += OnDamaged;
        EnemyBase.OnAnyDied += OnDied;
    }

    void OnDisable()
    {
        ToolController.OnSwingStart -= OnSwing;
        EnemyBase.OnAnyDamaged -= OnDamaged;
        EnemyBase.OnAnyDied -= OnDied;
    }

    void OnDamaged(EnemyBase e, float amount) { hitFlash = 1f; }
    void OnDied(EnemyBase e) { killFlash = 1f; }

    bool FindPlayer()
    {
        if (player != null) return true;
        ToolController tc = ToolController.Instance;
        if (tc == null) return false;
        player = tc.transform;
        cc = player.GetComponent<CharacterController>();
        tpc = player.GetComponent<ThirdPersonController>();
        return true;
    }

    static Vector3 Chest(EnemyBase e)
    {
        Collider c = e.GetComponentInChildren<Collider>();
        if (c != null) return new Vector3(c.bounds.center.x, c.bounds.min.y + c.bounds.size.y * 0.6f, c.bounds.center.z);
        return e.transform.position + Vector3.up * 1.1f;
    }

    /// <summary>True when this enemy is really in view: no maze wall between the camera and the enemy
    /// (its chest or its head). Enemies on the other side of a wall get no bar, no target panel, no aim.</summary>
    public static bool InSight(EnemyBase e)
    {
        ToolController tc = ToolController.Instance;
        if (e == null || tc == null) return false;
        Camera cam = Camera.main;
        Vector3 from = cam != null ? cam.transform.position : tc.transform.position + Vector3.up * 1.5f;
        Collider c = e.GetComponentInChildren<Collider>();
        Vector3 chest = Chest(e);
        Vector3 head = c != null ? new Vector3(c.bounds.center.x, c.bounds.max.y - 0.15f, c.bounds.center.z) : chest + Vector3.up * 0.6f;
        return !EnemyBase.WallBetween(from, chest) || !EnemyBase.WallBetween(from, head);
    }

    /// <summary>The enemy under the crosshair: first what a thick ray from the screen centre hits,
    /// otherwise the enemy closest to the centre within a few degrees.</summary>
    EnemyBase Pick(Camera cam)
    {
        Vector3 origin = cam.transform.position;
        Vector3 fwd = cam.transform.forward;

        RaycastHit hit;
        if (Physics.SphereCast(origin, 0.3f, fwd, out hit, aimRange + 4f, ~(1 << 8), QueryTriggerInteraction.Ignore))
        {
            EnemyBase e = hit.collider.GetComponentInParent<EnemyBase>();
            if (e != null && !e.IsDead && Vector3.Distance(player.position, e.transform.position) <= aimRange) return e;
        }

        EnemyBase best = null;
        float bestAng = aimAngle;
        foreach (EnemyBase e in EnemyBase.All)
        {
            if (e == null || e.IsDead || e.data == null) continue;
            if (Vector3.Distance(player.position, e.transform.position) > aimRange) continue;
            float ang = Vector3.Angle(fwd, Chest(e) - origin);
            if (e == target) ang -= 2f;   // a little sticky
            if (ang < bestAng && InSight(e)) { bestAng = ang; best = e; }
        }
        return best;
    }

    void LateUpdate()
    {
        Camera cam = Camera.main;
        if (cam == null || !FindPlayer()) return;
        float dt = Time.unscaledDeltaTime;

        target = Pick(cam);

        // hidden while a menu frees the mouse
        bool gameplay = Cursor.lockState == CursorLockMode.Locked || Application.isEditor && Cursor.lockState == CursorLockMode.None && Cursor.visible == false;
        HealthManager hm = player.GetComponent<HealthManager>();
        if (hm != null && hm.IsDead) gameplay = false;   // no crosshair on the death screen
        float baseAlpha = gameplay ? 1f : 0f;

        float reach = PlayerCombat.Instance != null ? PlayerCombat.Instance.range : 2.6f;
        bool inReach = target != null && Vector3.Distance(player.position, target.transform.position) <= reach + 0.3f;
        hot = Mathf.MoveTowards(hot, target != null ? 1f : 0f, dt * 10f);
        kick = Mathf.MoveTowards(kick, 0f, dt * 5f);
        hitFlash = Mathf.MoveTowards(hitFlash, 0f, dt * 5f);
        killFlash = Mathf.MoveTowards(killFlash, 0f, dt * 2.5f);

        Color idle = new Color(1f, 1f, 1f, 0.85f);
        Color aimed = inReach ? new Color(1f, 0.3f, 0.22f, 1f) : new Color(CombatSkin.Gold.r, CombatSkin.Gold.g, CombatSkin.Gold.b, 1f);
        Color c = Color.Lerp(idle, aimed, hot);
        c.a *= baseAlpha;
        crossImg.color = c;
        float pulse = inReach ? 1f + 0.05f * Mathf.Sin(Time.unscaledTime * 14f) : 1f;
        cross.localScale = Vector3.one * (1f + 0.15f * hot + 0.4f * kick) * pulse;

        float m = Mathf.Max(hitFlash, killFlash);
        Color mc = killFlash > 0.01f ? new Color(1f, 0.25f, 0.2f) : Color.white;
        mc.a = m * baseAlpha;
        markerImg.color = mc;
        marker.localScale = Vector3.one * (killFlash > 0.01f ? 1.35f : 1f) * (1.25f - 0.25f * m);
    }

    // ------------------------------------------------------------------ swing: turn to where you aim

    void OnSwing(ToolType tool)
    {
        if (!FindPlayer()) return;
        kick = 1f;
        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 aimDir = cam.transform.forward; aimDir.y = 0f;
        if (aimDir.sqrMagnitude < 0.001f) return;
        aimDir.Normalize();
        lungeLeft = 0f;

        if (target != null && !target.IsDead)
        {
            Vector3 to = target.transform.position - player.position; to.y = 0f;
            float dist = to.magnitude;
            float reach = PlayerCombat.Instance != null ? PlayerCombat.Instance.range : 2.6f;
            if (dist > 0.05f && dist <= reach + maxLunge + 0.4f)
            {
                aimDir = to.normalized;
                float gap = dist - (reach - 0.6f);   // step in so the enemy ends up well inside reach
                if (gap > 0f)
                {
                    lungeStep = aimDir * Mathf.Min(gap, maxLunge);
                    lungeLeft = 0.14f;
                }
            }
        }

        turnTo = aimDir;
        turnLeft = 0.1f;
    }

    void Update()
    {
        if (player == null) return;
        float dt = Time.deltaTime;
        if (turnLeft > 0f)
        {
            turnLeft -= dt;
            Quaternion want = Quaternion.LookRotation(turnTo, Vector3.up);
            player.rotation = Quaternion.Slerp(player.rotation, want, turnLeft <= 0f ? 1f : 0.5f);
            if (tpc != null && targetRotField != null) targetRotField.SetValue(tpc, want.eulerAngles.y);   // the controller won't turn him back
        }
        if (lungeLeft > 0f && cc != null && cc.enabled)
        {
            float part = Mathf.Min(dt, lungeLeft) / 0.14f;
            lungeLeft -= dt;
            cc.Move(lungeStep * part);
        }
    }

    // ------------------------------------------------------------------ sprites

    /// <summary>Thin ring + centre dot, white with a soft dark edge so it reads on bright and dark ground.</summary>
    static Sprite MakeCrosshair()
    {
        const int s = 96;
        Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        float c = (s - 1) * 0.5f;
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float r = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;   // 0..1
                float ring = 1f - Mathf.Abs(r - 0.62f) / 0.06f;
                float dot = 1f - r / 0.1f;
                float white = Mathf.Clamp01(Mathf.Max(ring, dot));
                float edge = Mathf.Clamp01(Mathf.Max(1f - Mathf.Abs(r - 0.62f) / 0.11f, 1f - r / 0.16f));
                float a = Mathf.Max(white, edge * 0.55f);
                float v = a > 0f ? white / a : 0f;   // white core, dark rim
                tex.SetPixel(x, y, new Color(v, v, v, a));
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
    }

    /// <summary>Hit-marker: four short diagonal strokes around the centre.</summary>
    static Sprite MakeHitMarker()
    {
        const int s = 96;
        Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        float c = (s - 1) * 0.5f;
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float dx = (x - c) / c, dy = (y - c) / c;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float diag = Mathf.Abs(Mathf.Abs(dx) - Mathf.Abs(dy)) / 1.414f;   // distance to the diagonals
                float a = 0f;
                if (r > 0.42f && r < 0.92f) a = Mathf.Clamp01(1f - diag / 0.055f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a * (3f - 2f * a)));
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
    }
}
