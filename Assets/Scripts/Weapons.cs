using UnityEngine;
using UnityEngine.InputSystem;

public enum WeaponKind { Sword, Spear, BattleAxe, WarHammer }

/// <summary>Numbers and look of one weapon.</summary>
public struct WeaponInfo
{
    public string name, desc, anim, hudIcon, workshopIcon, prefab;
    public CraftItem item;
    public float damage, range, arc, cooldown, hitDelay;
    public int maxTargets;
    public float slow, slowTime, shake;
    public float gripFromButt, scale;
}

/// <summary>
/// The weapons the player can own: Sword, Spear, Battle Axe and War Hammer.
/// All of them use the "Weapon" hotbar slot; Q switches between the ones you own (or pick one in the inventory).
/// Each has its own damage, reach, swing arc, speed, attack animation and special effect.
/// </summary>
public class Weapons : MonoBehaviour
{
    public static Weapons Instance { get; private set; }
    public static WeaponKind Current { get; private set; }

    public static readonly WeaponInfo[] All =
    {
        new WeaponInfo { name = "Sword", item = CraftItem.Sword, anim = "Attack", hudIcon = "HUD/in_item_sword", workshopIcon = "icon_sword",
            desc = "Balanced blade. Good reach, fast swings.",
            damage = 25f, range = 2.6f, arc = 130f, cooldown = 0.8f, hitDelay = 0.4f, maxTargets = 3 },
        new WeaponInfo { name = "Spear", item = CraftItem.Spear, anim = "Attack", hudIcon = "HUD/in_item_spear", workshopIcon = "icon_spear", prefab = "Weapons/Spear",
            desc = "Long reach and quick thrusts. Pierces up to 3 enemies in a line.",
            damage = 21f, range = 3.7f, arc = 55f, cooldown = 0.6f, hitDelay = 0.3f, maxTargets = 3, gripFromButt = 0.36f, scale = 1f },
        new WeaponInfo { name = "Battle Axe", item = CraftItem.BattleAxe, anim = "Chop", hudIcon = "HUD/in_item_doubleaxe", workshopIcon = "icon_doubleaxe", prefab = "Weapons/DoubleAxe",
            desc = "Huge, slow cleave. Hits everything around you and slows it down.",
            damage = 44f, range = 2.9f, arc = 220f, cooldown = 1.25f, hitDelay = 0.5f, maxTargets = 5, slow = 0.45f, slowTime = 2f, shake = 0.08f, gripFromButt = 0.2f, scale = 0.82f },
        new WeaponInfo { name = "War Hammer", item = CraftItem.WarHammer, anim = "Mine", hudIcon = "HUD/in_item_warhammer", workshopIcon = "icon_warhammer", prefab = "Weapons/WarHammer",
            desc = "Crushing overhead slam. Stuns enemies for a moment.",
            damage = 38f, range = 2.5f, arc = 110f, cooldown = 1.1f, hitDelay = 0.48f, maxTargets = 2, slow = 0.1f, slowTime = 1.4f, shake = 0.12f, gripFromButt = 0.16f, scale = 1.1f },
    };

    public static WeaponInfo Info { get { return All[(int)Current]; } }
    public static WeaponInfo InfoOf(WeaponKind k) { return All[(int)k]; }

    readonly GameObject[] models = new GameObject[4];
    float defCooldown = 0.8f, defHitDelay = 0.4f;
    bool defaultsRead;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (Object.FindAnyObjectByType<ToolController>() == null) return;
        if (Instance != null) return;
        Current = WeaponKind.Sword;
        new GameObject("Weapons").AddComponent<Weapons>();
    }

    void Awake() { Instance = this; }

    // ---------------------------------------------------------------- ownership
    public static bool Owns(WeaponKind k)
    {
        CraftingState cs = CraftingState.Instance;
        if (cs == null) return false;
        return cs.Count(InfoOf(k).item) > 0;
    }

    public static bool OwnsAny()
    {
        for (int i = 0; i < All.Length; i++) if (Owns((WeaponKind)i)) return true;
        return false;
    }

    public static WeaponKind? KindOf(CraftItem item)
    {
        for (int i = 0; i < All.Length; i++) if (All[i].item == item) return (WeaponKind)i;
        return null;
    }

    public static void Select(WeaponKind k)
    {
        if (!Owns(k)) return;
        Current = k;
        if (Instance != null) Instance.Refresh();
    }

    public static void Cycle()
    {
        for (int s = 1; s <= All.Length; s++)
        {
            WeaponKind k = (WeaponKind)(((int)Current + s) % All.Length);
            if (Owns(k)) { Select(k); return; }
        }
    }

    // ---------------------------------------------------------------- models
    void Start() { BuildModels(); }

    void BuildModels()
    {
        ToolController tc = ToolController.Instance;
        if (tc == null || tc.weaponModel == null) return;
        Transform hand = tc.weaponModel.transform.parent;
        models[0] = tc.weaponModel;
        for (int i = 1; i < All.Length; i++)
        {
            GameObject pf = Resources.Load<GameObject>(All[i].prefab);
            if (pf == null) continue;
            GameObject g = Instantiate(pf, hand);
            g.name = "Tool_" + All[i].name.Replace(" ", "");
            foreach (Collider c in g.GetComponentsInChildren<Collider>()) Destroy(c);
            Grip(g.transform, All[i]);
            g.SetActive(false);
            models[i] = g;
        }
    }

    /// <summary>Puts the handle in the fist: the weapon points along the hand's +Z (thumb side), the head's flat side towards the knuckles.</summary>
    static void Grip(Transform w, WeaponInfo info)
    {
        // weapon length is along its local Y; find which end is the head (the wider end)
        float yMin = float.MaxValue, yMax = float.MinValue;
        System.Collections.Generic.List<Vector3> pts = new System.Collections.Generic.List<Vector3>();
        foreach (MeshFilter mf in w.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh == null) continue;
            Matrix4x4 m = w.worldToLocalMatrix * mf.transform.localToWorldMatrix;
            foreach (Vector3 v in mf.sharedMesh.vertices) { Vector3 p = m.MultiplyPoint3x4(v); pts.Add(p); yMin = Mathf.Min(yMin, p.y); yMax = Mathf.Max(yMax, p.y); }
        }
        if (pts.Count == 0) return;
        float len = yMax - yMin, topW = 0f, botW = 0f;
        foreach (Vector3 p in pts)
        {
            float wdt = Mathf.Abs(p.x) + Mathf.Abs(p.z);
            if (p.y > yMax - len * 0.2f) topW = Mathf.Max(topW, wdt);
            if (p.y < yMin + len * 0.2f) botW = Mathf.Max(botW, wdt);
        }
        bool headUp = topW >= botW;
        Vector3 axis = headUp ? Vector3.up : Vector3.down;              // butt -> head, in weapon space
        float gripY = headUp ? yMin + len * info.gripFromButt : yMax - len * info.gripFromButt;

        Quaternion q1 = Quaternion.FromToRotation(axis, Vector3.forward);   // weapon axis -> hand +Z
        Vector3 face = q1 * Vector3.right;
        float twist = Vector3.SignedAngle(face, Vector3.up, Vector3.forward);
        Quaternion q = Quaternion.AngleAxis(twist, Vector3.forward) * q1;
        float s = info.scale <= 0f ? 1f : info.scale;
        w.localRotation = q;
        w.localScale = Vector3.one * s;
        Vector3 gripHand = new Vector3(0.045f, 0.08f, 0f);
        w.localPosition = gripHand - q * (new Vector3(0f, gripY, 0f) * s);
    }

    void Refresh()
    {
        ToolController tc = ToolController.Instance;
        if (tc == null) return;
        bool armed = tc.CurrentTool == ToolType.Weapon;
        for (int i = 0; i < models.Length; i++)
            if (models[i] != null) { bool on = armed && (int)Current == i; if (models[i].activeSelf != on) models[i].SetActive(on); }
    }

    void Update()
    {
        Keyboard kb = Keyboard.current;
        ToolController tc = ToolController.Instance;
        if (kb == null || tc == null) return;
        if (!Owns(Current) && OwnsAny()) Cycle();
        if (kb.qKey.wasPressedThisFrame && Cursor.lockState == CursorLockMode.Locked && OwnsAny())
        {
            if (tc.CurrentTool != ToolType.Weapon) { if (TorchController.IsHeld) TorchController.Toggle(); tc.Equip(ToolType.Weapon); }
            else Cycle();
            HudMessage.Show(Info.name + "   [Q] switch weapon");
        }
    }

    void LateUpdate()
    {
        ToolController tc = ToolController.Instance;
        if (tc == null) return;
        if (!defaultsRead) { defCooldown = tc.cooldown; defHitDelay = tc.hitDelay; defaultsRead = true; }
        if (tc.CurrentTool == ToolType.Weapon) { tc.cooldown = Info.cooldown; tc.hitDelay = Info.hitDelay; }
        else { tc.cooldown = defCooldown; tc.hitDelay = defHitDelay; }
        Refresh();
    }

    /// <summary>Animator trigger for the swing of a tool.</summary>
    public static string AnimFor(ToolType t) { return t == ToolType.Weapon ? Info.anim : "Attack"; }
}
