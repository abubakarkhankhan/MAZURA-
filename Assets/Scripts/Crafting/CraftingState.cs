using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>What a recipe makes.</summary>
public enum CraftItem { HerbSalve, HealthPotion, Torch, Campfire, SpikeTrap, Shield, Workbench, Sword, Spear, BattleAxe, WarHammer }
public enum CraftCategory { Tools, Survival, Build }

/// <summary>One ingredient line of a recipe.</summary>
[Serializable]
public struct Ingredient
{
    public ItemType item;
    public int amount;
    public Ingredient(ItemType item, int amount) { this.item = item; this.amount = amount; }
}

/// <summary>One recipe. Either upgrades a tool to 'tier' or adds one CraftItem.</summary>
public class Recipe
{
    public string id, name, description, effect;
    public CraftCategory category;
    public Ingredient[] cost;
    public bool isToolUpgrade;
    /// <summary>Can be made anywhere from the hand-crafting menu [C] (otherwise only at a Workbench).</summary>
    public bool byHand;
    public ToolType tool;
    public int tier;                 // tool upgrades: the tier you get (1 or 2)
    public CraftItem item;           // everything else
    public float craftTime = 1.2f;
}

/// <summary>
/// The crafting "save": tool tiers and crafted items. Created automatically when the game starts.
/// All recipes are in BuildRecipes() below, so balancing is one file.
///   Tool tiers: 0 Basic, 1 Reinforced, 2 Gemforged.
///     Axe / Pickaxe: a hit counts as 1 / 2 / 3 hits on trees and rocks.
///     Sword: damage x1 / x1.4 / x2.
///   H = use a Herb Salve (heals 30 over 4 s), J = use a Health Potion (heals 60 at once).
/// </summary>
public class CraftingState : MonoBehaviour
{
    public static CraftingState Instance { get; private set; }
    public static event Action OnChanged;

    public static readonly string[] TierNames = { "Basic", "Reinforced", "Gemforged" };

    readonly int[] toolTier = new int[3];   // Axe, Pickaxe, Weapon
    readonly bool[] hasTool = new bool[3];  // the player starts with no tools (axe + pickaxe are in the starter chest)
    readonly Dictionary<CraftItem, int> items = new Dictionary<CraftItem, int>();
    public List<Recipe> Recipes { get; private set; }

    float healLeft, healRate;
    ParticleSystem healFx;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (Instance != null) return;
        new GameObject("CraftingState").AddComponent<CraftingState>();
    }

    void Awake()
    {
        Instance = this;
        Recipes = BuildRecipes();
    }

    // ------------------------------------------------------------------ recipes

    static List<Recipe> BuildRecipes()
    {
        List<Recipe> r = new List<Recipe>();
        // ---- tools
        r.Add(Item("sword0", "Sword", CraftItem.Sword, CraftCategory.Tools, "A plain iron blade on a leather grip. Better than bare hands.", "Goes into your hotbar",
            new Ingredient(ItemType.Wood, 3), new Ingredient(ItemType.Stone, 6)));
        r.Add(Tool("axe1", "Reinforced Axe", ToolType.Axe, 1, "Stone-weighted head on a hardwood haft.", "Trees fall in half the swings",
            new Ingredient(ItemType.Wood, 6), new Ingredient(ItemType.Stone, 6)));
        r.Add(Tool("axe2", "Gemforged Axe", ToolType.Axe, 2, "Emerald-edged and bound in amber. It bites like it is hungry.", "Trees fall in a third of the swings",
            new Ingredient(ItemType.Emerald, 3), new Ingredient(ItemType.Amber, 4), new Ingredient(ItemType.Stone, 6)));
        r.Add(Tool("pick1", "Reinforced Pickaxe", ToolType.Pickaxe, 1, "A heavier, sharper point for stubborn rock.", "Rocks break in half the swings",
            new Ingredient(ItemType.Wood, 6), new Ingredient(ItemType.Stone, 8)));
        r.Add(Tool("pick2", "Gemforged Pickaxe", ToolType.Pickaxe, 2, "Sapphire tip, amber grip. Stone gives way to it.", "Rocks break in a third of the swings",
            new Ingredient(ItemType.Sapphire, 3), new Ingredient(ItemType.Amber, 4), new Ingredient(ItemType.Stone, 6)));
        r.Add(Tool("sword1", "Reinforced Sword", ToolType.Weapon, 1, "Re-forged on the anvil, edge ground true.", "+40% sword damage",
            new Ingredient(ItemType.Wood, 4), new Ingredient(ItemType.Stone, 10), new Ingredient(ItemType.Amber, 1)));
        r.Add(Tool("sword2", "Gemforged Sword", ToolType.Weapon, 2, "A ruby burns in the crossguard. Night things fear it.", "Double sword damage",
            new Ingredient(ItemType.Ruby, 3), new Ingredient(ItemType.Sapphire, 2), new Ingredient(ItemType.Amber, 4)));
        r.Add(Item("shield", "Wooden Shield", CraftItem.Shield, CraftCategory.Tools, "Oak planks bound with an iron rim. Heavy, but it holds.", "Hold Right Mouse to block 85% of a hit",
            new Ingredient(ItemType.Wood, 8), new Ingredient(ItemType.Stone, 4), new Ingredient(ItemType.Amber, 1)));
        r.Add(Item("spear", "Spear", CraftItem.Spear, CraftCategory.Tools, "A long ash shaft with a sharpened stone point.", "Long reach, quick thrusts, pierces a line of enemies   [Q] switch",
            new Ingredient(ItemType.Wood, 7), new Ingredient(ItemType.Stone, 3)));
        r.Add(Item("battleaxe", "Battle Axe", CraftItem.BattleAxe, CraftCategory.Tools, "A crescent blade on a long haft. Slow, brutal, wide.", "Huge cleave around you, slows what it hits   [Q] switch",
            new Ingredient(ItemType.Wood, 6), new Ingredient(ItemType.Stone, 10), new Ingredient(ItemType.Ruby, 1)));
        r.Add(Item("warhammer", "War Hammer", CraftItem.WarHammer, CraftCategory.Tools, "A block of stone bound to a root-wrapped handle.", "Crushing slam that stuns enemies   [Q] switch",
            new Ingredient(ItemType.Wood, 5), new Ingredient(ItemType.Stone, 12), new Ingredient(ItemType.Sapphire, 1)));
        // ---- survival
        r.Add(Item("salve", "Herb Salve", CraftItem.HerbSalve, CraftCategory.Survival, "Crushed maze herbs. Smells awful, works well.", "Heals 30 HP over 4 s   [H]",
            new Ingredient(ItemType.Herb, 2)));
        r.Add(Item("potion", "Health Potion", CraftItem.HealthPotion, CraftCategory.Survival, "Herbs steeped with a powdered ruby. Glows red.", "Heals 60 HP at once   [J]",
            new Ingredient(ItemType.Herb, 3), new Ingredient(ItemType.Ruby, 1)));
        r.Add(Item("torch", "Torch", CraftItem.Torch, CraftCategory.Survival, "Pitch-soaked wood and a lump of amber that burns slow.", "Light in your hand for the night",
            new Ingredient(ItemType.Wood, 2), new Ingredient(ItemType.Amber, 1)));
        // ---- build
        r.Add(Item("campfire", "Campfire", CraftItem.Campfire, CraftCategory.Build, "A ring of stones and a stack of dry wood.", "A safe, warm place at night",
            new Ingredient(ItemType.Wood, 8), new Ingredient(ItemType.Stone, 4)));
        r.Add(Item("trap", "Spike Trap", CraftItem.SpikeTrap, CraftCategory.Build, "Sharpened stakes set in stone. Hidden in the dark.", "Hurts and slows what steps on it",
            new Ingredient(ItemType.Wood, 4), new Ingredient(ItemType.Stone, 3)));
        r.Add(Item("workbench", "Workbench", CraftItem.Workbench, CraftCategory.Build, "A sturdy table with a vice and an anvil.", "Place it to unlock the Workshop",
            new Ingredient(ItemType.Wood, 10), new Ingredient(ItemType.Stone, 4)));

        foreach (Recipe x in r)
        {
            x.byHand = x.id == "workbench" || x.id == "torch" || x.id == "campfire";
            x.craftTime = x.isToolUpgrade || x.category == CraftCategory.Tools ? 3.2f : (x.id == "workbench" ? 3.5f : 2.4f);
        }
        return r;
    }

    static Recipe Tool(string id, string name, ToolType tool, int tier, string desc, string effect, params Ingredient[] cost)
    {
        return new Recipe { id = id, name = name, description = desc, effect = effect, category = CraftCategory.Tools, cost = cost, isToolUpgrade = true, tool = tool, tier = tier, craftTime = 1.6f };
    }

    static Recipe Item(string id, string name, CraftItem item, CraftCategory cat, string desc, string effect, params Ingredient[] cost)
    {
        return new Recipe { id = id, name = name, description = desc, effect = effect, category = cat, cost = cost, item = item, craftTime = 1.1f };
    }

    // ------------------------------------------------------------------ queries

    public int TierOf(ToolType tool)
    {
        int i = (int)tool;
        return i >= 0 && i < 3 ? toolTier[i] : 0;
    }

    /// <summary>Does the player own this tool at all (found or crafted)?</summary>
    public bool HasTool(ToolType tool)
    {
        int i = (int)tool;
        return i >= 0 && i < 3 && hasTool[i];
    }

    /// <summary>Gives the player a tool (starter chest, crafting the sword).</summary>
    public void GiveTool(ToolType tool)
    {
        int i = (int)tool;
        if (i < 0 || i >= 3 || hasTool[i]) return;
        hasTool[i] = true;
        if (tool == ToolType.Weapon && Count(CraftItem.Sword) == 0) items[CraftItem.Sword] = 1;
        ApplyToolLooks();
        OnChanged?.Invoke();
    }

    public int Count(CraftItem item)
    {
        int n;
        return items.TryGetValue(item, out n) ? n : 0;
    }

    /// <summary>A hit with this tool counts as this many hits on trees / rocks.</summary>
    public static int ToolPower(ToolType tool) { return Instance != null ? 1 + Instance.TierOf(tool) : 1; }

    /// <summary>Damage multiplier for the sword tier.</summary>
    public static float DamageMultiplier(ToolType tool)
    {
        if (Instance == null || tool != ToolType.Weapon) return 1f;
        int t = Instance.TierOf(tool);
        return t == 2 ? 2f : (t == 1 ? 1.4f : 1f);
    }

    /// <summary>Why the recipe can't be crafted right now ("" = it can).</summary>
    public string Blocker(Recipe r)
    {
        if (r.isToolUpgrade)
        {
            if (!HasTool(r.tool)) return "You need a " + (r.tool == ToolType.Weapon ? "Sword" : r.tool.ToString()) + " first";
            int cur = TierOf(r.tool);
            if (cur >= r.tier) return "Already owned";
            if (cur < r.tier - 1) return "Needs " + TierNames[r.tier - 1] + " first";
        }
        if (!r.isToolUpgrade && r.item == CraftItem.Shield && Count(CraftItem.Shield) > 0) return "Already owned";
        if (!r.isToolUpgrade && r.item == CraftItem.Sword && Count(CraftItem.Sword) > 0) return "Already owned";
        if (!r.isToolUpgrade && Weapons.KindOf(r.item).HasValue && r.item != CraftItem.Sword && Count(r.item) > 0) return "Already owned";
        Inventory inv = Inventory.Instance;
        if (inv == null) return "No inventory";
        foreach (Ingredient g in r.cost) if (!inv.Has(g.item, g.amount)) return "Not enough materials";
        return "";
    }

    /// <summary>Takes the materials and gives the result. Returns false if it can't.</summary>
    public bool Craft(Recipe r)
    {
        if (Blocker(r) != "") return false;
        Inventory inv = Inventory.Instance;
        foreach (Ingredient g in r.cost) inv.Remove(g.item, g.amount);
        if (r.isToolUpgrade) { toolTier[(int)r.tool] = r.tier; ApplyToolLooks(); }
        else if (Weapons.KindOf(r.item).HasValue) { hasTool[(int)ToolType.Weapon] = true; items[r.item] = 1; ApplyToolLooks(); Weapons.Select(Weapons.KindOf(r.item).Value); }
        else items[r.item] = Count(r.item) + 1;
        OnChanged?.Invoke();
        return true;
    }

    /// <summary>Gives crafted items for free (e.g. loot from a chest).</summary>
    public void Grant(CraftItem item, int amount)
    {
        if (amount <= 0) return;
        items[item] = Count(item) + amount;
        OnChanged?.Invoke();
    }

    /// <summary>Uses up one crafted item (false if there is none).</summary>
    public bool Consume(CraftItem item)
    {
        int n = Count(item);
        if (n <= 0) return false;
        items[item] = n - 1;
        OnChanged?.Invoke();
        return true;
    }

    // ------------------------------------------------------------------ tool looks

    /// <summary>Reinforced tools turn steel-grey, Gemforged ones gold with a faint glow.</summary>
    void ApplyToolLooks()
    {
        ToolController tc = ToolController.Instance;
        if (tc == null) return;
        Tint(tc.axeModel, TierOf(ToolType.Axe));
        Tint(tc.pickaxeModel, TierOf(ToolType.Pickaxe));
        Tint(tc.weaponModel, TierOf(ToolType.Weapon));
    }

    static void Tint(GameObject model, int tier)
    {
        if (model == null || tier == 0) return;
        Color c = tier == 2 ? new Color(1f, 0.82f, 0.45f) : new Color(0.78f, 0.82f, 0.88f);
        MaterialPropertyBlock mpb = new MaterialPropertyBlock();
        foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer || r is TrailRenderer) continue;
            r.GetPropertyBlock(mpb);
            mpb.SetColor("_BaseColor", c);
            if (tier == 2) mpb.SetColor("_EmissionColor", new Color(0.35f, 0.18f, 0.02f));
            r.SetPropertyBlock(mpb);
            if (tier == 2) foreach (Material m in r.materials) m.EnableKeyword("_EMISSION");
        }
    }

    // ------------------------------------------------------------------ healing

    void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb != null && Cursor.lockState == CursorLockMode.Locked)
        {
            if (kb.hKey.wasPressedThisFrame) UseSalve();
            if (kb.jKey.wasPressedThisFrame) UsePotion();
        }

        if (healLeft > 0f)
        {
            HealthManager hm = PlayerHealth();
            float step = Mathf.Min(healLeft, healRate * Time.deltaTime);
            healLeft -= step;
            if (hm != null && !hm.IsDead) hm.Heal(step);
            if (healLeft <= 0f && healFx != null) healFx.Stop();
        }
    }

    static HealthManager PlayerHealth()
    {
        ToolController tc = ToolController.Instance;
        return tc != null ? tc.GetComponent<HealthManager>() : null;
    }

    public bool UseSalve()
    {
        HealthManager hm = PlayerHealth();
        if (hm == null || hm.IsDead) return false;
        if (Count(CraftItem.HerbSalve) <= 0) { HudMessage.Show("No Herb Salve - craft one at a workbench"); return false; }
        if (hm.CurrentHealth >= hm.maxHealth - 0.5f) { HudMessage.Show("Health is already full"); return false; }
        Consume(CraftItem.HerbSalve);
        healLeft += 30f;
        healRate = 30f / 4f;
        HealFx(hm.transform, 4f);
        HudMessage.Float("+30 HP", new Color(0.55f, 1f, 0.5f));
        return true;
    }

    public bool UsePotion()
    {
        HealthManager hm = PlayerHealth();
        if (hm == null || hm.IsDead) return false;
        if (Count(CraftItem.HealthPotion) <= 0) { HudMessage.Show("No Health Potion - craft one at a workbench"); return false; }
        if (hm.CurrentHealth >= hm.maxHealth - 0.5f) { HudMessage.Show("Health is already full"); return false; }
        Consume(CraftItem.HealthPotion);
        hm.Heal(60f);
        HealFx(hm.transform, 1.2f);
        HudMessage.Float("+60 HP", new Color(1f, 0.45f, 0.45f));
        return true;
    }

    /// <summary>Soft green motes rising around the player while the heal works.</summary>
    void HealFx(Transform player, float seconds)
    {
        if (healFx == null)
        {
            GameObject go = new GameObject("HealFX");
            go.transform.SetParent(player, false);
            go.transform.localPosition = Vector3.up * 0.2f;
            go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            healFx = go.AddComponent<ParticleSystem>();
            healFx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = healFx.main;
            main.loop = true; main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.5f, 1f, 0.45f), new Color(1f, 0.95f, 0.6f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 80;
            var em = healFx.emission; em.rateOverTime = 26f;
            var sh = healFx.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 0.55f;
            Gradient g = new Gradient();
            g.SetKeys(new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new GradientAlphaKey[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
            var col = healFx.colorOverLifetime; col.enabled = true; col.color = g;
            ParticleSystemRenderer pr = go.GetComponent<ParticleSystemRenderer>();
            pr.sharedMaterial = SoftFX.Spark();
            pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        healFx.Play();
        CancelInvoke("StopHealFx");
        Invoke("StopHealFx", seconds);
    }

    void StopHealFx() { if (healFx != null && healLeft <= 0f) healFx.Stop(); }
}
