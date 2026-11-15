using TMPro;
using UnityEngine;

/// <summary>
/// A floating "" [E] Do something "" label above an object, like the chest prompt.
/// Call Tick(show) every frame; it fades in/out and always faces the camera.
/// </summary>
public class WorldPrompt
{
    readonly TextMeshPro label;
    readonly Transform anchor;
    readonly float height;
    float alpha;

    public WorldPrompt(Transform anchor, float height, string text)
    {
        this.anchor = anchor; this.height = height;
        GameObject p = new GameObject("Prompt");
        p.transform.SetParent(anchor, false);
        p.transform.localPosition = Vector3.up * height;
        label = p.AddComponent<TextMeshPro>();
        label.fontSize = 1.6f;
        label.alignment = TextAlignmentOptions.Center;
        label.fontStyle = FontStyles.Bold;
        label.color = new Color(0.95f, 0.89f, 0.74f, 0f);
        label.outlineWidth = 0.25f;
        label.outlineColor = new Color32(15, 8, 4, 255);
        label.rectTransform.sizeDelta = new Vector2(10f, 1.4f);
        label.enableWordWrapping = false;
        Text = text;
    }

    public string Text { set { label.text = value; } }

    public void Tick(bool show)
    {
        alpha = Mathf.MoveTowards(alpha, show ? 1f : 0f, Time.unscaledDeltaTime * 5f);
        Color c = label.color; c.a = alpha; label.color = c;
        Camera cam = Camera.main;
        if (alpha > 0.001f && cam != null)
        {
            label.transform.rotation = cam.transform.rotation;
            label.transform.localPosition = Vector3.up * (height + Mathf.Sin(Time.time * 2.2f) * 0.05f);
        }
    }

    public static string Key(string action) { return "<color=#E8C060>[E]</color>  " + action; }

    /// <summary>Is the player standing within range of this point (and able to act)?</summary>
    public static bool PlayerNear(Vector3 p, float range)
    {
        ToolController tc = ToolController.Instance;
        if (tc == null) return false;
        Vector3 d = tc.transform.position - p; d.y = 0f;
        return d.sqrMagnitude < range * range;
    }

    public static bool CanAct()
    {
        return Cursor.lockState == CursorLockMode.Locked && !WorkshopUI.IsOpen && !InventoryWindow.IsOpen
            && !ChestLootUI.IsOpen && !RecipeBookUI.IsOpen && !StoryScreen.IsOpen;
    }
}
