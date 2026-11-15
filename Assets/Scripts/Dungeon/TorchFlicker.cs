using UnityEngine;

/// <summary>Makes a torch light flicker a little.</summary>
public class TorchFlicker : MonoBehaviour
{
    public float baseIntensity = 2.6f;
    public float amount = 0.35f;
    Light l; float seed;

    void Awake() { l = GetComponent<Light>(); seed = Random.value * 100f; if (l != null) baseIntensity = l.intensity; }

    void Update()
    {
        if (l == null) return;
        float n = Mathf.PerlinNoise(seed, Time.time * 6f) - 0.5f;
        l.intensity = baseIntensity * (1f + n * amount * 2f);
    }
}
