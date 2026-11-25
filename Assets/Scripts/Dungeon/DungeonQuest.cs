using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public enum QuestStage { Maps, FindGate, Keys, BossDoor, Boss, Escape, Won }

/// <summary>
/// The story / goal of the game:
/// 1. find the 4 map pieces in the Dark Forest  ->  the Old Dungeon gate opens
/// 2. go down into the Dungeon and find the 3 keys  ->  the sealed door opens
/// 3. defeat the Dungeon Lord  ->  a portal opens  ->  step in to escape (win).
/// Also teleports the player between the forest and the dungeon.
/// </summary>
public class DungeonQuest : MonoBehaviour
{
    public static DungeonQuest Instance { get; private set; }

    public QuestStage Stage { get; private set; }
    public const int KeysNeeded = 3;
    readonly HashSet<string> keys = new HashSet<string>();
    public int KeyCount { get { return keys.Count; } }
    public bool HasKey(string id) { return keys.Contains(id); }

    DungeonGate forestGate;
    BossGate bossGate;
    BossArena arena;
    Transform dungeonStart;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (Instance != null) return;
        if (Object.FindAnyObjectByType<DungeonZone>() == null) return;
        new GameObject("DungeonQuest").AddComponent<DungeonQuest>();
    }

    void Awake()
    {
        Instance = this;
        forestGate = Object.FindAnyObjectByType<DungeonGate>();
        bossGate = Object.FindAnyObjectByType<BossGate>();
        arena = Object.FindAnyObjectByType<BossArena>();
        foreach (DungeonMarker m in Object.FindObjectsByType<DungeonMarker>(FindObjectsSortMode.None))
            if (m.kind == DungeonMarker.Kind.DungeonStart) dungeonStart = m.transform;
        Stage = QuestStage.Maps;
    }

    void Update()
    {
        switch (Stage)
        {
            case QuestStage.Maps:
                if (MapManager.Instance != null && MapManager.Instance.CollectedPieces >= 4)
                {
                    Stage = QuestStage.FindGate;
                    if (forestGate != null) forestGate.Open();
                    NightUI.Banner("THE MAP IS COMPLETE", "The gate of the Old Dungeon has opened", false);
                }
                break;
            case QuestStage.FindGate:
                if (DungeonZone.PlayerInside) Stage = QuestStage.Keys;
                break;
            case QuestStage.Keys:
                if (keys.Count >= KeysNeeded)
                {
                    Stage = QuestStage.BossDoor;
                    if (bossGate != null) bossGate.Open();
                    NightUI.Banner("THE SEALED DOOR OPENS", "Something ancient waits behind it", true);
                }
                break;
        }
    }

    // ---------------------------------------------------------------- called by the world
    public void AddKey(string id, string niceName, Color color)
    {
        if (!keys.Add(id)) return;
        HudMessage.Float(niceName + " found  (" + keys.Count + "/" + KeysNeeded + ")", color);
        HudMessage.Show(keys.Count < KeysNeeded ? "Keys: " + keys.Count + " / " + KeysNeeded : "All keys found! Go to the sealed door");
    }

    public void BossStarted() { if (Stage < QuestStage.Boss) Stage = QuestStage.Boss; }
    public void BossDefeated()
    {
        Stage = QuestStage.Escape;
        NightUI.Banner("THE DUNGEON LORD IS DEAD", "A portal has opened - escape the maze!", false);
    }

    public void Escape()
    {
        if (Stage == QuestStage.Won) return;
        Stage = QuestStage.Won;
        GameSave.Delete();   // the run is over
        StoryScreen.FadeThrough(() => StoryScreen.ShowWin());
    }

    public void EnterDungeon()
    {
        if (dungeonStart == null) return;
        Transform s = dungeonStart;
        LoadingScreen.Transition("dungeon", null, () =>
        {
            TeleportPlayer(s.position, s.eulerAngles.y);
            NightUI.Banner("THE OLD DUNGEON", "Find the 3 keys to open the sealed door", true);
        });
    }

    public void LeaveDungeon()
    {
        if (forestGate == null || forestGate.outsidePoint == null) return;
        Transform o = forestGate.outsidePoint;
        LoadingScreen.Transition("forest", "Returning to the Dark Forest...", () => TeleportPlayer(o.position, o.eulerAngles.y));
    }

    // ---------------------------------------------------------------- objective text for the HUD
    public string Objective
    {
        get
        {
            int maps = MapManager.Instance != null ? MapManager.Instance.CollectedPieces : 0;
            switch (Stage)
            {
                case QuestStage.Maps: return "Find the 4 pieces of the ancient map  (" + maps + "/4)";
                case QuestStage.FindGate: return "The map is complete - find the gate of the Old Dungeon";
                case QuestStage.Keys:
                    return DungeonZone.PlayerInside ? "Find the 3 dungeon keys  (" + keys.Count + "/3)" : "Return to the Old Dungeon  (keys " + keys.Count + "/3)";
                case QuestStage.BossDoor: return "All keys found - open the sealed door";
                case QuestStage.Boss: return "Defeat the Dungeon Lord";
                case QuestStage.Escape: return "Step into the portal to escape the maze!";
                default: return "You escaped!";
            }
        }
    }

    /// <summary>Where the objective arrow points (null = no arrow).</summary>
    public bool TryGetTarget(out Vector3 target)
    {
        target = Vector3.zero;
        bool inDungeon = DungeonZone.PlayerInside;
        switch (Stage)
        {
            case QuestStage.FindGate:
                if (forestGate == null || inDungeon) return false;
                target = forestGate.transform.position; return true;
            case QuestStage.Keys:
                if (!inDungeon) { if (forestGate == null) return false; target = forestGate.transform.position; return true; }
                return false;
            case QuestStage.BossDoor:
                if (!inDungeon) { if (forestGate == null) return false; target = forestGate.transform.position; return true; }
                if (bossGate == null) return false;
                target = bossGate.transform.position; return true;
            case QuestStage.Escape:
                if (arena == null || arena.portal == null) return false;
                target = arena.portal.transform.position; return true;
        }
        return false;
    }

    // ---------------------------------------------------------------- teleport
    public static void TeleportPlayer(Vector3 pos, float yaw)
    {
        ToolController tc = ToolController.Instance;
        if (tc == null) return;
        Transform pl = tc.transform;
        Vector3 delta = pos - pl.position;
        CharacterController cc = pl.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        pl.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
        if (cc != null) cc.enabled = true;
        Physics.SyncTransforms();

        // the third person controller keeps its own camera yaw
        foreach (MonoBehaviour mb in pl.GetComponents<MonoBehaviour>())
        {
            if (mb == null || mb.GetType().Name != "ThirdPersonController") continue;
            FieldInfo f = mb.GetType().GetField("_cinemachineTargetYaw", BindingFlags.NonPublic | BindingFlags.Instance);
            if (f != null) f.SetValue(mb, yaw);
            FieldInfo p = mb.GetType().GetField("_cinemachineTargetPitch", BindingFlags.NonPublic | BindingFlags.Instance);
            if (p != null) p.SetValue(mb, 10f);
        }
        Transform root = pl.Find("PlayerCameraRoot");
        if (root != null) root.rotation = Quaternion.Euler(10f, yaw, 0f);

        // tell the cinemachine cameras the target jumped (no long camera swoop)
        foreach (MonoBehaviour mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
        {
            if (mb == null) continue;
            MethodInfo m = mb.GetType().GetMethod("OnTargetObjectWarped", new System.Type[] { typeof(Transform), typeof(Vector3) });
            if (m == null) continue;
            m.Invoke(mb, new object[] { pl, delta });
            if (root != null) m.Invoke(mb, new object[] { root, delta });
        }
    }
}
