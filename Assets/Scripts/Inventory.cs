using System;
using UnityEngine;

/// <summary>Everything the player can collect.</summary>
public enum ItemType { Wood, Stone, Herb, Ruby, Emerald, Sapphire, Amber }

/// <summary>
/// Singleton on a scene object (e.g. "GameManagers").
/// Keeps the item counts. The HUD and the future CraftingManager use it:
///   Inventory.Instance.Has(ItemType.Wood, 5)
///   Inventory.Instance.Remove(ItemType.Wood, 5)
/// </summary>
public class Inventory : MonoBehaviour
{
    public static Inventory Instance { get; private set; }

    /// <summary>Raised whenever any count changes.</summary>
    public event Action OnChanged;

    readonly int[] counts = new int[Enum.GetValues(typeof(ItemType)).Length];

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Current amount of an item.</summary>
    public int Get(ItemType item)
    {
        return counts[(int)item];
    }

    /// <summary>True if the player owns at least 'amount' of the item.</summary>
    public bool Has(ItemType item, int amount)
    {
        return counts[(int)item] >= amount;
    }

    /// <summary>Adds items and shows a floating "+3 Wood" text.</summary>
    public void Add(ItemType item, int amount)
    {
        if (amount <= 0) return;
        counts[(int)item] += amount;
        OnChanged?.Invoke();
        HudMessage.Float("+" + amount + " " + item, ColorFor(item));
    }

    /// <summary>Removes items. Returns false (and changes nothing) if there are not enough.</summary>
    public bool Remove(ItemType item, int amount)
    {
        if (amount <= 0) return true;
        if (!Has(item, amount)) return false;
        counts[(int)item] -= amount;
        OnChanged?.Invoke();
        return true;
    }

    /// <summary>Colour used for the item in the HUD and in floating texts.</summary>
    public static Color ColorFor(ItemType item)
    {
        switch (item)
        {
            case ItemType.Wood: return new Color(0.85f, 0.62f, 0.35f);
            case ItemType.Stone: return new Color(0.75f, 0.75f, 0.78f);
            case ItemType.Herb: return new Color(0.5f, 0.9f, 0.4f);
            case ItemType.Ruby: return new Color(1f, 0.3f, 0.35f);
            case ItemType.Emerald: return new Color(0.3f, 1f, 0.5f);
            case ItemType.Sapphire: return new Color(0.4f, 0.6f, 1f);
            case ItemType.Amber: return new Color(1f, 0.65f, 0.2f);
            default: return Color.white;
        }
    }
}
