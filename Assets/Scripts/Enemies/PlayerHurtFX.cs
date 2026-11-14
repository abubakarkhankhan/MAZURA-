using System;
using UnityEngine;

/// <summary>Called when an enemy hits the player. Task 4 adds the red screen flash and sound; for now a camera shake.</summary>
public static class PlayerHurtFX
{
    public static event Action<Vector3, float> OnPlayerHit;

    public static void Notify(Vector3 from, float amount)
    {
        CameraShake cs = CameraShake.Instance;
        if (cs != null) cs.Shake(Mathf.Clamp(amount * 0.006f, 0.04f, 0.14f), 0.25f);
        OnPlayerHit?.Invoke(from, amount);
    }
}
