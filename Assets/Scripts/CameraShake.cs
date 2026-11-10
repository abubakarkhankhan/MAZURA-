using UnityEngine;

/// <summary>
/// Small camera shake for hits and falling trees. Runs after Cinemachine has placed the camera
/// (high execution order), so it never fights the camera system. Added automatically by ImpactFX.
/// </summary>
[DefaultExecutionOrder(10000)]
public class CameraShake : MonoBehaviour
{
    public static CameraShake Instance { get; private set; }

    float amplitude, duration, timeLeft;

    void Awake() { Instance = this; }

    /// <summary>Start a shake. A stronger shake replaces a weaker one that is still running.</summary>
    public void Shake(float amp, float time)
    {
        float current = timeLeft > 0f ? amplitude * (timeLeft / Mathf.Max(0.0001f, duration)) : 0f;
        if (amp < current) return;
        amplitude = amp;
        duration = Mathf.Max(0.05f, time);
        timeLeft = duration;
    }

    void LateUpdate()
    {
        if (timeLeft <= 0f) return;
        timeLeft -= Time.unscaledDeltaTime;
        float k = Mathf.Clamp01(timeLeft / duration);
        k *= k; // fades out quickly
        transform.position += Random.insideUnitSphere * amplitude * k;
        transform.rotation *= Quaternion.Euler(Random.insideUnitSphere * amplitude * 18f * k);
    }
}
