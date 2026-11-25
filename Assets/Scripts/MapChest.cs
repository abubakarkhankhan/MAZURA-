using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>One thing lying inside a chest.</summary>
[System.Serializable]
public class ChestLoot
{
    public enum Kind { Map, Resource, Crafted, Tool }
    public Kind kind;
    public ItemType item;
    public ToolType tool;
    public CraftItem crafted;
    public int amount = 1;

    public Sprite Icon()
    {
        switch (kind)
        {
            case Kind.Map: return Resources.Load<Sprite>("ChestUI/icon_map");
            case Kind.Tool: return Resources.Load<Sprite>(tool == ToolType.Axe ? "WorkshopUI/icon_axe" : tool == ToolType.Pickaxe ? "WorkshopUI/icon_pickaxe" : "WorkshopUI/icon_sword");
            case Kind.Resource:
                if (item == ItemType.Wood) return Resources.Load<Sprite>("WorkshopUI/res_wood");
                if (item == ItemType.Stone) return Resources.Load<Sprite>("WorkshopUI/res_stone");
                if (item == ItemType.Herb) return Resources.Load<Sprite>("WorkshopUI/res_herb");
                return Resources.Load<Sprite>("Crafting/Item_" + item);
            default:
                switch (crafted)
                {
                    case CraftItem.HerbSalve: return Resources.Load<Sprite>("WorkshopUI/icon_herbsalve");
                    case CraftItem.HealthPotion: return Resources.Load<Sprite>("Crafting/Icon_HealthPotion");
                    case CraftItem.Torch: return Resources.Load<Sprite>("WorkshopUI/icon_torch");
                    case CraftItem.Campfire: return Resources.Load<Sprite>("WorkshopUI/icon_campfire");
                    case CraftItem.Shield: return Resources.Load<Sprite>("WorkshopUI/icon_shield");
                    default: return Resources.Load<Sprite>("WorkshopUI/icon_spiketrap");
                }
        }
    }

    public string DisplayName()
    {
        switch (kind)
        {
            case Kind.Map: return "Map Piece";
            case Kind.Tool: return tool == ToolType.Weapon ? "Sword" : tool.ToString();
            case Kind.Resource: return item == ItemType.Herb ? "Herbs" : item.ToString();
            default:
                switch (crafted)
                {
                    case CraftItem.HerbSalve: return "Herb Salve";
                    case CraftItem.HealthPotion: return "Health Potion";
                    case CraftItem.SpikeTrap: return "Spike Trap";
                    case CraftItem.Shield: return "Wooden Shield";
                    default: return crafted.ToString();
                }
        }
    }
}

/// <summary>
/// A treasure chest in a dead end of the maze.
/// Walk up and press E: the player reaches in, the lid swings open (creak, warm glow, sparkles)
/// and the loot window shows what is inside: always a map piece (while pieces are missing),
/// a few materials and sometimes a crafted item. Take items one by one or all at once.
/// A chest with items left can be searched again with E.
/// Needs a trigger collider on this object and the player tagged "Player".
/// </summary>
[RequireComponent(typeof(Collider))]
public class MapChest : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The pivot that rotates to open the lid (placed at the lid's hinge).")]
    [SerializeField] private Transform lidPivot;

    [Header("Settings")]
    [SerializeField] private float openAngle = -110f;   // degrees around the pivot's X axis
    [SerializeField] private float openDuration = 0.9f; // seconds
    [SerializeField] private float promptHeight = 1.35f;

    [Header("Loot")]
    [Tooltip("Chance (0-1) that the chest also holds a crafted item (potion, salve or torch).")]
    [Range(0f, 1f)] [SerializeField] private float craftedChance = 0.45f;
    [Tooltip("The chest next to the start: holds the Axe and the Pickaxe instead of random loot.")]
    [SerializeField] private bool starterKit;

    [Header("Dungeon")]
    [Tooltip("A chest in the Old Dungeon: no map pieces, richer loot (gems, potions) and it fills up again after a while.")]
    public bool dungeonChest;
    [Tooltip("Minutes until an emptied dungeon chest has new loot.")]
    public float refillMinutes = 4f;
    float emptySince = -1f;

    public List<ChestLoot> Loot { get; private set; }

    bool playerInRange;
    bool isOpened;
    bool opening;
    TextMeshPro prompt;
    float promptAlpha;
    Light glow;
    ParticleSystem sparkles;
    float glowTarget;

    void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    void Awake()
    {
        Loot = new List<ChestLoot>();
        GameObject p = new GameObject("Prompt");
        p.transform.SetParent(transform, false);
        p.transform.localPosition = Vector3.up * promptHeight;
        prompt = p.AddComponent<TextMeshPro>();
        prompt.text = "<color=#E8C060>[E]</color>  Open Chest";
        prompt.fontSize = 1.6f;
        prompt.alignment = TextAlignmentOptions.Center;
        prompt.fontStyle = FontStyles.Bold;
        prompt.color = new Color(0.95f, 0.89f, 0.74f, 0f);
        prompt.outlineWidth = 0.25f;
        prompt.outlineColor = new Color32(15, 8, 4, 255);
        prompt.rectTransform.sizeDelta = new Vector2(8f, 1.2f);
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player")) playerInRange = true;
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerInRange = false;
        if (ChestLootUI.IsOpen) ChestLootUI.Close();
    }

    void Update()
    {
        UpdatePrompt();
        if (glow != null) glow.intensity = Mathf.MoveTowards(glow.intensity, glowTarget, Time.deltaTime * 2.5f);

        // dungeon chests fill up again some minutes after they were emptied
        if (dungeonChest && isOpened && !opening && Loot.Count == 0)
        {
            if (emptySince < 0f) { emptySince = Time.time; glowTarget = 0f; }
            else if (Time.time - emptySince > refillMinutes * 60f && !ChestLootUI.IsOpen)
            {
                RollLoot();
                emptySince = -1f;
                glowTarget = 1.1f;
                if (sparkles != null) sparkles.Emit(16);
            }
        }

        if (!playerInRange || opening) return;
        if (isOpened && Loot.Count == 0) return;
        if (ChestLootUI.IsOpen || WorkshopUI.IsOpen) return;
        if (Time.frameCount - ChestLootUI.ClosedFrame <= 1) return;   // same E press that closed the window
        if (Cursor.lockState != CursorLockMode.Locked) return;
        ToolController tc = ToolController.Instance;
        HealthManager hm = tc != null ? tc.GetComponent<HealthManager>() : null;
        if (hm != null && hm.IsDead) return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.eKey.wasPressedThisFrame) return;

        if (!isOpened) StartCoroutine(OpenSequence());
        else ChestLootUI.Open(this);
    }

    void UpdatePrompt()
    {
        Camera cam = Camera.main;
        bool tooClose = cam != null && Vector3.Distance(cam.transform.position, prompt.transform.position) < 1.4f;
        bool show = playerInRange && !opening && !ChestLootUI.IsOpen && !(isOpened && Loot.Count == 0) && !tooClose;
        if (isOpened) prompt.text = "<color=#E8C060>[E]</color>  Search Chest";
        promptAlpha = Mathf.MoveTowards(promptAlpha, show ? 1f : 0f, Time.unscaledDeltaTime * 5f);
        Color c = prompt.color; c.a = promptAlpha; prompt.color = c;
        if (promptAlpha > 0.001f && cam != null)
        {
            prompt.transform.rotation = cam.transform.rotation;
            prompt.transform.localPosition = Vector3.up * (promptHeight + Mathf.Sin(Time.time * 2.2f) * 0.05f);
        }
    }

    // ---------------------------------------------------------------- opening

    IEnumerator OpenSequence()
    {
        opening = true;
        isOpened = true;
        RollLoot();

        // the player reaches for the chest
        ToolController tc = ToolController.Instance;
        if (tc != null && tc.animator != null) tc.animator.SetTrigger("Craft");

        yield return new WaitForSeconds(0.25f);
        PlaceFX.Play(PlaceFX.Creak(), transform.position + Vector3.up * 0.6f, 0.8f);
        BuildGlow();
        glowTarget = 2.2f;
        if (sparkles != null) { sparkles.Emit(28); sparkles.Play(); }

        if (lidPivot != null)
        {
            Quaternion start = lidPivot.localRotation;
            Quaternion end = Quaternion.Euler(openAngle, 0f, 0f) * start;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(0.01f, openDuration);
                float k = EaseOutBack(Mathf.Clamp01(t));
                lidPivot.localRotation = Quaternion.SlerpUnclamped(start, end, k);
                yield return null;
            }
            lidPivot.localRotation = end;
        }
        PlaceFX.Play(PlaceFX.Thud(), transform.position + Vector3.up * 0.6f, 0.35f);
        glowTarget = 1.1f;
        opening = false;

        if (playerInRange && !ChestLootUI.IsOpen && !WorkshopUI.IsOpen) ChestLootUI.Open(this);
    }

    static float EaseOutBack(float x)
    {
        const float c1 = 1.4f, c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }

    void BuildGlow()
    {
        if (glow != null) return;
        GameObject g = new GameObject("TreasureGlow");
        g.transform.SetParent(transform, false);
        g.transform.localPosition = new Vector3(0f, 0.75f, 0.05f);
        glow = g.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = new Color(1f, 0.78f, 0.38f);
        glow.range = 3.2f;
        glow.intensity = 0f;
        glow.shadows = LightShadows.None;

        GameObject s = new GameObject("TreasureSparkles");
        s.transform.SetParent(transform, false);
        s.transform.localPosition = new Vector3(0f, 0.55f, 0.05f);
        s.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        sparkles = s.AddComponent<ParticleSystem>();
        sparkles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = sparkles.main;
        main.loop = true; main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.1f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.4f), new Color(1f, 0.97f, 0.75f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 80;
        var em = sparkles.emission; em.rateOverTime = 5f;
        var sh = sparkles.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(0.6f, 0.35f, 0.05f);
        Gradient gr = new Gradient();
        gr.SetKeys(new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                   new GradientAlphaKey[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
        var col = sparkles.colorOverLifetime; col.enabled = true; col.color = gr;
        ParticleSystemRenderer pr = s.GetComponent<ParticleSystemRenderer>();
        pr.sharedMaterial = SoftFX.Spark();
        pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    // ---------------------------------------------------------------- loot

    void RollLoot()
    {
        Loot.Clear();
        if (dungeonChest) { DungeonLoot(); return; }
        if (starterKit)
        {
            Loot.Add(new ChestLoot { kind = ChestLoot.Kind.Tool, tool = ToolType.Axe, amount = 1 });
            Loot.Add(new ChestLoot { kind = ChestLoot.Kind.Tool, tool = ToolType.Pickaxe, amount = 1 });
            Loot.Add(new ChestLoot { kind = ChestLoot.Kind.Resource, item = ItemType.Wood, amount = 3 });
            return;
        }
        MapManager mm = MapManager.Instance;
        if (mm != null && mm.CollectedPieces < mm.MaxPieces)
            Loot.Add(new ChestLoot { kind = ChestLoot.Kind.Map, amount = 1 });

        List<ChestLoot> pool = new List<ChestLoot>
        {
            new ChestLoot { kind = ChestLoot.Kind.Resource, item = ItemType.Wood, amount = Random.Range(3, 7) },
            new ChestLoot { kind = ChestLoot.Kind.Resource, item = ItemType.Stone, amount = Random.Range(3, 7) },
            new ChestLoot { kind = ChestLoot.Kind.Resource, item = ItemType.Herb, amount = Random.Range(2, 4) },
            new ChestLoot { kind = ChestLoot.Kind.Resource, item = (ItemType)Random.Range((int)ItemType.Ruby, (int)ItemType.Amber + 1), amount = Random.Range(1, 3) },
            new ChestLoot { kind = ChestLoot.Kind.Resource, item = ItemType.Amber, amount = Random.Range(1, 3) },
        };
        int count = Random.Range(3, 5);
        for (int i = 0; i < count && pool.Count > 0; i++)
        {
            int k = Random.Range(0, pool.Count);
            ChestLoot l = pool[k];
            pool.RemoveAt(k);
            // merge if the same resource is already in (e.g. two Amber lines)
            ChestLoot same = Loot.Find(x => x.kind == ChestLoot.Kind.Resource && x.item == l.item);
            if (same != null) same.amount += l.amount; else Loot.Add(l);
        }

        // rare: a shield, if the player has none yet
        if (CraftingState.Instance != null && CraftingState.Instance.Count(CraftItem.Shield) == 0 && Random.value < 0.2f)
            Loot.Add(new ChestLoot { kind = ChestLoot.Kind.Crafted, crafted = CraftItem.Shield, amount = 1 });
        else if (Random.value < craftedChance)
        {
            CraftItem[] rare = { CraftItem.HealthPotion, CraftItem.HerbSalve, CraftItem.Torch };
            Loot.Add(new ChestLoot { kind = ChestLoot.Kind.Crafted, crafted = rare[Random.Range(0, rare.Length)], amount = 1 });
        }
    }

    /// <summary>Loot of a dungeon chest: gems, stone and herbs, often a potion. Never map pieces.</summary>
    void DungeonLoot()
    {
        List<ChestLoot> pool = new List<ChestLoot>
        {
            new ChestLoot { kind = ChestLoot.Kind.Resource, item = ItemType.Stone, amount = Random.Range(4, 9) },
            new ChestLoot { kind = ChestLoot.Kind.Resource, item = ItemType.Herb, amount = Random.Range(2, 6) },
            new ChestLoot { kind = ChestLoot.Kind.Resource, item = ItemType.Ruby, amount = Random.Range(1, 4) },
            new ChestLoot { kind = ChestLoot.Kind.Resource, item = ItemType.Sapphire, amount = Random.Range(1, 4) },
            new ChestLoot { kind = ChestLoot.Kind.Resource, item = ItemType.Emerald, amount = Random.Range(1, 4) },
            new ChestLoot { kind = ChestLoot.Kind.Resource, item = ItemType.Amber, amount = Random.Range(1, 4) },
        };
        // there are no trees down here: wood always comes from the chests
        Loot.Add(new ChestLoot { kind = ChestLoot.Kind.Resource, item = ItemType.Wood, amount = Random.Range(4, 8) });
        int count = Random.Range(2, 5);
        for (int i = 0; i < count && pool.Count > 0; i++)
        {
            int k = Random.Range(0, pool.Count);
            Loot.Add(pool[k]);
            pool.RemoveAt(k);
        }
        if (Random.value < 0.75f)
        {
            CraftItem[] rare = { CraftItem.HealthPotion, CraftItem.HealthPotion, CraftItem.HerbSalve, CraftItem.Torch };
            Loot.Add(new ChestLoot { kind = ChestLoot.Kind.Crafted, crafted = rare[Random.Range(0, rare.Length)], amount = 1 });
        }
        if (Random.value < 0.25f)
            Loot.Add(new ChestLoot { kind = ChestLoot.Kind.Crafted, crafted = CraftItem.HealthPotion, amount = 1 });
    }

    /// <summary>Gives the player the item in this slot. Returns false if nothing was taken.</summary>
    public bool Take(int index)
    {
        if (index < 0 || index >= Loot.Count) return false;
        ChestLoot l = Loot[index];
        switch (l.kind)
        {
            case ChestLoot.Kind.Map:
                if (MapManager.Instance != null && MapManager.Instance.AddMapPiece())
                    HudMessage.Float("+1 Map Piece", new Color(1f, 0.82f, 0.4f));
                else
                    HudMessage.Show("Map is already complete");
                break;
            case ChestLoot.Kind.Tool:
                if (CraftingState.Instance == null) return false;
                CraftingState.Instance.GiveTool(l.tool);
                HudMessage.Float("+ " + l.DisplayName() + "  [" + ((int)l.tool + 1) + "]", new Color(0.85f, 0.9f, 1f));
                if (ToolController.Instance != null && ToolController.Instance.CurrentTool == ToolType.None && !TorchController.IsHeld) ToolController.Instance.Equip(l.tool);
                break;
            case ChestLoot.Kind.Resource:
                if (Inventory.Instance == null) return false;
                Inventory.Instance.Add(l.item, l.amount);
                break;
            default:
                if (CraftingState.Instance == null) return false;
                CraftingState.Instance.Grant(l.crafted, l.amount);
                HudMessage.Float("+" + l.amount + " " + l.DisplayName(), new Color(1f, 0.75f, 0.55f));
                break;
        }
        Loot.RemoveAt(index);
        if (Loot.Count == 0)
        {
            glowTarget = 0f;
            if (sparkles != null) sparkles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
        return true;
    }
}
