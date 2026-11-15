using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Builds the Old Dungeon (a big generated maze, like the Dark Forest) with the "3D Dungeon Lowpoly Pack" pieces, and places the
/// dungeon gate in a far dead end of the Dark Forest maze.  Menu: Tools > Mazura > Build Dungeon.
/// Map legend:
///   #  rock            .  floor            A  boss hall floor (high ceiling)
///   B  boss rises      P  escape portal    G  sealed door (needs the 3 keys)
///   R Y N  red / yellow / green key        S  player arrives    X  way back up
///   c  column          b  barrels          t  table             k  book shelf
///   e  dungeon guard   s  dungeon specter
/// </summary>
public static class DungeonBuilder
{
    const string Kit = "Assets/Scenes/CatBorg Studio/3D Dungeon Lowpoly Pack/Prefabs/";
    public static readonly Vector3 Origin = new Vector3(400f, 0f, 0f);
    const float C = 4f;
    const float Ceil = 2.9f, CeilHigh = 5.85f, WallH = 2.96f;
    static readonly Vector3 WallScale = new Vector3(1f, 1.07f, 1f);

    // ------------------------------------------------------------------ the map (generated)
    // A big maze like the Dark Forest: 41 x 41 tiles (164 m), corridors on odd tiles, rock between them.
    const int N = 41;
    static string[] map;
    static string[] Map { get { if (map == null) map = MakeMap(1337); return map; } set { map = value; } }

    static readonly RectInt Hall = new RectInt(17, 1, 9, 7);          // boss hall (high ceiling)
    static readonly RectInt RedRoom = new RectInt(1, 1, 5, 5);
    static readonly RectInt YellowRoom = new RectInt(35, 1, 5, 5);
    static readonly RectInt GreenRoom = new RectInt(1, 35, 5, 5);
    static readonly RectInt StartRoom = new RectInt(19, 35, 5, 5);

    static bool InRect(RectInt r, int x, int y) { return x >= r.xMin && x < r.xMax && y >= r.yMin && y < r.yMax; }
    static bool Reserved(int x, int y)
    {
        return InRect(Hall, x, y) || InRect(RedRoom, x, y) || InRect(YellowRoom, x, y) || InRect(GreenRoom, x, y) || InRect(StartRoom, x, y);
    }

    static string[] MakeMap(int seed)
    {
        System.Random r = new System.Random(seed);
        char[,] g = new char[N, N];
        for (int y = 0; y < N; y++) for (int x = 0; x < N; x++) g[x, y] = '#';
        System.Action<RectInt, char> fill = (rc, ch) => { for (int y = rc.yMin; y < rc.yMax; y++) for (int x = rc.xMin; x < rc.xMax; x++) g[x, y] = ch; };
        fill(Hall, 'A'); fill(RedRoom, '.'); fill(YellowRoom, '.'); fill(GreenRoom, '.'); fill(StartRoom, '.');

        // maze: recursive backtracker over the odd cells
        bool[,] seen = new bool[N, N];
        int[] sx = { 2, -2, 0, 0 }, sy = { 0, 0, 2, -2 };
        Stack<Vector2Int> st = new Stack<Vector2Int>();
        Vector2Int s0 = new Vector2Int(21, 33);
        seen[s0.x, s0.y] = true; g[s0.x, s0.y] = '.'; st.Push(s0);
        while (st.Count > 0)
        {
            Vector2Int c = st.Peek();
            List<int> opts = new List<int>();
            for (int d = 0; d < 4; d++)
            {
                int nx = c.x + sx[d], ny = c.y + sy[d];
                if (nx < 1 || ny < 1 || nx > N - 2 || ny > N - 2 || seen[nx, ny] || Reserved(nx, ny)) continue;
                opts.Add(d);
            }
            if (opts.Count == 0) { st.Pop(); continue; }
            int k = opts[r.Next(opts.Count)];
            int ax = c.x + sx[k], ay = c.y + sy[k];
            g[c.x + sx[k] / 2, c.y + sy[k] / 2] = '.';
            g[ax, ay] = '.';
            seen[ax, ay] = true;
            st.Push(new Vector2Int(ax, ay));
        }
        // a few loops so it is not one long corridor (the forest has them too)
        for (int y = 1; y < N - 1; y++)
            for (int x = 1; x < N - 1; x++)
            {
                if (g[x, y] != '#' || Reserved(x, y)) continue;
                bool hor = (x % 2 == 0) && (y % 2 == 1), ver = (x % 2 == 1) && (y % 2 == 0);
                if (!hor && !ver) continue;
                int ax = hor ? x - 1 : x, ay = hor ? y : y - 1, bx = hor ? x + 1 : x, by = hor ? y : y + 1;
                if (Reserved(ax, ay) || Reserved(bx, by) || g[ax, ay] != '.' || g[bx, by] != '.') continue;
                if (r.NextDouble() < 0.09) g[x, y] = '.';
            }

        // doors into the rooms
        g[21, 8] = 'G';                                   // sealed door of the boss hall
        g[6, 3] = '.'; g[34, 3] = '.'; g[3, 34] = '.';     // key rooms
        g[21, 34] = '.'; g[18, 37] = '.'; g[24, 37] = '.'; // start room

        // boss hall
        g[21, 3] = 'B'; g[21, 5] = 'P';
        g[18, 2] = 'c'; g[24, 2] = 'c'; g[18, 6] = 'c'; g[24, 6] = 'c';
        // key rooms
        g[3, 3] = 'R'; g[2, 1] = 'k'; g[4, 1] = 'k'; g[1, 5] = 'b'; g[5, 5] = 't'; g[2, 4] = 'e';
        g[37, 3] = 'Y'; g[36, 1] = 'k'; g[38, 1] = 'k'; g[39, 5] = 'b'; g[35, 5] = 't'; g[38, 4] = 's';
        g[3, 37] = 'N'; g[2, 39] = 'k'; g[4, 39] = 'k'; g[1, 35] = 'b'; g[5, 36] = 't'; g[4, 36] = 'e';
        // start room
        g[21, 37] = 'S'; g[21, 39] = 'X'; g[19, 39] = 'b'; g[23, 39] = 'b'; g[19, 35] = 'k';

        // the maze cells
        List<Vector2Int> cells = new List<Vector2Int>();
        for (int y = 1; y < N - 1; y += 2)
            for (int x = 1; x < N - 1; x += 2)
                if (!Reserved(x, y) && g[x, y] == '.') cells.Add(new Vector2Int(x, y));
        // barrels in some dead ends
        foreach (Vector2Int c in cells)
        {
            int open = 0;
            for (int d = 0; d < 4; d++) if (g[c.x + sx[d] / 2, c.y + sy[d] / 2] != '#') open++;
            if (open == 1 && r.NextDouble() < 0.35) g[c.x, c.y] = 'b';
        }
        // enemies: spread out, away from the start room
        for (int i = cells.Count - 1; i > 0; i--) { int j = r.Next(i + 1); Vector2Int t = cells[i]; cells[i] = cells[j]; cells[j] = t; }
        List<Vector2Int> placed = new List<Vector2Int>();
        int guards = 0, specters = 0;
        foreach (Vector2Int c in cells)
        {
            if (guards >= 16 && specters >= 6) break;
            if (g[c.x, c.y] != '.') continue;
            if (Mathf.Abs(c.x - 21) + Mathf.Abs(c.y - 37) < 12) continue;
            bool near = false;
            foreach (Vector2Int p in placed) if (Mathf.Abs(p.x - c.x) + Mathf.Abs(p.y - c.y) < 7) { near = true; break; }
            if (near) continue;
            if (guards < 16) { g[c.x, c.y] = 'e'; guards++; } else { g[c.x, c.y] = 's'; specters++; }
            placed.Add(c);
        }

        string[] rows = new string[N];
        for (int y = 0; y < N; y++)
        {
            char[] row = new char[N];
            for (int x = 0; x < N; x++) row[x] = g[x, y];
            rows[y] = new string(row);
        }
        return rows;
    }

    static int W { get { return Map[0].Length; } }
    static int H { get { return Map.Length; } }
    static char At(int x, int y) { return x < 0 || y < 0 || x >= W || y >= H ? '#' : Map[y][x]; }
    static bool Floor(int x, int y) { return At(x, y) != '#'; }
    static bool High(int x, int y) { return Floor(x, y) && InRect(Hall, x, y); }
    static Vector3 Cell(int x, int y) { return Origin + new Vector3(x * C + C * 0.5f, 0f, (H - 1 - y) * C + C * 0.5f); }

    // E, N, W, S in map space (map y grows southwards)
    static readonly int[] DX = { 1, 0, -1, 0 };
    static readonly int[] DY = { 0, -1, 0, 1 };
    static Vector3 WorldDir(int d) { return new Vector3(DX[d], 0f, -DY[d]); }

    static GameObject P(string n) { return AssetDatabase.LoadAssetAtPath<GameObject>(Kit + n + ".prefab"); }

    static System.Random rng;
    static Transform root, geo, deco, lights;

    [MenuItem("Tools/Mazura/Build Dungeon")]
    public static void Build()
    {
        rng = new System.Random(1337);
        Map = MakeMap(1337);
        torches.Clear();
        GameObject old = GameObject.Find("OldDungeon");
        if (old != null) Object.DestroyImmediate(old);
        root = new GameObject("OldDungeon").transform;
        geo = new GameObject("Geometry").transform; geo.SetParent(root, false);
        deco = new GameObject("Decor").transform; deco.SetParent(root, false);
        lights = new GameObject("Lights").transform; lights.SetParent(root, false);
        Transform ceil = new GameObject("Ceiling").transform; ceil.SetParent(root, false);
        Transform gameplay = new GameObject("Gameplay").transform; gameplay.SetParent(root, false);
        NavMeshModifier ignore = ceil.gameObject.AddComponent<NavMeshModifier>(); ignore.ignoreFromBuild = true;
        NavMeshModifier ignore2 = lights.gameObject.AddComponent<NavMeshModifier>(); ignore2.ignoreFromBuild = true;

        string[] floors = { "99_Enviroment_Floor_01", "99_Enviroment_Floor_02", "99_Enviroment_Floor_03", "99_Enviroment_Floor_01" };
        string[] walls = { "99_Enviroment_Wall_01", "99_Enviroment_Wall_02", "99_Enviroment_Wall_03" };
        string[] banners = { "99_Decorations_WallBanner_0", "99_Decorations_WallBanner_1", "99_Decorations_WallBanner_2", "99_Decorations_WallBanner_3", "99_Decorations_WallBanner_4", "99_Decorations_WallBanner_5" };

        List<GameObject> keyLightsList = new List<GameObject>();
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                if (!Floor(x, y)) continue;
                Vector3 c = Cell(x, y);
                bool high = High(x, y);
                // floor + ceiling
                Box(Put(floors[rng.Next(floors.Length)], geo, c, Quaternion.Euler(0f, 90f * rng.Next(4), 0f)));
                GameObject ct = Put(floors[rng.Next(floors.Length)], ceil, c + Vector3.up * ((high ? CeilHigh : Ceil) + 0.09f), Quaternion.Euler(180f, 90f * rng.Next(4), 0f));
                Box(ct);
                // walls
                for (int d = 0; d < 4; d++)
                {
                    int nx = x + DX[d], ny = y + DY[d];
                    Quaternion rot = Quaternion.LookRotation(-WorldDir(d));   // wall prefab: wall on its -Z side
                    bool solid = !Floor(nx, ny);
                    if (solid)
                    {
                        Box(Put(walls[rng.Next(walls.Length)], geo, c, rot, WallScale));
                        if (high) Box(Put(walls[rng.Next(walls.Length)], geo, c + Vector3.up * WallH, rot, WallScale));
                        int hsh = (x * 73856093) ^ (y * 19349663) ^ (d * 83492791);
                        hsh = Mathf.Abs(hsh);
                        if (hsh % 3 == 0 && TorchFree(c + WorldDir(d) * 1.78f, high ? 7f : 9f)) Torch(c + WorldDir(d) * 1.78f + Vector3.up * 1.5f, -WorldDir(d), high ? 4f : 3.2f);
                        else if (hsh % 7 == 3) Put(banners[rng.Next(banners.Length)], deco, c + WorldDir(d) * 1.9f + Vector3.up * (high ? 4.6f : 2.65f), Quaternion.LookRotation(-WorldDir(d)));
                    }
                    else if (high && !High(nx, ny))
                        Box(Put(walls[rng.Next(walls.Length)], geo, c + Vector3.up * WallH, rot, WallScale));
                }
                // corner cobwebs
                for (int d = 0; d < 4; d++)
                {
                    int e = (d + 1) % 4;
                    if (Floor(x + DX[d], y + DY[d]) || Floor(x + DX[e], y + DY[e])) continue;
                    if (rng.NextDouble() > 0.35) continue;
                    Vector3 corner = c + (WorldDir(d) + WorldDir(e)) * 1.55f + Vector3.up * ((high ? CeilHigh : Ceil) - 0.6f);
                    Put("99_Decorations_SpiderWebb_2", deco, corner, Quaternion.LookRotation(-(WorldDir(d) + WorldDir(e))));
                }

                char ch = At(x, y);
                switch (ch)
                {
                    case 'c': Box(Put("99_Enviroment_Column_Rocky_1", geo, c, Quaternion.Euler(0f, 90f * rng.Next(4), 0f), high ? new Vector3(1f, 1.95f, 1f) : Vector3.one)); break;
                    case 'b': Props(x, y, c); break;
                    case 't': Box(Put(rng.Next(2) == 0 ? "01_DiningHall_01_WoodenTable#1" : "02_Kitchen_01_WoodenTable#2", deco, c + NearWall(x, y) * 0.6f, WallFacing(x, y))); break;
                    case 'k': Box(Put(rng.Next(2) == 0 ? "01_Library_01_BookShelve_1" : "01_Library_01_BookShelve_2", deco, c + NearWall(x, y) * 1.45f, WallFacing(x, y))); break;
                    case 'R': Key(gameplay, c, "red", "Red Key", new Color(1f, 0.25f, 0.2f), "97_Othert_Key_Red"); break;
                    case 'Y': Key(gameplay, c, "yellow", "Yellow Key", new Color(1f, 0.85f, 0.2f), "97_Othert_Key_Yellow"); break;
                    case 'N': Key(gameplay, c, "green", "Green Key", new Color(0.3f, 1f, 0.35f), "97_Othert_Key_Green"); break;
                    case 'G': Gate(gameplay, c); break;
                    case 'S': Marker(gameplay, "DungeonStart", DungeonMarker.Kind.DungeonStart, c, 0f); break;
                    case 'B': Marker(gameplay, "BossSpawn", DungeonMarker.Kind.BossSpawn, c, 180f); break;
                    case 'P': Marker(gameplay, "PortalPoint", DungeonMarker.Kind.Portal, c, 0f); break;
                    case 'X': WayBack(gameplay, c); break;
                }
            }

        // nav mesh
        NavMeshSurface surf = root.gameObject.AddComponent<NavMeshSurface>();
        surf.collectObjects = CollectObjects.Children;
        surf.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surf.BuildNavMesh();

        // enemies (after the nav mesh)
        Transform enemies = new GameObject("Enemies").transform; enemies.SetParent(root, false);
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                char ch = At(x, y);
                if (ch != 'e' && ch != 's') continue;
                Enemy(enemies, ch == 'e' ? "DungeonGuard" : "DungeonSpecter", x, y);
            }

        // zone + boss hall
        Bounds all = new Bounds(Cell(0, 0), Vector3.zero);
        all.Encapsulate(Cell(W - 1, H - 1)); all.Encapsulate(Cell(0, H - 1)); all.Encapsulate(Cell(W - 1, 0));
        all.Expand(new Vector3(C, 0f, C)); all.center += Vector3.up * 3f; all.size = new Vector3(all.size.x, 14f, all.size.z);
        DungeonZone zone = root.gameObject.AddComponent<DungeonZone>();
        zone.area = all;

        BossArena arena = new GameObject("BossArena").AddComponent<BossArena>();
        arena.transform.SetParent(gameplay, false);
        Bounds ab = new Bounds(Cell(Hall.xMin, Hall.yMin), Vector3.zero);
        ab.Encapsulate(Cell(Hall.xMax - 1, Hall.yMax - 1)); ab.Expand(new Vector3(C * 0.9f, 0f, C * 0.9f));
        ab.center += Vector3.up * 2f; ab.size = new Vector3(ab.size.x, 8f, ab.size.z);
        arena.area = ab;
        arena.bossPrefab = MakeDungeonLord();
        foreach (DungeonMarker m in root.GetComponentsInChildren<DungeonMarker>())
        {
            if (m.kind == DungeonMarker.Kind.BossSpawn) arena.bossPoint = m.transform;
            if (m.kind == DungeonMarker.Kind.Portal) arena.portalPoint = m.transform;
        }
        // a big brazier glow in the boss hall
        AddLight(Cell(21, 2) + Vector3.up * 4.8f, new Color(0.65f, 0.4f, 1f), 18f, 1.6f, false);
        AddLight(Cell(21, 6) + Vector3.up * 4.8f, new Color(0.65f, 0.4f, 1f), 18f, 1.6f, false);

        if (GameObject.Find("DungeonGate_Forest") == null) PlaceForestGate();
        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        Debug.Log("DungeonBuilder: built the Old Dungeon at " + Origin);
    }

    // ------------------------------------------------------------------ pieces
    static GameObject Put(string prefab, Transform parent, Vector3 pos, Quaternion rot) { return Put(prefab, parent, pos, rot, Vector3.one); }

    static GameObject Put(string prefab, Transform parent, Vector3 pos, Quaternion rot, Vector3 scale)
    {
        GameObject src = P(prefab);
        if (src == null) { Debug.LogWarning("DungeonBuilder: missing " + prefab); return new GameObject("missing_" + prefab); }
        GameObject g = (GameObject)PrefabUtility.InstantiatePrefab(src, parent);
        g.transform.SetPositionAndRotation(pos, rot);
        g.transform.localScale = scale;
        if (parent == geo || (parent != null && parent.name == "Ceiling") || parent == deco)
            GameObjectUtility.SetStaticEditorFlags(g, StaticEditorFlags.BatchingStatic);
        return g;
    }

    /// <summary>Adds one BoxCollider that wraps all meshes of the object.</summary>
    static void Box(GameObject g)
    {
        if (g == null) return;
        Bounds b = new Bounds(); bool any = false;
        foreach (MeshFilter mf in g.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh == null) continue;
            Bounds mb = mf.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 w = mf.transform.TransformPoint(corner);
                Vector3 l = g.transform.InverseTransformPoint(w);
                if (!any) { b = new Bounds(l, Vector3.zero); any = true; } else b.Encapsulate(l);
            }
        }
        if (!any) return;
        BoxCollider bc = g.AddComponent<BoxCollider>();
        bc.center = b.center; bc.size = b.size;
    }

    static Light AddLight(Vector3 pos, Color col, float range, float intensity, bool flicker)
    {
        Light l = new GameObject("Light").AddComponent<Light>();
        l.transform.SetParent(lights, false);
        l.transform.position = pos;
        l.type = LightType.Point; l.color = col; l.range = range; l.intensity = intensity;
        l.shadows = LightShadows.None;
        if (flicker) l.gameObject.AddComponent<TorchFlicker>();
        return l;
    }

    static readonly List<Vector3> torches = new List<Vector3>();
    static bool TorchFree(Vector3 p, float min)
    {
        foreach (Vector3 t in torches) if ((t - p).sqrMagnitude < min * min) return false;
        torches.Add(p);
        return true;
    }

    static void Torch(Vector3 pos, Vector3 facing, float intensity)
    {
        Put("97_Othert_Torch_1", deco, pos - Vector3.up * 0.5f, Quaternion.LookRotation(facing));
        AddLight(pos + facing * 0.45f + Vector3.up * 0.35f, new Color(1f, 0.58f, 0.25f), 8f, intensity, true);
    }

    static Vector3 NearWall(int x, int y)
    {
        for (int d = 0; d < 4; d++) if (!Floor(x + DX[d], y + DY[d])) return WorldDir(d);
        return Vector3.zero;
    }

    static Quaternion WallFacing(int x, int y)
    {
        Vector3 w = NearWall(x, y);
        return w == Vector3.zero ? Quaternion.identity : Quaternion.LookRotation(-w);
    }

    static void Props(int x, int y, Vector3 c)
    {
        string[] props = { "97_Othert_Barrel_Whole", "97_Othert_Barrel_MK2", "97_Othert_Amphora_Whole" };
        Vector3 w = NearWall(x, y);
        Vector3 side = Vector3.Cross(Vector3.up, w == Vector3.zero ? Vector3.forward : w);
        int n = 1 + rng.Next(2);
        for (int i = 0; i < n; i++)
        {
            Vector3 p = c + w * 1.0f + side * ((i - (n - 1) * 0.5f) * 1.6f);
            GameObject g = Put(props[rng.Next(props.Length)], deco, p, Quaternion.Euler(0f, rng.Next(360), 0f), Vector3.one * 0.75f);
            Box(g);
        }
    }

    static void Key(Transform parent, Vector3 c, string id, string name, Color col, string prefab)
    {
        GameObject root = new GameObject("Key_" + id);
        root.transform.SetParent(parent, false);
        root.transform.position = c;
        GameObject ped = Put("99_Enviroment_Column_Rocky_1", root.transform, c, Quaternion.identity, new Vector3(0.5f, 0.33f, 0.5f));
        Box(ped);
        GameObject model = Put(prefab, root.transform, c + Vector3.up * 1.15f, Quaternion.identity, Vector3.one * 0.8f);
        Light l = new GameObject("Glow").AddComponent<Light>();
        l.transform.SetParent(root.transform, false);
        l.transform.position = c + Vector3.up * 1.6f;
        l.type = LightType.Point; l.color = col; l.range = 4.5f; l.intensity = 1.6f; l.shadows = LightShadows.None;
        DungeonKey k = root.AddComponent<DungeonKey>();
        k.id = id; k.niceName = name; k.color = col; k.model = model.transform; k.glow = l;
    }

    static void Gate(Transform parent, Vector3 c)
    {
        GameObject g = new GameObject("SealedDoor");
        g.transform.SetParent(parent, false);
        g.transform.position = c;
        g.transform.rotation = Quaternion.Euler(0f, 90f, 0f);   // doorway across X
        GameObject doors = Put("99_Enviroment_Doors", g.transform, c - Vector3.forward * 1.6f, g.transform.rotation, new Vector3(0.68f, 0.68f, 0.9f));
        Transform frame = doors.transform.Find("DoorFrames");
        if (frame != null) Box(frame.gameObject);
        BoxCollider blocker = g.AddComponent<BoxCollider>();
        blocker.center = g.transform.InverseTransformPoint(c - Vector3.forward * 1.6f + Vector3.up * 1.4f);
        blocker.size = new Vector3(0.8f, 2.8f, 4f);
        NavMeshModifier mod = g.AddComponent<NavMeshModifier>(); mod.ignoreFromBuild = true;
        BossGate bg = g.AddComponent<BossGate>();
        bg.leftDoor = doors.transform.Find("LeftDoors");
        bg.rightDoor = doors.transform.Find("RightDoors");
        bg.blocker = blocker;
        Color[] cols = { new Color(1f, 0.25f, 0.2f), new Color(1f, 0.85f, 0.2f), new Color(0.3f, 1f, 0.35f) };
        bg.keyLights = new Light[3];
        for (int i = 0; i < 3; i++)
        {
            Light l = new GameObject("KeyLight" + i).AddComponent<Light>();
            l.transform.SetParent(g.transform, false);
            l.transform.position = c - Vector3.forward * 2.3f + Vector3.up * 2.4f + Vector3.right * ((i - 1) * 0.9f);
            l.type = LightType.Point; l.color = cols[i]; l.range = 3.5f; l.intensity = 2.5f; l.shadows = LightShadows.None;
            l.enabled = false;
            bg.keyLights[i] = l;
        }
    }

    static void Marker(Transform parent, string name, DungeonMarker.Kind kind, Vector3 c, float yaw)
    {
        DungeonMarker m = new GameObject(name).AddComponent<DungeonMarker>();
        m.transform.SetParent(parent, false);
        m.transform.SetPositionAndRotation(c + Vector3.up * 0.15f, Quaternion.Euler(0f, yaw, 0f));
        m.kind = kind;
    }

    static void WayBack(Transform parent, Vector3 c)
    {
        Vector3 wall = c - Vector3.forward * 1.92f;
        GameObject d = Put("99_Enviroment_DoorsFake#1", deco, wall, Quaternion.LookRotation(Vector3.forward), Vector3.one * 1.15f);
        GameObject rd = new GameObject("WayBackUp");
        rd.transform.SetParent(parent, false);
        rd.transform.position = c - Vector3.forward * 1.2f;
        rd.AddComponent<ReturnDoor>();
        AddLight(c - Vector3.forward * 1.3f + Vector3.up * 2.2f, new Color(0.75f, 0.85f, 1f), 5f, 1.6f, false);
    }

    static void Enemy(Transform parent, string prefab, int x, int y)
    {
        GameObject src = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Enemies/Prefabs/" + prefab + ".prefab");
        if (src == null) return;
        Vector3 c = Cell(x, y);
        NavMeshHit hit;
        if (NavMesh.SamplePosition(c, out hit, 2f, NavMesh.AllAreas)) c = hit.position;
        GameObject g = (GameObject)PrefabUtility.InstantiatePrefab(src, parent);
        g.transform.SetPositionAndRotation(c, Quaternion.Euler(0f, rng.Next(360), 0f));
        EnemyBase e = g.GetComponent<EnemyBase>();
        if (e != null)
        {
            e.patrolPoints = new List<Vector3>();
            int px = x, py = y;
            e.patrolPoints.Add(Cell(px, py));
            for (int i = 0; i < 4; i++)
            {
                for (int tries = 0; tries < 8; tries++)
                {
                    int d = rng.Next(4);
                    int nx = px + DX[d] * 2, ny = py + DY[d] * 2;
                    if (Floor(nx, ny) && Floor(px + DX[d], py + DY[d]) && !High(nx, ny) && At(nx, ny) != 'G') { px = nx; py = ny; e.patrolPoints.Add(Cell(px, py)); break; }
                }
            }
        }
    }

    // ------------------------------------------------------------------ boss
    static GameObject MakeDungeonLord()
    {
        const string prefabPath = "Assets/Enemies/Prefabs/DungeonLord.prefab";
        const string dataPath = "Assets/Enemies/Data/DungeonLord.asset";
        EnemyData data = AssetDatabase.LoadAssetAtPath<EnemyData>(dataPath);
        if (data == null)
        {
            AssetDatabase.CopyAsset("Assets/Enemies/Data/ForestWarden.asset", dataPath);
            data = AssetDatabase.LoadAssetAtPath<EnemyData>(dataPath);
            data.displayName = "Dungeon Lord";
            data.biome = EnemyBiome.Dungeon;
            data.isBoss = true;
            data.maxHealth = 1100f;
            data.damage = 34f;
            data.scale = 1.55f;
            data.tint = new Color(0.55f, 0.45f, 0.75f);
            data.glowColor = new Color(0.9f, 0.3f, 2.4f);
            data.lootItem = ItemType.Sapphire;
            EditorUtility.SetDirty(data);
        }
        GameObject pf = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (pf == null)
        {
            AssetDatabase.CopyAsset("Assets/Enemies/Prefabs/ForestWarden.prefab", prefabPath);
            GameObject contents = PrefabUtility.LoadPrefabContents(prefabPath);
            EnemyBase eb = contents.GetComponent<EnemyBase>();
            if (eb != null) eb.data = data;
            BossController bc = contents.GetComponent<BossController>();
            if (bc != null)
            {
                bc.minionPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Enemies/Prefabs/DungeonGuard.prefab");
                bc.rewardItem = ItemType.Sapphire;
                bc.phase2Glow = new Color(1.2f, 0.3f, 2.5f);
            }
            contents.name = "DungeonLord";
            PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
            PrefabUtility.UnloadPrefabContents(contents);
            pf = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        }
        AssetDatabase.SaveAssets();
        return pf;
    }

    // ------------------------------------------------------------------ the gate in the forest
    static bool WallHit(Vector3 c, Vector3 d, Transform walls)
    {
        foreach (RaycastHit h in Physics.RaycastAll(c, d, 2.3f, ~0, QueryTriggerInteraction.Ignore))
            if (walls == null || h.collider.transform.IsChildOf(walls)) return true;
        return false;
    }

    [MenuItem("Tools/Mazura/Place Forest Dungeon Gate")]
    public static void PlaceForestGate()
    {
        GameObject old = GameObject.Find("DungeonGate_Forest");
        if (old != null) Object.DestroyImmediate(old);

        List<Vector3> avoid = new List<Vector3>();
        foreach (MapChest mc in Object.FindObjectsByType<MapChest>(FindObjectsInactive.Include, FindObjectsSortMode.None)) avoid.Add(mc.transform.position);
        Vector3 start = new Vector3(2f, 0f, 2f);
        Vector3[] dirs = { Vector3.right, Vector3.forward, Vector3.left, Vector3.back };

        GameObject wr = GameObject.Find("MazeGenerator/GeneratedMaze/Walls");
        Transform wallsRoot = wr != null ? wr.transform : null;
        Vector3 best = Vector3.zero, bestOpen = Vector3.zero; float bestScore = -1f;
        for (int cx = 0; cx < 25; cx++)
            for (int cz = 0; cz < 25; cz++)
            {
                Vector3 c = new Vector3(cx * 4f + 2f, 1.2f, cz * 4f + 2f);
                int open = 0; Vector3 od = Vector3.zero;
                foreach (Vector3 d in dirs)
                    if (!WallHit(c, d, wallsRoot)) { open++; od = d; }
                if (open != 1) continue;
                bool bad = false;
                foreach (Vector3 a in avoid) if ((new Vector3(a.x, 1.2f, a.z) - c).magnitude < 14f) { bad = true; break; }
                if (bad) continue;
                float score = Vector3.Distance(new Vector3(c.x, 0f, c.z), start) + (cx > 6 && cz > 6 ? 20f : 0f);
                if (score > bestScore) { bestScore = score; best = new Vector3(c.x, 0f, c.z); bestOpen = od; }
            }
        if (bestScore < 0f) { Debug.LogWarning("DungeonBuilder: no dead end found for the forest gate"); return; }

        // clear the cell (hide trees, rocks and props standing in it)
        foreach (string n in new[] { "Decorations", "Details", "Herbs" })
        {
            GameObject g = GameObject.Find("MazeGenerator/GeneratedMaze/" + n);
            if (g == null) continue;
            foreach (Transform t in g.GetComponentsInChildren<Transform>(true))
            {
                if (t == g.transform || t.parent == null) continue;
                Vector3 d = t.position - best; d.y = 0f;
                Vector3 d2 = t.position - (best + bestOpen * 4f); d2.y = 0f;
                bool inGate = d.magnitude < 2.7f || d2.magnitude < 2.4f;
                if (inGate && (t.parent == g.transform || t.parent.parent == g.transform) && t.childCount < 30) t.gameObject.SetActive(false);
            }
        }

        GameObject root = new GameObject("DungeonGate_Forest");
        root.transform.position = best;
        float yaw = Mathf.Atan2(-bestOpen.z, bestOpen.x) * Mathf.Rad2Deg;   // local +X faces the open side
        root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        Vector3 back = -bestOpen;

        GameObject doors = (GameObject)PrefabUtility.InstantiatePrefab(P("99_Enviroment_Doors"), root.transform);
        doors.transform.position = best + back * 1.45f;
        doors.transform.rotation = root.transform.rotation;
        doors.transform.localScale = new Vector3(1f, 0.9f, 0.85f);
        Transform frame = doors.transform.Find("DoorFrames");
        if (frame != null) Box(frame.gameObject);

        Vector3 side = Vector3.Cross(Vector3.up, bestOpen).normalized;
        lights = root.transform;
        deco = root.transform;
        for (int s = -1; s <= 1; s += 2)
        {
            GameObject col = (GameObject)PrefabUtility.InstantiatePrefab(P("99_Enviroment_Column_Rocky_1"), root.transform);
            col.transform.position = best + back * 1.2f + side * (s * 2.05f);
            col.transform.localScale = new Vector3(0.9f, 1.25f, 0.9f);
            Box(col);
            Torch(best + back * 0.6f + side * (s * 2.05f) + Vector3.up * 1.9f, bestOpen, 3f);
            GameObject ban = (GameObject)PrefabUtility.InstantiatePrefab(P(s < 0 ? "99_Decorations_WallBanner_2" : "99_Decorations_WallBanner_5"), root.transform);
            ban.transform.SetPositionAndRotation(best + back * 1.05f + side * (s * 2.05f) + Vector3.up * 3.6f, Quaternion.LookRotation(bestOpen));
        }

        // darkness behind the doors
        GameObject voidQ = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.DestroyImmediate(voidQ.GetComponent<Collider>());
        voidQ.name = "Darkness";
        voidQ.transform.SetParent(root.transform, false);
        voidQ.transform.SetPositionAndRotation(best + back * 1.62f + Vector3.up * 1.75f, Quaternion.LookRotation(back));
        voidQ.transform.localScale = new Vector3(3.6f, 3.5f, 1f);
        Material dark = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        dark.SetColor("_BaseColor", new Color(0.01f, 0.005f, 0.02f));
        AssetDatabase.CreateAsset(dark, "Assets/Materials/DungeonGate_Darkness.mat");
        voidQ.GetComponent<Renderer>().sharedMaterial = dark;

        Light glow = new GameObject("GateGlow").AddComponent<Light>();
        glow.transform.SetParent(root.transform, false);
        glow.transform.position = best + back * 0.6f + Vector3.up * 2f;
        glow.type = LightType.Point; glow.color = new Color(0.65f, 0.4f, 1f); glow.range = 9f; glow.intensity = 0f;

        Transform outside = new GameObject("OutsidePoint").transform;
        outside.SetParent(root.transform, false);
        outside.position = best + bestOpen * 1.0f + Vector3.up * 0.1f;
        outside.rotation = Quaternion.LookRotation(bestOpen);

        DungeonGate gate = root.AddComponent<DungeonGate>();
        gate.leftDoor = doors.transform.Find("LeftDoors");
        gate.rightDoor = doors.transform.Find("RightDoors");
        gate.outsidePoint = outside;
        gate.glow = glow;
        NavMeshObstacle obs = root.AddComponent<NavMeshObstacle>();
        obs.shape = NavMeshObstacleShape.Box; obs.carving = true;
        obs.center = root.transform.InverseTransformPoint(best + back * 1.4f + Vector3.up * 1.5f);
        obs.size = new Vector3(1.2f, 3f, 4f);
        Debug.Log("DungeonBuilder: forest gate at " + best + " opening " + bestOpen);
    }
}
