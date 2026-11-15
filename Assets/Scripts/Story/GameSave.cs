using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Save / Load for one save slot (a JSON file in Application.persistentDataPath).
/// Saves: player position + health, resources, crafted items, tools + tiers, weapons, hotbar,
/// map pieces, opened chests (and what is still inside), quest stage + keys, gates,
/// built campfires / traps / workbenches, day-night time and the run stats.
///
/// Save:  F5, the SAVE GAME button in the pause menu, and an autosave every dawn and every few minutes.
/// Load:  CONTINUE in the main menu, or TRY AGAIN after dying (goes back to the last save).
/// </summary>
public class GameSave : MonoBehaviour
{
    // ---------------------------------------------------------------- data
    [System.Serializable] public class Placed { public string prefab; public Vector3 pos; public float yaw; }
    [System.Serializable] public class ChestSave { public Vector3 pos; public List<ChestLoot> loot = new List<ChestLoot>(); }
    [System.Serializable] public class IntPair { public int key; public int value; }

    [System.Serializable]
    public class SaveData
    {
        public int version = 1;
        public string savedAt;
        public Vector3 playerPos;
        public float playerYaw;
        public float health;
        public int[] resources;
        public List<IntPair> crafted = new List<IntPair>();
        public bool[] hasTool;
        public int[] toolTier;
        public int weapon;
        public HotbarEntry[] hotbar;
        public List<string> hotbarPlacedOnce = new List<string>();
        public int mapPieces;
        public List<ChestSave> chests = new List<ChestSave>();
        public int stage;
        public List<string> keys = new List<string>();
        public List<Placed> placed = new List<Placed>();
        public int dayPhase;
        public float dayElapsed;
        public float playTime;
        public int kills, bossKills, nights;
    }

    public static GameSave Instance { get; private set; }
    /// <summary>Set before loading the game scene to continue from the save file.</summary>
    public static bool PendingLoad;

    const BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    const BindingFlags SBF = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
    const float AutoSaveEvery = 180f;

    float nextAuto;

    public static string FilePath { get { return Path.Combine(Application.persistentDataPath, "mazura_save.json"); } }
    public static bool HasSave { get { return File.Exists(FilePath); } }

    public static void Delete()
    {
        try { if (File.Exists(FilePath)) File.Delete(FilePath); } catch (System.Exception e) { Debug.LogWarning("GameSave: " + e.Message); }
    }

    /// <summary>Short text for the main menu, e.g. "Keys 2/3  -  saved 14 Nov 18:40".</summary>
    public static string Describe()
    {
        try
        {
            SaveData d = JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath));
            string what;
            switch ((QuestStage)d.stage)
            {
                case QuestStage.Maps: what = "Map pieces " + d.mapPieces + "/4"; break;
                case QuestStage.FindGate: what = "Map complete"; break;
                case QuestStage.Keys: what = "Dungeon - keys " + d.keys.Count + "/3"; break;
                case QuestStage.BossDoor: case QuestStage.Boss: what = "The sealed door"; break;
                default: what = "The portal"; break;
            }
            return what + "   -   " + d.savedAt;
        }
        catch { return ""; }
    }

    // ---------------------------------------------------------------- boot
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (Object.FindAnyObjectByType<ToolController>() == null) { PendingLoad = false; return; }
        if (Instance != null) return;
        new GameObject("GameSave").AddComponent<GameSave>();
    }

    void Awake() { Instance = this; }
    void OnDestroy() { if (Instance == this) Instance = null; }

    void Start()
    {
        nextAuto = Time.time + AutoSaveEvery;
        if (DayNightManager.Instance != null) DayNightManager.Instance.OnDawn.AddListener(() => TrySave(true));
        if (PendingLoad) StartCoroutine(LoadLater());
    }

    IEnumerator LoadLater()
    {
        PendingLoad = false;
        // let every system finish Awake / Start first
        yield return null; yield return null; yield return null;
        bool ok = false;
        try { ok = Load(); }
        catch (System.Exception e) { Debug.LogWarning("GameSave: load failed - " + e); }
        nextAuto = Time.time + AutoSaveEvery;
        if (ok) HudMessage.Show("Game loaded");
    }

    void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb != null && kb.f5Key.wasPressedThisFrame) TrySave(false);
        if (Time.time >= nextAuto)
        {
            nextAuto = Time.time + AutoSaveEvery;
            TrySave(true);
        }
    }

    // ---------------------------------------------------------------- save
    /// <summary>Saves if the game is in a safe state. Returns true if it saved.</summary>
    public bool TrySave(bool auto)
    {
        string why = BlockReason(auto);
        if (why != null) { if (!auto) HudMessage.Show(why); return false; }
        try
        {
            File.WriteAllText(FilePath, JsonUtility.ToJson(Capture(), true));
            HudMessage.Show(auto ? "Autosaved" : "Game saved");
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("GameSave: save failed - " + e);
            if (!auto) HudMessage.Show("Could not save the game");
            return false;
        }
    }

    static string BlockReason(bool auto)
    {
        ToolController tc = ToolController.Instance;
        if (tc == null) return "Can't save now";
        HealthManager hm = tc.GetComponent<HealthManager>();
        if (hm != null && hm.IsDead) return "Can't save now";
        if (StoryScreen.IsOpen) return "Can't save now";
        DungeonQuest q = DungeonQuest.Instance;
        if (q != null && (q.Stage == QuestStage.Boss || q.Stage == QuestStage.Won)) return "Can't save during the boss fight";
        if (PlacementController.IsPlacing || ConstructionSite.Busy) return "Finish building first";
        if (auto && hm != null && hm.CurrentHealth < 30f) return "low health";
        return null;
    }

    SaveData Capture()
    {
        SaveData d = new SaveData();
        d.savedAt = System.DateTime.Now.ToString("d MMM HH:mm");

        ToolController tc = ToolController.Instance;
        d.playerPos = tc.transform.position;
        d.playerYaw = tc.transform.eulerAngles.y;
        HealthManager hm = tc.GetComponent<HealthManager>();
        d.health = hm != null ? hm.CurrentHealth : 100f;

        Inventory inv = Inventory.Instance;
        if (inv != null) d.resources = (int[])((int[])Get(inv, "counts")).Clone();

        CraftingState cs = CraftingState.Instance;
        if (cs != null)
        {
            Dictionary<CraftItem, int> items = (Dictionary<CraftItem, int>)Get(cs, "items");
            foreach (KeyValuePair<CraftItem, int> kv in items) d.crafted.Add(new IntPair { key = (int)kv.Key, value = kv.Value });
            d.hasTool = (bool[])((bool[])Get(cs, "hasTool")).Clone();
            d.toolTier = (int[])((int[])Get(cs, "toolTier")).Clone();
        }
        d.weapon = (int)Weapons.Current;

        Hotbar hb = Hotbar.Instance;
        if (hb != null)
        {
            d.hotbar = (HotbarEntry[])((HotbarEntry[])Get(hb, "slots")).Clone();
            foreach (string s in (HashSet<string>)Get(hb, "placedOnce")) d.hotbarPlacedOnce.Add(s);
        }

        if (MapManager.Instance != null) d.mapPieces = MapManager.Instance.CollectedPieces;

        foreach (MapChest c in Object.FindObjectsByType<MapChest>(FindObjectsSortMode.None))
        {
            if (!(bool)Get(c, "isOpened")) continue;
            ChestSave s = new ChestSave { pos = c.transform.position };
            if (c.Loot != null) s.loot.AddRange(c.Loot);
            d.chests.Add(s);
        }

        DungeonQuest q = DungeonQuest.Instance;
        if (q != null)
        {
            d.stage = (int)q.Stage;
            foreach (string k in (HashSet<string>)Get(q, "keys")) d.keys.Add(k);
        }

        HashSet<Transform> done = new HashSet<Transform>();
        foreach (Campfire c in Object.FindObjectsByType<Campfire>(FindObjectsSortMode.None)) AddPlaced(d, "Crafting/Campfire", c.transform, done);
        foreach (SpikeTrap t in Object.FindObjectsByType<SpikeTrap>(FindObjectsSortMode.None)) AddPlaced(d, "Crafting/SpikeTrap", t.transform, done);
        foreach (Workbench w in Object.FindObjectsByType<Workbench>(FindObjectsSortMode.None)) AddPlaced(d, "Crafting/WorkbenchPlaced", w.transform, done);

        DayNightManager dn = DayNightManager.Instance;
        if (dn != null)
        {
            d.dayPhase = (int)dn.CurrentPhase;
            d.dayElapsed = (float)Get(dn, "elapsed");
        }

        d.playTime = Time.time - GameStats.StartTime;
        d.kills = GameStats.Kills; d.bossKills = GameStats.BossKills; d.nights = GameStats.Nights;
        return d;
    }

    static void AddPlaced(SaveData d, string prefab, Transform t, HashSet<Transform> done)
    {
        // only things the player built (ConstructionSite names them "..._Built"); the script may sit on a child
        while (t != null && !t.name.EndsWith("_Built")) t = t.parent;
        if (t == null || !done.Add(t)) return;
        d.placed.Add(new Placed { prefab = prefab, pos = t.position, yaw = t.eulerAngles.y });
    }

    // ---------------------------------------------------------------- load
    bool Load()
    {
        if (!HasSave) return false;
        SaveData d = JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath));
        if (d == null) return false;

        // resources
        Inventory inv = Inventory.Instance;
        if (inv != null && d.resources != null)
        {
            int[] counts = (int[])Get(inv, "counts");
            for (int i = 0; i < counts.Length && i < d.resources.Length; i++) counts[i] = d.resources[i];
            Raise(inv, "OnChanged");
        }

        // crafted items + tools
        CraftingState cs = CraftingState.Instance;
        if (cs != null)
        {
            Dictionary<CraftItem, int> items = (Dictionary<CraftItem, int>)Get(cs, "items");
            items.Clear();
            foreach (IntPair p in d.crafted) items[(CraftItem)p.key] = p.value;
            if (d.hasTool != null) { bool[] h = (bool[])Get(cs, "hasTool"); for (int i = 0; i < h.Length && i < d.hasTool.Length; i++) h[i] = d.hasTool[i]; }
            if (d.toolTier != null) { int[] t = (int[])Get(cs, "toolTier"); for (int i = 0; i < t.Length && i < d.toolTier.Length; i++) t[i] = d.toolTier[i]; }
            Call(cs, "ApplyToolLooks");
            RaiseStatic(typeof(CraftingState), "OnChanged");
        }
        Weapons.Select((WeaponKind)d.weapon);

        // hotbar
        Hotbar hb = Hotbar.Instance;
        if (hb != null && d.hotbar != null)
        {
            HotbarEntry[] slots = (HotbarEntry[])Get(hb, "slots");
            for (int i = 0; i < slots.Length && i < d.hotbar.Length; i++) slots[i] = d.hotbar[i];
            HashSet<string> once = (HashSet<string>)Get(hb, "placedOnce");
            once.Clear();
            foreach (string s in d.hotbarPlacedOnce) once.Add(s);
            RaiseStatic(typeof(Hotbar), "OnChanged");
        }

        // map pieces
        MapManager mm = MapManager.Instance;
        if (mm != null)
        {
            SetProp(mm, "CollectedPieces", d.mapPieces);
            Call(mm, "UpdateHUD");
        }

        // chests that were already opened
        MapChest[] chests = Object.FindObjectsByType<MapChest>(FindObjectsSortMode.None);
        foreach (ChestSave s in d.chests)
        {
            MapChest best = null; float bd = 1f;
            foreach (MapChest c in chests)
            {
                float dist = Vector3.Distance(c.transform.position, s.pos);
                if (dist < bd) { bd = dist; best = c; }
            }
            if (best == null) continue;
            Set(best, "isOpened", true);
            best.Loot.Clear();
            best.Loot.AddRange(s.loot);
            Transform lid = (Transform)Get(best, "lidPivot");
            if (lid != null) lid.localRotation = Quaternion.Euler((float)Get(best, "openAngle"), 0f, 0f) * lid.localRotation;
        }

        // quest
        DungeonQuest q = DungeonQuest.Instance;
        QuestStage stage = (QuestStage)d.stage;
        if (stage == QuestStage.Boss) stage = QuestStage.BossDoor;   // the boss fight starts again
        if (q != null)
        {
            HashSet<string> keys = (HashSet<string>)Get(q, "keys");
            keys.Clear();
            foreach (string k in d.keys) keys.Add(k);
            SetProp(q, "Stage", stage);
        }
        foreach (DungeonKey k in Object.FindObjectsByType<DungeonKey>(FindObjectsSortMode.None))
        {
            if (!d.keys.Contains(k.id)) continue;
            if (k.model != null) k.model.gameObject.SetActive(false);
            if (k.glow != null) k.glow.intensity = 0f;
            k.enabled = false;
        }
        if (stage >= QuestStage.FindGate)
        {
            DungeonGate g = Object.FindAnyObjectByType<DungeonGate>();
            if (g != null) { Set(g, "open", true); Set(g, "t", 1f); }
        }
        if (stage >= QuestStage.BossDoor)
        {
            BossGate g = Object.FindAnyObjectByType<BossGate>();
            if (g != null) { Set(g, "open", true); Set(g, "t", 1f); if (g.blocker != null) g.blocker.enabled = false; }
        }
        if (stage >= QuestStage.Escape)
        {
            BossArena a = Object.FindAnyObjectByType<BossArena>();
            if (a != null)
            {
                Set(a, "spawned", true);
                Set(a, "done", true);
                if (a.portal == null) a.portal = (GameObject)Call(a, "BuildPortal", a.portalPoint != null ? a.portalPoint.position : a.area.center);
            }
        }

        // things the player built
        foreach (Placed p in d.placed)
        {
            GameObject pf = Resources.Load<GameObject>(p.prefab);
            if (pf == null) continue;
            GameObject go = Instantiate(pf, p.pos, Quaternion.Euler(0f, p.yaw, 0f));
            go.name = Path.GetFileName(p.prefab).Replace("Placed", "") + "_Built";
        }

        // time of day
        DayNightManager dn = DayNightManager.Instance;
        if (dn != null)
        {
            Set(dn, "phase", (DayNightManager.Phase)d.dayPhase);
            Set(dn, "elapsed", d.dayElapsed);
            Call(dn, "Apply");
        }

        // stats
        SetStaticProp(typeof(GameStats), "StartTime", Time.time - d.playTime);
        SetStaticProp(typeof(GameStats), "Kills", d.kills);
        SetStaticProp(typeof(GameStats), "BossKills", d.bossKills);
        SetStaticProp(typeof(GameStats), "Nights", d.nights);

        // player (last, so the camera lands on the right spot)
        ToolController tc = ToolController.Instance;
        if (tc != null)
        {
            DungeonQuest.TeleportPlayer(d.playerPos, d.playerYaw);
            HealthManager hm = tc.GetComponent<HealthManager>();
            if (hm != null)
            {
                SetProp(hm, "CurrentHealth", Mathf.Max(30f, d.health));
                Call(hm, "UpdateHud");
            }
        }
        return true;
    }

    // ---------------------------------------------------------------- reflection helpers
    static FieldInfo Field(System.Type t, string name, BindingFlags f)
    {
        for (System.Type x = t; x != null; x = x.BaseType)
        {
            FieldInfo fi = x.GetField(name, f | BindingFlags.DeclaredOnly);
            if (fi != null) return fi;
        }
        return null;
    }

    static object Get(object o, string name) { FieldInfo f = Field(o.GetType(), name, BF); return f != null ? f.GetValue(o) : null; }
    static void Set(object o, string name, object v) { FieldInfo f = Field(o.GetType(), name, BF); if (f != null) f.SetValue(o, v); }

    static void SetProp(object o, string name, object v)
    {
        PropertyInfo p = o.GetType().GetProperty(name, BF);
        MethodInfo m = p != null ? p.GetSetMethod(true) : null;
        if (m != null) m.Invoke(o, new object[] { v });
    }

    static void SetStaticProp(System.Type t, string name, object v)
    {
        PropertyInfo p = t.GetProperty(name, SBF);
        MethodInfo m = p != null ? p.GetSetMethod(true) : null;
        if (m != null) m.Invoke(null, new object[] { v });
    }

    static object Call(object o, string name, params object[] args)
    {
        foreach (MethodInfo m in o.GetType().GetMethods(BF))
            if (m.Name == name && m.GetParameters().Length == args.Length) return m.Invoke(o, args);
        return null;
    }

    static void Raise(object o, string evt)
    {
        FieldInfo f = Field(o.GetType(), evt, BF);
        System.Delegate del = f != null ? f.GetValue(o) as System.Delegate : null;
        if (del != null) del.DynamicInvoke();
    }

    static void RaiseStatic(System.Type t, string evt)
    {
        FieldInfo f = t.GetField(evt, SBF);
        System.Delegate del = f != null ? f.GetValue(null) as System.Delegate : null;
        if (del != null) del.DynamicInvoke();
    }
}
