using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>What sits in one hotbar slot.</summary>
public enum HotbarKind { Empty, Tool, Torch, Place, Use }

[Serializable]
public struct HotbarEntry
{
    public HotbarKind kind;
    public ToolType tool;      // kind == Tool
    public CraftItem item;     // kind == Place (Campfire / SpikeTrap / Workbench)

    public bool Same(HotbarEntry o)
    {
        if (kind != o.kind) return false;
        if (kind == HotbarKind.Tool) return tool == o.tool;
        if (kind == HotbarKind.Place || kind == HotbarKind.Use) return item == o.item;
        return true;
    }

    public string Name
    {
        get
        {
            switch (kind)
            {
                case HotbarKind.Tool: return tool == ToolType.Weapon ? Weapons.Info.name : tool.ToString();
                case HotbarKind.Torch: return "Torch";
                case HotbarKind.Place: return item == CraftItem.SpikeTrap ? "Spike Trap" : item.ToString();
                case HotbarKind.Use: return item == CraftItem.HealthPotion ? "Health Potion" : "Herb Salve";
                default: return "";
            }
        }
    }

    public Sprite Icon
    {
        get
        {
            switch (kind)
            {
                case HotbarKind.Tool:
                    return Resources.Load<Sprite>(tool == ToolType.Axe ? "WorkshopUI/icon_axe" : tool == ToolType.Pickaxe ? "WorkshopUI/icon_pickaxe" : "WorkshopUI/" + Weapons.Info.workshopIcon);
                case HotbarKind.Torch: return Resources.Load<Sprite>("WorkshopUI/icon_torch");
                case HotbarKind.Place:
                    if (item == CraftItem.Campfire) return Resources.Load<Sprite>("WorkshopUI/icon_campfire");
                    if (item == CraftItem.SpikeTrap) return Resources.Load<Sprite>("WorkshopUI/icon_spiketrap");
                    return Resources.Load<Sprite>("RecipeBookUI/Icon_Workbench");
                case HotbarKind.Use:
                    return Resources.Load<Sprite>(item == CraftItem.HealthPotion ? "RecipeBookUI/Icon_Health_Potion" : "RecipeBookUI/Icon_Herb_Salve");
                default: return null;
            }
        }
    }
}

/// <summary>
/// The 5-slot hotbar (keys 1-5). It only holds what the player really has:
/// tools you own, a torch, and placeables (campfire, spike trap, workbench) while you carry some.
/// New things go into the first free slot; drag slots in the Workshop / hand-craft window [C] to reorder.
/// Pressing the key of the tool you hold puts it away (empty hands).
/// </summary>
public class Hotbar : MonoBehaviour
{
    public const int Size = 5;
    public static Hotbar Instance { get; private set; }
    public static event Action OnChanged;

    readonly HotbarEntry[] slots = new HotbarEntry[Size];

    public HotbarEntry this[int i] { get { return slots[i]; } }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (Instance != null) return;
        new GameObject("Hotbar").AddComponent<Hotbar>();
    }

    void Awake() { Instance = this; }

    void Update()
    {
        Sync();

        Keyboard kb = Keyboard.current;
        if (kb == null) return;
        if (Cursor.lockState != CursorLockMode.Locked) return;
        if (WorkshopUI.IsOpen || ChestLootUI.IsOpen || RecipeBookUI.IsOpen || ConstructionSite.Busy) return;
        ToolController tc = ToolController.Instance;
        HealthManager hm = tc != null ? tc.GetComponent<HealthManager>() : null;
        if (hm != null && hm.IsDead) return;

        if (kb.digit1Key.wasPressedThisFrame) Activate(0);
        else if (kb.digit2Key.wasPressedThisFrame) Activate(1);
        else if (kb.digit3Key.wasPressedThisFrame) Activate(2);
        else if (kb.digit4Key.wasPressedThisFrame) Activate(3);
        else if (kb.digit5Key.wasPressedThisFrame) Activate(4);
    }

    // ---------------------------------------------------------------- contents

    bool Wanted(HotbarEntry e)
    {
        CraftingState cs = CraftingState.Instance;
        if (cs == null) return false;
        switch (e.kind)
        {
            case HotbarKind.Tool: return cs.HasTool(e.tool);
            case HotbarKind.Torch: return TorchController.Available;
            case HotbarKind.Place: return cs.Count(e.item) > 0;
            case HotbarKind.Use: return cs.Count(e.item) > 0;
            default: return false;
        }
    }

    void Sync()
    {
        bool changed = false;
        for (int i = 0; i < Size; i++)
            if (slots[i].kind != HotbarKind.Empty && !Wanted(slots[i])) { slots[i] = new HotbarEntry(); changed = true; }

        changed |= Ensure(new HotbarEntry { kind = HotbarKind.Tool, tool = ToolType.Axe });
        changed |= Ensure(new HotbarEntry { kind = HotbarKind.Tool, tool = ToolType.Pickaxe });
        changed |= Ensure(new HotbarEntry { kind = HotbarKind.Tool, tool = ToolType.Weapon });
        changed |= Ensure(new HotbarEntry { kind = HotbarKind.Torch });
        changed |= Ensure(new HotbarEntry { kind = HotbarKind.Place, item = CraftItem.Campfire });
        changed |= Ensure(new HotbarEntry { kind = HotbarKind.Place, item = CraftItem.SpikeTrap });
        changed |= Ensure(new HotbarEntry { kind = HotbarKind.Place, item = CraftItem.Workbench });
        if (changed && OnChanged != null) OnChanged();
    }

    /// <summary>Puts the entry into the first free slot if the player has it and it is not in the bar yet.</summary>
    // things that were put in the bar once; if the player takes them out we do not push them back in
    readonly System.Collections.Generic.HashSet<string> placedOnce = new System.Collections.Generic.HashSet<string>();

    static string Key(HotbarEntry e) { return e.kind + "_" + e.tool + "_" + e.item; }

    bool Ensure(HotbarEntry e)
    {
        string k = Key(e);
        if (!Wanted(e)) { placedOnce.Remove(k); return false; }
        for (int i = 0; i < Size; i++) if (slots[i].Same(e)) { placedOnce.Add(k); return false; }
        if (placedOnce.Contains(k)) return false;
        for (int i = 0; i < Size; i++)
            if (slots[i].kind == HotbarKind.Empty) { slots[i] = e; placedOnce.Add(k); return true; }
        return false;   // bar is full: still usable from the Workshop / by dragging it in
    }

    /// <summary>Puts an entry into slot i (from the backpack). If it is already in another slot the two swap.</summary>
    public void Assign(int i, HotbarEntry e)
    {
        if (i < 0 || i >= Size || e.kind == HotbarKind.Empty) return;
        for (int j = 0; j < Size; j++)
            if (slots[j].Same(e)) { Swap(j, i); return; }
        slots[i] = e;
        placedOnce.Add(Key(e));
        if (OnChanged != null) OnChanged();
    }

    /// <summary>Empties slot i (the item stays in the backpack).</summary>
    public void Clear(int i)
    {
        if (i < 0 || i >= Size) return;
        if (Selected == i) Activate(i);   // put it away first
        slots[i] = new HotbarEntry();
        if (OnChanged != null) OnChanged();
    }

    public void Swap(int a, int b)
    {
        if (a == b || a < 0 || b < 0 || a >= Size || b >= Size) return;
        HotbarEntry t = slots[a]; slots[a] = slots[b]; slots[b] = t;
        if (OnChanged != null) OnChanged();
    }

    /// <summary>Slot that is "in hand" right now (-1 = none).</summary>
    public int Selected
    {
        get
        {
            ToolController tc = ToolController.Instance;
            for (int i = 0; i < Size; i++)
            {
                HotbarEntry e = slots[i];
                if (e.kind == HotbarKind.Tool && tc != null && tc.CurrentTool == e.tool && !TorchController.IsHeld) return i;
                if (e.kind == HotbarKind.Torch && TorchController.IsHeld) return i;
                if (e.kind == HotbarKind.Place && PlacementController.PlacingItem == e.item && PlacementController.IsPlacing) return i;
            }
            return -1;
        }
    }

    /// <summary>Count shown on the slot ("" for tools).</summary>
    public string CountText(int i)
    {
        CraftingState cs = CraftingState.Instance;
        HotbarEntry e = slots[i];
        if (cs == null) return "";
        if (e.kind == HotbarKind.Torch) { int n = cs.Count(CraftItem.Torch); return n > 0 ? "x" + n : ""; }
        if (e.kind == HotbarKind.Place || e.kind == HotbarKind.Use) return "x" + cs.Count(e.item);
        if (e.kind == HotbarKind.Tool) { int t = cs.TierOf(e.tool); return t == 0 ? "" : (t == 1 ? "I" : "II"); }
        return "";
    }

    // ---------------------------------------------------------------- use

    public void Activate(int i)
    {
        if (i < 0 || i >= Size) return;
        HotbarEntry e = slots[i];
        ToolController tc = ToolController.Instance;
        if (tc == null) return;
        bool wasSelected = Selected == i;

        if (PlacementController.IsPlacing) PlacementController.Instance.StopPlacing(e.kind != HotbarKind.Place);

        switch (e.kind)
        {
            case HotbarKind.Empty:
                if (TorchController.IsHeld) TorchController.Toggle();
                tc.Equip(ToolType.None);
                break;
            case HotbarKind.Tool:
                if (wasSelected) tc.Equip(ToolType.None);        // press again = empty hands
                else tc.Equip(e.tool);
                break;
            case HotbarKind.Torch:
                TorchController.Toggle();
                break;
            case HotbarKind.Place:
                if (!wasSelected) PlacementController.Instance.BeginItem(e.item);
                break;
            case HotbarKind.Use:
                CraftingState cs = CraftingState.Instance;
                if (cs != null) { if (e.item == CraftItem.HealthPotion) cs.UsePotion(); else cs.UseSalve(); }
                break;
        }
    }
}
