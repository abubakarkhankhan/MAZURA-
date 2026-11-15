using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// A crafting table in the maze. Walk up to it (inside the trigger) and press E to open the Workshop window (WorkshopUI).
/// Shows a floating "[E] Workbench" prompt while the player is close, and a warm lantern light
/// so it can be found at night. Needs a trigger collider on this object; the player is found by tag "Player".
/// </summary>
[RequireComponent(typeof(Collider))]
public class Workbench : MonoBehaviour
{
    [Tooltip("Height of the floating prompt above the table.")]
    public float promptHeight = 1.7f;

    bool playerNear;
    TextMeshPro prompt;
    float promptAlpha;

    /// <summary>The workbench the player is standing at (null if none).</summary>
    public static Workbench Current { get; private set; }

    void Awake()
    {
        GetComponent<Collider>().isTrigger = true;

        GameObject p = new GameObject("Prompt");
        p.transform.SetParent(transform, false);
        p.transform.localPosition = Vector3.up * promptHeight;
        prompt = p.AddComponent<TextMeshPro>();
        prompt.text = "<color=#E8C060>[E]</color>  Workbench";
        prompt.fontSize = 1.8f;
        prompt.alignment = TextAlignmentOptions.Center;
        prompt.fontStyle = FontStyles.Bold;
        prompt.color = new Color(0.95f, 0.89f, 0.74f, 0f);
        prompt.outlineWidth = 0.25f;
        prompt.outlineColor = new Color32(15, 8, 4, 255);
        prompt.rectTransform.sizeDelta = new Vector2(8f, 1.2f);
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player")) { playerNear = true; Current = this; }
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerNear = false;
        if (Current == this) Current = null;
        if (WorkshopUI.IsOpen) WorkshopUI.Close();
    }

    void Update()
    {
        Camera cm = Camera.main;
        bool tooClose = cm != null && Vector3.Distance(cm.transform.position, prompt.transform.position) < 1.6f;
        bool show = playerNear && !WorkshopUI.IsOpen && !tooClose;
        promptAlpha = Mathf.MoveTowards(promptAlpha, show ? 1f : 0f, Time.unscaledDeltaTime * 5f);
        Color c = prompt.color; c.a = promptAlpha; prompt.color = c;
        if (promptAlpha > 0.001f)
        {
            Camera cam = Camera.main;
            if (cam != null) prompt.transform.rotation = cam.transform.rotation;
            prompt.transform.localPosition = Vector3.up * (promptHeight + Mathf.Sin(Time.time * 2.2f) * 0.05f);
        }

        if (!playerNear) return;
        Keyboard kb = Keyboard.current;
        if (kb == null || !kb.eKey.wasPressedThisFrame) return;
        if (WorkshopUI.IsOpen) { WorkshopUI.Close(); return; }
        if (Cursor.lockState != CursorLockMode.Locked || InventoryUI.IsOpen || InventoryWindow.IsOpen) return;
        HealthManager hm = ToolController.Instance != null ? ToolController.Instance.GetComponent<HealthManager>() : null;
        if (hm != null && hm.IsDead) return;
        WorkshopUI.Open();
    }
}
