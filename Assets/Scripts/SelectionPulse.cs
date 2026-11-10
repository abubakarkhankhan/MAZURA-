using UnityEngine;
using UnityEngine.UI;

/// <summary>Makes a selection frame breathe: a slow glow pulse plus a tiny pop when it appears.</summary>
public class SelectionPulse : MonoBehaviour
{
    public float speed = 2.6f;
    public float scaleAmount = 0.035f;
    [Range(0f, 1f)] public float minAlpha = 0.72f;

    Image img;
    float baseAlpha = -1f, written = -1f, popT;

    void OnEnable() { popT = 0f; }

    void Update()
    {
        if (img == null) img = GetComponent<Image>();
        float t = Time.unscaledTime * speed;
        popT = Mathf.Min(1f, popT + Time.unscaledDeltaTime * 6f);
        float pop = 1f + 0.12f * (1f - popT) * (1f - popT);              // starts a little big, settles
        float breathe = 1f + scaleAmount * (0.5f + 0.5f * Mathf.Sin(t));
        transform.localScale = Vector3.one * pop * breathe;
        if (img != null)
        {
            Color c = img.color;
            // another script may change the colour (hover / selected); take that as the new base
            if (Mathf.Abs(c.a - written) > 0.001f) baseAlpha = c.a;
            if (baseAlpha > 0.01f)
            {
                c.a = baseAlpha * Mathf.Lerp(minAlpha, 1f, 0.5f + 0.5f * Mathf.Sin(t));
                img.color = c;
                written = c.a;
            }
            else { written = c.a; transform.localScale = Vector3.one; }
        }
    }
}
