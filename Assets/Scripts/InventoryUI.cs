using System;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// The inventory window. Lives on "InventoryCanvas".
/// I or Tab opens / closes it (Esc closes). While it is open the cursor is free,
/// the camera look is paused and ToolController ignores swings (it already ignores
/// input when the cursor is not locked).
/// Shows every owned item in the grid, details on the right and the tool bar at the bottom.
/// </summary>
public class InventoryUI : MonoBehaviour
{
    public static bool IsOpen { get; private set; }

    [Header("Panel")]
    public CanvasGroup rootGroup;
    public RectTransform panel;
    [Tooltip("Seconds for the fade / scale animation.")]
    public float animTime = 0.15f;
    [Range(0.8f, 1f)] public float closedScale = 0.92f;

    [Header("Grid and detail panel")]
    public InventorySlotUI[] slots;
    public Image detailIcon;
    public TMP_Text detailName;
    public TMP_Text detailCount;
    public TMP_Text detailDescription;
    [Tooltip("One icon per ItemType, in enum order.")]
    public Sprite[] itemIcons;

    [Header("Tool bar (keys 1-4)")]
    public ToolSlotUI[] toolSlots;

    [Header("Slot colours")]
    public Color hoverColor = new Color(1f, 0.72f, 0.25f, 0.22f);
    public Color selectedColor = new Color(1f, 0.72f, 0.25f, 0.45f);

    Inventory inventory;
    bool open;
    float t;                 // 0 = closed, 1 = open
    int selected = -1;       // ItemType index
    int hovered = -1;

    // camera look is paused through StarterAssetsInputs (found by name, so no hard dependency)
    MonoBehaviour lookInputs;
    FieldInfo lookEnabledField, lookVectorField;

    void Awake()
    {
        IsOpen = false;
        if (slots != null)
            foreach (var s in slots) if (s != null) s.owner = this;
        ApplyVisual();
    }

    void Start()
    {
        inventory = Inventory.Instance;
        if (inventory != null) inventory.OnChanged += Refresh;
        Refresh();
    }

    void OnDestroy()
    {
        if (inventory != null) inventory.OnChanged -= Refresh;
        if (open) { IsOpen = false; }
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.iKey.wasPressedThisFrame || kb.tabKey.wasPressedThisFrame)
            {
                if (open) Close();
                else if (Cursor.lockState == CursorLockMode.Locked) Open();
            }
            else if (open && kb.escapeKey.wasPressedThisFrame)
            {
                Close();
            }
        }

        float target = open ? 1f : 0f;
        if (!Mathf.Approximately(t, target))
        {
            t = Mathf.MoveTowards(t, target, Time.unscaledDeltaTime / Mathf.Max(0.01f, animTime));
            ApplyVisual();
        }

        if (t > 0.001f) UpdateToolSlots();
    }

    public void Open()
    {
        if (open) return;
        open = true;
        IsOpen = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SetLook(false);
        Refresh();
    }

    public void Close()
    {
        if (!open) return;
        open = false;
        IsOpen = false;
        hovered = -1;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        SetLook(true);
        UpdateHighlights();
        UpdateDetail();
    }

    void ApplyVisual()
    {
        float k = t * t * (3f - 2f * t);
        if (rootGroup != null)
        {
            rootGroup.alpha = k;
            rootGroup.blocksRaycasts = open;
            rootGroup.interactable = open;
        }
        if (panel != null) panel.localScale = Vector3.one * Mathf.Lerp(closedScale, 1f, k);
    }

    // ---------- items ----------

    void Refresh()
    {
        int s = 0;
        foreach (ItemType item in Enum.GetValues(typeof(ItemType)))
        {
            int count = inventory != null ? inventory.Get(item) : 0;
            if (count <= 0) continue;
            if (slots == null || s >= slots.Length) break;
            slots[s].Set((int)item, IconFor(item), count);
            s++;
        }
        if (slots != null)
            for (; s < slots.Length; s++) slots[s].Clear();

        if (selected >= 0 && (inventory == null || inventory.Get((ItemType)selected) <= 0)) selected = -1;
        if (selected < 0 && slots != null && slots.Length > 0 && slots[0].itemIndex >= 0) selected = slots[0].itemIndex;

        UpdateHighlights();
        UpdateDetail();
    }

    Sprite IconFor(ItemType item)
    {
        int i = (int)item;
        return itemIcons != null && i < itemIcons.Length ? itemIcons[i] : null;
    }

    public void SlotHover(InventorySlotUI slot, bool enter)
    {
        hovered = enter ? slot.itemIndex : -1;
        UpdateHighlights();
        UpdateDetail();
    }

    public void SlotClick(InventorySlotUI slot)
    {
        if (slot.itemIndex < 0) return;
        selected = slot.itemIndex;
        UpdateHighlights();
        UpdateDetail();
    }

    void UpdateHighlights()
    {
        if (slots == null) return;
        foreach (var s in slots)
        {
            if (s == null) continue;
            Color c = Color.clear;
            if (s.itemIndex >= 0)
            {
                if (s.itemIndex == selected) c = selectedColor;
                else if (s.itemIndex == hovered) c = hoverColor;
            }
            s.SetHighlight(c);
        }
    }

    void UpdateDetail()
    {
        int show = hovered >= 0 ? hovered : selected;
        bool has = show >= 0 && inventory != null && inventory.Get((ItemType)show) > 0;

        if (detailIcon != null)
        {
            Sprite sp = has ? IconFor((ItemType)show) : null;
            detailIcon.sprite = sp;
            detailIcon.enabled = sp != null;
        }
        if (detailName != null) detailName.text = has ? ((ItemType)show).ToString() : "";
        if (detailCount != null) detailCount.text = has ? "Owned: " + inventory.Get((ItemType)show) : "";
        if (detailDescription != null)
            detailDescription.text = has ? Describe((ItemType)show) : "Collect resources from trees and stones. Click an item to see its details.";
    }

    void UpdateToolSlots()
    {
        if (toolSlots == null) return;
        ToolController tc = ToolController.Instance;
        foreach (var ts in toolSlots)
            if (ts != null) ts.SetSelected(tc != null && tc.CurrentTool == ts.tool);
    }

    static string Describe(ItemType item)
    {
        switch (item)
        {
            case ItemType.Wood: return "Sturdy timber cut from the dark forest. Needed for building and crafting.";
            case ItemType.Stone: return "Rough stone chipped from boulders. Used for tools and walls.";
            case ItemType.Herb: return "A pale healing herb that grows in damp corners of the maze.";
            case ItemType.Ruby: return "A deep red gem that glows faintly in the dark. Rare and valuable.";
            case ItemType.Emerald: return "A green gem with a cold inner light. Prized by traders.";
            case ItemType.Sapphire: return "A blue gem that seems to hold a drop of the night sky.";
            case ItemType.Amber: return "Warm golden resin, hard as stone. Old trees leave it behind.";
            default: return "";
        }
    }

    // ---------- camera look ----------

    void SetLook(bool on)
    {
        if (lookInputs == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                foreach (var m in player.GetComponents<MonoBehaviour>())
                {
                    if (m != null && m.GetType().Name == "StarterAssetsInputs")
                    {
                        lookInputs = m;
                        lookEnabledField = m.GetType().GetField("cursorInputForLook");
                        lookVectorField = m.GetType().GetField("look");
                        break;
                    }
                }
            }
        }
        if (lookInputs == null) return;
        if (lookEnabledField != null) lookEnabledField.SetValue(lookInputs, on);
        if (lookVectorField != null) lookVectorField.SetValue(lookInputs, Vector2.zero);   // no stale camera spin
    }
}
