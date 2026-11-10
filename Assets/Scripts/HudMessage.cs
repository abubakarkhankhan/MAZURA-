using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Lives on HUDCanvas.
///   HudMessage.Show("Need an axe")      -> short hint in the lower middle of the screen
///   HudMessage.Float("+3 Wood", color)  -> small text that rises and fades
/// Both do nothing if no HudMessage exists in the scene.
/// </summary>
public class HudMessage : MonoBehaviour
{
    static HudMessage instance;

    [Tooltip("Text used for hints such as 'Need an axe'. Its font is also used for floating texts.")]
    public TMP_Text hintText;
    public float hintSeconds = 1.5f;
    public float floatSeconds = 1.2f;
    public float floatRise = 90f;

    Coroutine hintRoutine;
    int activeFloats;

    void Awake()
    {
        instance = this;
        if (hintText != null) hintText.gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    public static void Show(string message)
    {
        if (instance != null) instance.ShowHint(message);
    }

    public static void Float(string message, Color color)
    {
        if (instance != null) instance.SpawnFloat(message, color);
    }

    void ShowHint(string message)
    {
        if (hintText == null) return;
        hintText.text = message;
        hintText.gameObject.SetActive(true);
        if (hintRoutine != null) StopCoroutine(hintRoutine);
        hintRoutine = StartCoroutine(HideHintLater());
    }

    IEnumerator HideHintLater()
    {
        yield return new WaitForSeconds(hintSeconds);
        hintText.gameObject.SetActive(false);
        hintRoutine = null;
    }

    void SpawnFloat(string message, Color color)
    {
        var go = new GameObject("FloatText", typeof(RectTransform));
        go.transform.SetParent(transform, false);

        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(500f, 60f);
        float startY = 110f + activeFloats * 50f; // stack several texts instead of overlapping
        rt.anchoredPosition = new Vector2(0f, startY);

        var txt = go.AddComponent<TextMeshProUGUI>();
        if (hintText != null) txt.font = hintText.font;
        txt.text = message;
        txt.fontSize = 40;
        txt.alignment = TextAlignmentOptions.Center;
        txt.color = color;
        txt.raycastTarget = false;

        StartCoroutine(AnimateFloat(rt, txt, startY));
    }

    IEnumerator AnimateFloat(RectTransform rt, TextMeshProUGUI txt, float startY)
    {
        activeFloats++;
        Color c = txt.color;
        float t = 0f;
        while (t < floatSeconds)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / floatSeconds);
            rt.anchoredPosition = new Vector2(0f, startY + floatRise * k);
            txt.color = new Color(c.r, c.g, c.b, 1f - k * k);
            yield return null;
        }
        activeFloats--;
        Destroy(rt.gameObject);
    }
}
