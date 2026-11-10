using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// One of the four tool slots at the bottom of the inventory (keys 1-4).
/// Shows which tool is equipped and equips the tool on click.
/// </summary>
public class ToolSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public ToolType tool;
    public Image highlight;
    public Color hoverColor = new Color(1f, 0.72f, 0.25f, 0.22f);
    public Color selectedColor = new Color(1f, 0.72f, 0.25f, 0.45f);

    bool hovered, selected;

    public void SetSelected(bool on)
    {
        selected = on;
        Apply();
    }

    void Apply()
    {
        if (highlight == null) return;
        highlight.color = selected ? selectedColor : (hovered ? hoverColor : Color.clear);
    }

    public void OnPointerEnter(PointerEventData e) { hovered = true; Apply(); }
    public void OnPointerExit(PointerEventData e) { hovered = false; Apply(); }

    public void OnPointerClick(PointerEventData e)
    {
        if (ToolController.Instance != null) ToolController.Instance.Equip(tool);
    }
}
