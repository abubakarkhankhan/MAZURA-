using UnityEngine;

/// <summary>
/// Put next to the Animator (BobModel). Animation clips from Starter Assets fire
/// OnFootstep / OnLand events on the Animator's GameObject; this forwards them
/// to the parent (ThirdPersonController) so nothing logs "has no receiver".
/// </summary>
public class AnimEventRelay : MonoBehaviour
{
    void OnFootstep(AnimationEvent e) { SendMessageUpwards("OnFootstep", e, SendMessageOptions.DontRequireReceiver); }
    void OnLand(AnimationEvent e) { SendMessageUpwards("OnLand", e, SendMessageOptions.DontRequireReceiver); }
}
