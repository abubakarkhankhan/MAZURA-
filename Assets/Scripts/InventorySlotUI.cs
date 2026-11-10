using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>One cell of the inventory grid. Filled by InventoryUI.</summary>
public class InventorySlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public Image icon;
    public TMP_Text countText;
    [Tooltip("Glow shown on hover / selection.")]
    public Image highlight;
    [Tooltip("Soft glow behind the item, tinted with the item's colour.")]
    public Image backGlow;
    [Range(0f, 1f)] public float backGlowAlpha = 0.28f;

    bool hover;
    float pop;

    [HideInInspector] public InventoryUI owner;

    /// <summary>ItemType index shown in this slot, -1 = empty.</summary>
    [HideInInspector] public int itemIndex = -1;

    public void Set(int index, Sprite sprite, int count)
    {
        itemIndex = index;
        if (icon != null)
        {
            icon.sprite = sprite;
            icon.enabled = sprite != null;
        }
        if (countText != null) countText.text = count > 1 ? count.ToString() : (count == 1 ? "1" : "");
        if (backGlow != null)
        {
            Color c = Inventory.ColorFor((ItemType)index);
            c.a = backGlowAlpha;
            backGlow.color = c;
            backGlow.enabled = sprite != null;
        }
    }

    public void Clear()
    {
        itemIndex = -1;
        if (icon != null) icon.enabled = false;
        if (countText != null) countText.text = "";
        if (backGlow != null) backGlow.enabled = false;
    }

    void Update()
    {
        // the item grows a little while the mouse is over it
        pop = Mathf.MoveTowards(pop, hover && itemIndex >= 0 ? 1f : 0f, Time.unscaledDeltaTime * 8f);
        if (icon != null) icon.rectTransform.localScale = Vector3.one * (1f + 0.1f * pop);
    }

    public void SetHighlight(Color c)
    {
        if (highlight != null) highlight.color = c;
    }

    public void OnPointerEnter(PointerEventData e) { hover = true; if (owner != null) owner.SlotHover(this, true); }
    public void OnPointerExit(PointerEventData e) { hover = false; if (owner != null) owner.SlotHover(this, false); }
    public void OnPointerClick(PointerEventData e) { if (owner != null) owner.SlotClick(this); }
}
