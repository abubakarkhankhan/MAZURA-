using UnityEngine;

/// <summary>
/// Curls the fingers of the right hand around the tool handle (axe, pickaxe, sword).
/// The rotation is applied after the animation every frame, so it works with any swing clip.
/// If no tool is in the hand the fingers relax a little.
/// Tuning: change the angles below (degrees per finger joint) or the curl axes.
/// </summary>
[DefaultExecutionOrder(500)]
public class HandGrip : MonoBehaviour
{
    [Range(0f, 1f)] public float grip = 1f;
    [Tooltip("How loose the hand is when it holds no tool.")]
    [Range(0f, 1f)] public float emptyHandGrip = 0.25f;
    public string wristName = "Wrist.R";

    [Header("Curl axis in the bone's local space")]
    public Vector3 fingerAxis = new Vector3(0f, 0f, 1f);
    public Vector3 thumbAxis = new Vector3(0f, 0f, 1f);

    [Header("Degrees for each joint (knuckle, middle, tip)")]
    public float[] indexAngles = { 70f, 80f, 40f };
    public float[] middleAngles = { 75f, 85f, 40f };
    public float[] ringAngles = { 78f, 85f, 40f };
    public float[] pinkyAngles = { 80f, 85f, 45f };
    public float[] thumbAngles = { 40f, 45f };

    string[] names = { "Index", "Middle", "Ring", "Pinky", "Thumb" };
    Transform[][] bones = new Transform[5][];
    Quaternion[][] rest = new Quaternion[5][];
    float[][] angles;
    ToolController tools;
    float current;

    void Start()
    {
        tools = GetComponentInParent<ToolController>();
        Transform wrist = FindDeep(transform, wristName);
        if (wrist == null) { Debug.LogWarning("HandGrip: wrist bone not found.", this); enabled = false; return; }

        angles = new float[][] { indexAngles, middleAngles, ringAngles, pinkyAngles, thumbAngles };
        for (int f = 0; f < 5; f++)
        {
            Transform root = wrist.Find(names[f] + "1.R");
            int count = f == 4 ? 2 : 3;
            bones[f] = new Transform[count];
            rest[f] = new Quaternion[count];
            Transform cur = root;
            for (int k = 0; k < count && cur != null; k++)
            {
                cur = cur.childCount > 0 ? cur.GetChild(0) : null; // skip the root bone, curl the joints after it
                bones[f][k] = cur;
                if (cur != null) rest[f][k] = cur.localRotation;
            }
        }
        current = grip;
    }

    void LateUpdate()
    {
        bool holding = tools == null
            || (tools.axeModel != null && tools.axeModel.activeSelf)
            || (tools.pickaxeModel != null && tools.pickaxeModel.activeSelf)
            || (tools.weaponModel != null && tools.weaponModel.activeSelf);
        float target = holding ? grip : emptyHandGrip * grip;
        current = Mathf.MoveTowards(current, target, Time.deltaTime * 6f);

        for (int f = 0; f < 5; f++)
        {
            Vector3 axis = f == 4 ? thumbAxis : fingerAxis;
            for (int k = 0; k < bones[f].Length; k++)
            {
                Transform b = bones[f][k];
                if (b == null) continue;
                b.localRotation = rest[f][k] * Quaternion.AngleAxis(angles[f][k] * current, axis);
            }
        }
    }

    static Transform FindDeep(Transform root, string n)
    {
        if (root.name == n) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform r = FindDeep(root.GetChild(i), n);
            if (r != null) return r;
        }
        return null;
    }
}
