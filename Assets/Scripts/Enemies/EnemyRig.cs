using UnityEngine;

/// <summary>
/// Procedural animation for models that come without clips (the Bestiary monsters are in T-pose).
/// Finds the bones by name and every frame poses them on top of the rest pose:
///   arms down and a bit forward, a hunched back, a walk / run cycle (legs and arms swing),
///   the head turns to the player, and the attack arm winds up and smashes down.
/// Works with any rig that uses common names (upperarm_l, thigh_r, spine_02, Head ...). Added by EnemyBase.
/// </summary>
[DefaultExecutionOrder(100)]
public class EnemyRig : MonoBehaviour
{
    [Tooltip("Body the directions are measured in (the enemy root).")]
    public Transform body;
    public float armDown = 72f;          // degrees from T-pose down to the sides
    public float armForward = 18f;
    public float elbowBend = 25f;
    public float hunch = 14f;
    public float stride = 32f;           // leg swing at full run
    public float armSwing = 26f;

    // filled from EnemyBase every frame
    [HideInInspector] public float run;          // 0 standing .. 1 running
    [HideInInspector] public float cycle;        // walk phase in radians
    [HideInInspector] public float attack;       // -1 wind-up .. 0 .. 1 strike
    [HideInInspector] public Vector3? lookAt;
    [HideInInspector] public float dead;         // 0 alive .. 1 limp

    Transform uaL, uaR, laL, laR, thL, thR, caL, caR, spine1, spine2, head;
    Quaternion[] rest;
    Transform[] all;

    void Start()
    {
        if (body == null) body = transform.parent != null ? transform.parent : transform;
        uaL = Find("upperarm_l", "UpperArm.L", "LeftArm", "upperarm.l");
        uaR = Find("upperarm_r", "UpperArm.R", "RightArm", "upperarm.r");
        laL = Find("lowerarm_l", "LowerArm.L", "LeftForeArm", "forearm.l");
        laR = Find("lowerarm_r", "LowerArm.R", "RightForeArm", "forearm.r");
        thL = Find("thigh_l", "UpperLeg.L", "LeftUpLeg", "thigh.l");
        thR = Find("thigh_r", "UpperLeg.R", "RightUpLeg", "thigh.r");
        caL = Find("calf_l", "LowerLeg.L", "LeftLeg", "shin.l");
        caR = Find("calf_r", "LowerLeg.R", "RightLeg", "shin.r");
        spine1 = Find("spine_02", "Spine1", "spine.002", "Chest");
        spine2 = Find("spine_03", "Spine2", "spine.003", "UpperChest");
        head = Find("Head", "head");
        all = new Transform[] { spine1, spine2, head, uaL, uaR, laL, laR, thL, thR, caL, caR };
        rest = new Quaternion[all.Length];
        for (int i = 0; i < all.Length; i++) if (all[i] != null) rest[i] = all[i].localRotation;
    }

    public bool HasRig { get { return uaL != null && uaR != null && thL != null && thR != null; } }

    Transform Find(params string[] names)
    {
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
            foreach (string n in names)
                if (string.Equals(t.name, n, System.StringComparison.OrdinalIgnoreCase)) return t;
        return null;
    }

    void LateUpdate()
    {
        if (rest == null || !HasRig) return;
        for (int i = 0; i < all.Length; i++) if (all[i] != null) all[i].localRotation = rest[i];

        Vector3 right = body.right, fwd = body.forward, up = body.up;
        float s = Mathf.Sin(cycle), c = Mathf.Cos(cycle);
        float wind = Mathf.Clamp01(-attack), strike = Mathf.Clamp01(attack);
        float limp = Mathf.Clamp01(dead);

        // back: hunched, more when running or winding up
        float bend = hunch + run * 10f - wind * 12f + strike * 22f;
        Rotate(spine1, right, bend * 0.5f);
        Rotate(spine2, right, bend * 0.5f);

        // arms: from T-pose down to the sides, then swing
        PointArm(uaL, laL, -1f, -s * armSwing * run, 0f);
        float attackArm = -wind * 150f + strike * 60f;          // right arm: raise high, then smash
        PointArm(uaR, laR, 1f, s * armSwing * run + attackArm, wind * 40f);

        // legs: walk cycle around the body's right axis
        float st = stride * Mathf.Lerp(0.35f, 1f, run) * Mathf.Min(1f, run * 3f);
        Rotate(thL, right, s * st);
        Rotate(thR, right, -s * st);
        Rotate(caL, right, -Mathf.Max(0f, -c) * st * 1.2f);    // knee bends when the leg swings forward
        Rotate(caR, right, -Mathf.Max(0f, c) * st * 1.2f);

        // head looks at the target
        if (head != null && lookAt.HasValue && limp < 0.5f)
        {
            Vector3 to = lookAt.Value - head.position;
            Vector3 flat = Vector3.ProjectOnPlane(to, up);
            float yaw = Vector3.SignedAngle(fwd, flat, up);
            yaw = Mathf.Clamp(yaw, -70f, 70f);
            Rotate(head, up, yaw * 0.8f);
        }

        // dead: arms and head go limp
        if (limp > 0f) { Rotate(spine2, right, -25f * limp); }
    }

    void Rotate(Transform t, Vector3 axis, float degrees)
    {
        if (t == null || Mathf.Abs(degrees) < 0.001f) return;
        t.rotation = Quaternion.AngleAxis(degrees, axis) * t.rotation;
    }

    // side: -1 left, +1 right. swing: forward/back degrees. raise: extra outward lift
    void PointArm(Transform upper, Transform lower, float side, float swing, float raise)
    {
        if (upper == null || lower == null) return;
        Vector3 right = body.right, fwd = body.forward, up = body.up;
        Vector3 cur = (lower.position - upper.position).normalized;
        // wanted direction: mostly down, a little forward, slightly out to the side
        float down = armDown - raise;
        Vector3 outward = right * side;
        Vector3 dir = Quaternion.AngleAxis(-swing - armForward, right) * (Quaternion.AngleAxis(down * side, fwd) * outward);
        upper.rotation = Quaternion.FromToRotation(cur, dir.normalized) * upper.rotation;
        // elbow
        Rotate(lower, right, -elbowBend - Mathf.Abs(swing) * 0.3f);
    }
}
