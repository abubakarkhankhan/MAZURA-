using System;
using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// Lives on the HUD panel text. Lists the item counts at the side of the screen
/// and refreshes whenever the Inventory changes.
/// </summary>
public class InventoryHUD : MonoBehaviour
{
    [Tooltip("Text that shows the list. Auto-found on this object if empty.")]
    public TMP_Text listText;

    Inventory inventory;

    void Awake()
    {
        if (listText == null) listText = GetComponent<TMP_Text>();
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
    }

    void Refresh()
    {
        if (listText == null) return;

        var sb = new StringBuilder();
        foreach (ItemType item in Enum.GetValues(typeof(ItemType)))
        {
            int count = inventory != null ? inventory.Get(item) : 0;
            string hex = ColorUtility.ToHtmlStringRGB(Inventory.ColorFor(item));
            sb.Append("<color=#").Append(hex).Append('>').Append(item).Append("</color>: ").Append(count).Append('\n');
        }
        listText.text = sb.ToString();
    }
}
