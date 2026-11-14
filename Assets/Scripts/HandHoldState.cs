using UnityEngine;

/// <summary>
/// Drives the HandL / HandR finger layers of the player's Animator.
/// If an active non-finger object (axe, pickaxe, weapon...) is parented under a wrist,
/// that hand is 'holding' something -> IsHoldingL / IsHoldingR = true (fingers close).
/// Otherwise the fingers stay straight. Put on AdventurerModel (next to the Animator).
/// </summary>
public class HandHoldState : MonoBehaviour
{
    public Animator animator;
    [Tooltip("Wrist.L bone. Auto-found by name if empty.")]
    public Transform wristLeft;
    [Tooltip("Wrist.R bone. Auto-found by name if empty.")]
    public Transform wristRight;

    static readonly int HoldL = Animator.StringToHash("IsHoldingL");
    static readonly int HoldR = Animator.StringToHash("IsHoldingR");

    void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();
        if (wristLeft == null) wristLeft = FindByName(transform, "Wrist.L");
        if (wristRight == null) wristRight = FindByName(transform, "Wrist.R");
        if (animator != null && animator.isHuman)
        {
            if (wristLeft == null) wristLeft = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            if (wristRight == null) wristRight = animator.GetBoneTransform(HumanBodyBones.RightHand);
        }
    }

    void Update()
    {
        if (animator == null) return;
        animator.SetBool(HoldL, IsHolding(wristLeft));
        animator.SetBool(HoldR, IsHolding(wristRight));
    }

    static bool IsHolding(Transform wrist)
    {
        if (wrist == null) return false;
        for (int i = 0; i < wrist.childCount; i++)
        {
            Transform c = wrist.GetChild(i);
            if (!c.gameObject.activeSelf) continue;
            if (IsFingerBone(c.name)) continue;
            // Humanoid rigs (Bob) have palm/thumb bones with no mesh: only count real held objects.
            if (c.GetComponentInChildren<Renderer>(true) == null) continue;
            return true;
        }
        return false;
    }

    static bool IsFingerBone(string n)
    {
        return n.StartsWith("Index") || n.StartsWith("Middle") || n.StartsWith("Ring")
            || n.StartsWith("Pinky") || n.StartsWith("Thumb");
    }

    static Transform FindByName(Transform root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }
}
