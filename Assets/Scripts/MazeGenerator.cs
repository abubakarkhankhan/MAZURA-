using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

/// <summary>
/// Generates a large "Dark Forest" maze from cubes (iterative DFS / recursive backtracker),
/// moves the player to the start cell, spawns 4 map chests in far dead ends and scatters
/// trees and rocks in the cell corners.
/// Use: right-click this component > "Generate Maze" (works in Edit mode and Play mode).
/// Keep this object at scale (1,1,1). Cell (0,0) is the start cell (south-west corner).
/// </summary>
public class MazeGenerator : MonoBehaviour
{
    private const string RootName = "GeneratedMaze";

    [Header("Maze size")]
    [SerializeField, Min(2)] private int width = 25;
    [SerializeField, Min(2)] private int height = 25;
    [SerializeField] private float cellSize = 4f;        // metres per cell
    [SerializeField] private float wallHeight = 3f;      // metres
    [SerializeField] private float wallThickness = 0.5f; // metres

    [Header("Generation")]
    [Tooltip("Chance (0-1) that each remaining inner wall is opened, giving loops and shortcuts.")]
    [SerializeField, Range(0f, 0.5f)] private float loopChance = 0.08f;
    [Tooltip("0 = a different random maze every time you generate. Any other number = same maze.")]
    [SerializeField] private int seed = 0;

    [Header("Materials (empty = default dark URP Lit colour)")]
    [SerializeField] private Material wallMaterial;   // dark green
    [SerializeField] private Material floorMaterial;  // dark brown

    [Header("Player")]
    [Tooltip("Empty = the object tagged 'Player' is used.")]
    [SerializeField] private Transform player;
    [Tooltip("Height of the player's feet above the floor when teleported (small, so it lands softly).")]
    [SerializeField] private float playerSpawnHeight = 0.1f;

    [Header("Chests")]
    [SerializeField] private GameObject chestPrefab;     // Assets/Prefabs/MapChest.prefab
    [SerializeField] private int chestCount = 4;
    [Tooltip("Preferred minimum distance between two chests, in cells (relaxed if not enough dead ends).")]
    [SerializeField] private int chestMinSpacing = 8;

    [Header("Surface tiling (metres covered by one texture tile)")]
    [SerializeField] private float wallTileSize = 3f;
    [SerializeField] private float floorTileSize = 4f;

    [Header("Details: small props on the floor and along the walls")]
    [Tooltip("Grass, pebbles, mushrooms... scattered on the floor (never inside walls).")]
    [SerializeField] private GameObject[] groundProps;
    [SerializeField] private int groundPropCount = 700;
    [Tooltip("Ferns, bushes, tall grass at the bottom of the walls.")]
    [SerializeField] private GameObject[] wallBaseProps;
    [SerializeField] private float wallBaseSpacing = 4f;
    [Tooltip("Bushes on top of the walls, so the wall edge is not a straight line.")]
    [SerializeField] private GameObject[] wallTopProps;
    [SerializeField] private float wallTopSpacing = 9f;
    [Tooltip("Same number = same look when you press 'Refresh Look'.")]
    [SerializeField] private int detailSeed = 4242;

    [Header("Decorations (trees, rocks)")]
    [SerializeField] private GameObject[] decorations;
    [SerializeField] private int decorationCount = 250;
    [SerializeField] private float decorationScale = 0.7f;

    // Wall data. vWall[x,y] = vertical wall on the WEST side of cell (x,y); x = width is the east border.
    // hWall[x,y] = horizontal wall on the SOUTH side of cell (x,y); y = height is the north border.
    private const float VExtraT = 0.03f; // extra thickness / length of vertical walls (metres)
    private const float VExtraH = 0.04f; // extra height of vertical walls (metres)

    private bool[,] vWall;
    private bool[,] hWall;

    // East, North, West, South
    private static readonly Vector2Int[] Dirs =
    {
        new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(-1, 0), new Vector2Int(0, -1)
    };

    [ContextMenu("Generate Maze")]
    public void Generate()
    {
        ClearMaze();

        int usedSeed = seed != 0 ? seed : System.Environment.TickCount;
        System.Random rng = new System.Random(usedSeed);
        Debug.Log("MazeGenerator: generating " + width + "x" + height + " maze, seed " + usedSeed, this);

        BuildGrid(rng);

        Transform root = new GameObject(RootName).transform;
        root.SetParent(transform, false);

        BuildFloor(root);
        BuildWalls(root);

        int[,] dist = ComputeDistances();
        List<Vector2Int> chestCells = PlaceChests(root, dist);
        PlaceDecorations(root, rng, chestCells);
        BuildDetails(root, new System.Random(usedSeed + 7));
        MovePlayerToStart();

#if UNITY_EDITOR
        if (!Application.isPlaying) EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
    }

    // ---------------------------------------------------------------- clearing

    // Deletes the previously generated maze (walls, floor, chests, decorations).
    private void ClearMaze()
    {
        Transform old = transform.Find(RootName);
        if (old == null) return;

        if (Application.isPlaying)
        {
            old.name = RootName + "_old"; // Destroy is delayed, so rename to avoid name clashes
            Destroy(old.gameObject);
        }
        else
        {
            DestroyImmediate(old.gameObject);
        }
    }

    // ---------------------------------------------------------------- maze logic

    // Carves the maze with an iterative depth-first search (explicit stack, no recursion).
    private void BuildGrid(System.Random rng)
    {
        vWall = new bool[width + 1, height];
        hWall = new bool[width, height + 1];
        for (int x = 0; x <= width; x++) for (int y = 0; y < height; y++) vWall[x, y] = true;
        for (int x = 0; x < width; x++) for (int y = 0; y <= height; y++) hWall[x, y] = true;

        bool[,] visited = new bool[width, height];
        Stack<Vector2Int> stack = new Stack<Vector2Int>();
        List<Vector2Int> options = new List<Vector2Int>(4);

        visited[0, 0] = true;
        stack.Push(new Vector2Int(0, 0));

        while (stack.Count > 0)
        {
            Vector2Int cell = stack.Peek();

            options.Clear();
            foreach (Vector2Int d in Dirs)
            {
                Vector2Int n = cell + d;
                if (InBounds(n) && !visited[n.x, n.y]) options.Add(n);
            }

            if (options.Count == 0) { stack.Pop(); continue; } // dead end: backtrack

            Vector2Int next = options[rng.Next(options.Count)];
            RemoveWall(cell, next);
            visited[next.x, next.y] = true;
            stack.Push(next);
        }

        // Loops: open some extra inner walls so there are shortcuts and escape routes.
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (x < width - 1 && vWall[x + 1, y] && rng.NextDouble() < loopChance) vWall[x + 1, y] = false;
                if (y < height - 1 && hWall[x, y + 1] && rng.NextDouble() < loopChance) hWall[x, y + 1] = false;
            }
        }
    }

    private bool InBounds(Vector2Int c) { return c.x >= 0 && c.y >= 0 && c.x < width && c.y < height; }

    // Opens the wall between two neighbouring cells.
    private void RemoveWall(Vector2Int a, Vector2Int b)
    {
        if (a.x != b.x) vWall[Mathf.Max(a.x, b.x), a.y] = false;
        else hWall[a.x, Mathf.Max(a.y, b.y)] = false;
    }

    // True if you can walk from cell c in direction d (no wall in between).
    private bool IsOpen(Vector2Int c, Vector2Int d)
    {
        if (d.x == 1) return !vWall[c.x + 1, c.y];
        if (d.x == -1) return !vWall[c.x, c.y];
        if (d.y == 1) return !hWall[c.x, c.y + 1];
        return !hWall[c.x, c.y];
    }

    // Walking distance (in cells) from the start cell to every cell (breadth-first search).
    private int[,] ComputeDistances()
    {
        int[,] dist = new int[width, height];
        for (int x = 0; x < width; x++) for (int y = 0; y < height; y++) dist[x, y] = -1;

        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        dist[0, 0] = 0;
        queue.Enqueue(new Vector2Int(0, 0));

        while (queue.Count > 0)
        {
            Vector2Int c = queue.Dequeue();
            foreach (Vector2Int d in Dirs)
            {
                Vector2Int n = c + d;
                if (InBounds(n) && dist[n.x, n.y] < 0 && IsOpen(c, d))
                {
                    dist[n.x, n.y] = dist[c.x, c.y] + 1;
                    queue.Enqueue(n);
                }
            }
        }
        return dist;
    }

    // Local-space centre of a cell (floor level).
    private Vector3 CellCenter(Vector2Int c)
    {
        return new Vector3((c.x + 0.5f) * cellSize, 0f, (c.y + 0.5f) * cellSize);
    }

    // ---------------------------------------------------------------- building

    private void BuildFloor(Transform parent)
    {
        Vector3 size = new Vector3(width * cellSize + wallThickness, 0.2f, height * cellSize + wallThickness);
        Vector3 pos = new Vector3(width * cellSize * 0.5f, -0.1f, height * cellSize * 0.5f); // top surface at y = 0
        MakeCube("Floor", parent, pos, size, GetMaterial(floorMaterial, new Color(0.2f, 0.12f, 0.07f)));
    }

    // Builds walls as long boxes: neighbouring wall pieces are merged into one cube per straight run.
    private void BuildWalls(Transform parent)
    {
        Transform walls = new GameObject("Walls").transform;
        walls.SetParent(parent, false);
        Material mat = GetMaterial(wallMaterial, new Color(0.07f, 0.17f, 0.09f));

        // Vertical wall lines (they run along Z).
        for (int x = 0; x <= width; x++)
        {
            int y = 0;
            while (y < height)
            {
                if (!vWall[x, y]) { y++; continue; }
                int start = y;
                while (y < height && vWall[x, y]) y++;
                int len = y - start;
                Vector3 pos = new Vector3(x * cellSize, (wallHeight + VExtraH) * 0.5f, (start + len * 0.5f) * cellSize);
                // Vertical walls are a hair thicker, longer and taller than horizontal ones, so their faces
                // are never on the same plane as a horizontal wall (that caused flickering z-fighting at corners).
                Vector3 size = new Vector3(wallThickness + VExtraT, wallHeight + VExtraH, len * cellSize + wallThickness + VExtraT);
                MakeCube("Wall_V", walls, pos, size, mat);
            }
        }

        // Horizontal wall lines (they run along X).
        for (int y = 0; y <= height; y++)
        {
            int x = 0;
            while (x < width)
            {
                if (!hWall[x, y]) { x++; continue; }
                int start = x;
                while (x < width && hWall[x, y]) x++;
                int len = x - start;
                Vector3 pos = new Vector3((start + len * 0.5f) * cellSize, wallHeight * 0.5f, y * cellSize);
                Vector3 size = new Vector3(len * cellSize + wallThickness, wallHeight, wallThickness);
                MakeCube("Wall_H", walls, pos, size, mat);
            }
        }
    }

    // Creates a static cube (with its BoxCollider) under a parent.
private GameObject MakeCube(string objName, Transform parent, Vector3 localPos, Vector3 localScale, Material mat)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = objName;
        cube.transform.SetParent(parent, false);
        cube.transform.localPosition = localPos;
        cube.transform.localScale = localScale;
        cube.GetComponent<Renderer>().sharedMaterial = mat;

        // Texture tiles at the same size on every wall, however long it is.
        float tile = objName == "Floor" ? floorTileSize : (objName.StartsWith("Wall") ? wallTileSize : 0f);
        if (tile > 0f) ApplyWorldUV(cube, tile);

        SetStatic(cube);
        return cube;
    }

    // ---------------------------------------------------------------- surface look + details

    // Gives a cube UVs measured in metres, so a texture keeps the same size on short and long walls.
    private static void ApplyWorldUV(GameObject cube, float tile)
    {
        MeshFilter mf = cube.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null || tile <= 0.01f) return;

        Mesh m = mf.sharedMesh;
        if (m.name != "CubeUV") { m = Object.Instantiate(m); m.name = "CubeUV"; } // own copy, the shared cube stays untouched

        Vector3 s = cube.transform.localScale;
        Vector3 pos = cube.transform.localPosition;
        Vector2 off = new Vector2(pos.x * 0.173f, pos.z * 0.291f); // so neighbouring walls do not repeat exactly
        Vector3[] v = m.vertices;
        Vector3[] n = m.normals;
        Vector2[] uv = new Vector2[v.Length];
        for (int i = 0; i < v.Length; i++)
        {
            Vector3 p = Vector3.Scale(v[i], s);
            Vector2 t;
            if (Mathf.Abs(n[i].x) > 0.5f) t = new Vector2(p.z, p.y);
            else if (Mathf.Abs(n[i].y) > 0.5f) t = new Vector2(p.x, p.z);
            else t = new Vector2(p.x, p.y);
            uv[i] = t / tile + off;
        }
        m.uv = uv;
        mf.sharedMesh = m;
    }

    // Re-applies the materials, texture tiling and details to the maze that is already in the scene (the maze itself does not change).
    [ContextMenu("Refresh Look (keep this maze)")]
    public void RefreshLook()
    {
        Transform root = transform.Find(RootName);
        if (root == null)
        {
            Debug.LogWarning("MazeGenerator: there is no maze yet. Use 'Generate Maze' first.", this);
            return;
        }

        Transform floor = root.Find("Floor");
        if (floor != null)
        {
            floor.GetComponent<Renderer>().sharedMaterial = GetMaterial(floorMaterial, new Color(0.2f, 0.12f, 0.07f));
            ApplyWorldUV(floor.gameObject, floorTileSize);
        }

        Transform walls = root.Find("Walls");
        if (walls != null)
        {
            Material wm = GetMaterial(wallMaterial, new Color(0.07f, 0.17f, 0.09f));
            foreach (Transform w in walls)
            {
                w.GetComponent<Renderer>().sharedMaterial = wm;
                if (w.name == "Wall_V")
                {
                    // same fix for a maze that already exists (safe to run many times)
                    Vector3 sc = w.localScale;
                    float runs = Mathf.Round((sc.z - wallThickness) / cellSize);
                    w.localScale = new Vector3(wallThickness + VExtraT, wallHeight + VExtraH, runs * cellSize + wallThickness + VExtraT);
                    Vector3 lp = w.localPosition;
                    w.localPosition = new Vector3(lp.x, (wallHeight + VExtraH) * 0.5f, lp.z);
                    BoxCollider bc = w.GetComponent<BoxCollider>();
                    if (bc != null) { bc.size = Vector3.one; bc.center = Vector3.zero; }
                }
                ApplyWorldUV(w.gameObject, wallTileSize);
            }
        }

        BuildDetails(root, new System.Random(detailSeed));

#if UNITY_EDITOR
        if (!Application.isPlaying) EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
    }

    // Places grass/pebbles on the floor, ferns at the wall bottoms and bushes on the wall tops.
    private void BuildDetails(Transform root, System.Random rng)
    {
        Transform old = root.Find("Details");
        if (old != null)
        {
            if (Application.isPlaying) { old.name = "Details_old"; Destroy(old.gameObject); }
            else DestroyImmediate(old.gameObject);
        }

        Transform holder = new GameObject("Details").transform;
        holder.SetParent(root, false);
        Transform baseHolder = new GameObject("WallBase").transform; baseHolder.SetParent(holder, false);
        Transform topHolder = new GameObject("WallTop").transform; topHolder.SetParent(holder, false);
        Transform groundHolder = new GameObject("Ground").transform; groundHolder.SetParent(holder, false);

        Physics.SyncTransforms();

        // Keep the start cell and the chests clear.
        List<Vector3> keepClear = new List<Vector3> { CellCenter(new Vector2Int(0, 0)) };
        foreach (Transform t in root) if (t.name.StartsWith("MapChest")) keepClear.Add(t.localPosition);

        Transform walls = root.Find("Walls");
        if (walls != null)
        {
            foreach (Transform w in walls)
            {
                Vector3 s = w.localScale;
                bool alongX = s.x > s.z;
                float length = alongX ? s.x : s.z;
                float thick = alongX ? s.z : s.x;
                Vector3 axis = alongX ? Vector3.right : Vector3.forward;
                Vector3 side = alongX ? Vector3.forward : Vector3.right;

                if (wallBaseProps != null && wallBaseProps.Length > 0 && wallBaseSpacing > 0.5f)
                {
                    for (float d = -length * 0.5f + wallBaseSpacing * 0.5f; d < length * 0.5f; d += wallBaseSpacing * Mathf.Lerp(0.6f, 1.4f, (float)rng.NextDouble()))
                    {
                        float sign = rng.Next(2) == 0 ? -1f : 1f;
                        Vector3 p = w.localPosition + axis * d + side * sign * (thick * 0.5f + 0.2f + (float)rng.NextDouble() * 0.3f);
                        p.y = 0f;
                        if (IsNear(keepClear, p, 1.8f)) continue;
                        PlaceProp(wallBaseProps, baseHolder, p, 0.7f, 1.5f, rng);
                    }
                }

                if (wallTopProps != null && wallTopProps.Length > 0 && wallTopSpacing > 0.5f)
                {
                    for (float d = -length * 0.5f + wallTopSpacing * 0.5f; d < length * 0.5f; d += wallTopSpacing * Mathf.Lerp(0.7f, 1.3f, (float)rng.NextDouble()))
                    {
                        Vector3 p = w.localPosition + axis * d;
                        p.y = wallHeight - 0.15f;
                        PlaceProp(wallTopProps, topHolder, p, 1.4f, 2.4f, rng);
                    }
                }
            }
        }

        if (groundProps != null && groundProps.Length > 0 && groundPropCount > 0)
        {
            float maxX = width * cellSize, maxZ = height * cellSize;
            int placed = 0, tries = 0;
            while (placed < groundPropCount && tries < groundPropCount * 4)
            {
                tries++;
                Vector3 p = new Vector3((float)rng.NextDouble() * maxX, 0f, (float)rng.NextDouble() * maxZ);
                if (IsNear(keepClear, p, 1.8f)) continue;
                // skip spots inside walls
                if (Physics.CheckBox(transform.TransformPoint(p) + Vector3.up * 0.6f, new Vector3(0.35f, 0.4f, 0.35f))) continue;
                PlaceProp(groundProps, groundHolder, p, 0.35f, 0.9f, rng);
                placed++;
            }
        }
    }

    private static bool IsNear(List<Vector3> points, Vector3 p, float radius)
    {
        foreach (Vector3 q in points)
        {
            float dx = p.x - q.x, dz = p.z - q.z;
            if (dx * dx + dz * dz < radius * radius) return true;
        }
        return false;
    }

    // Spawns one random prop from the set, scaled so its biggest side is between minSize and maxSize metres.
    private void PlaceProp(GameObject[] set, Transform parent, Vector3 localPos, float minSize, float maxSize, System.Random rng)
    {
        GameObject prefab = set[rng.Next(set.Length)];
        if (prefab == null) return;

        GameObject go = SpawnPrefab(prefab, parent);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        Renderer[] rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length > 0)
        {
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            float ext = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            if (ext > 0.001f)
                go.transform.localScale = Vector3.one * (Mathf.Lerp(minSize, maxSize, (float)rng.NextDouble()) / ext);
        }
        go.transform.localRotation = Quaternion.Euler(0f, (float)(rng.NextDouble() * 360.0), 0f);

        foreach (Collider c in go.GetComponentsInChildren<Collider>())
        {
            if (Application.isPlaying) Destroy(c); else DestroyImmediate(c);
        }
        SetStatic(go);
    }

    // Uses the assigned material, or makes a plain dark URP Lit one.
    private Material GetMaterial(Material assigned, Color fallbackColor)
    {
        if (assigned != null) return assigned;
        Material m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.SetColor("_BaseColor", fallbackColor);
        return m;
    }

    private static void SetStatic(GameObject go)
    {
#if UNITY_EDITOR
        // Static flags only exist in the Editor (used for batching / baking).
        foreach (Transform t in go.GetComponentsInChildren<Transform>()) t.gameObject.isStatic = true;
#endif
    }

    // ---------------------------------------------------------------- chests

    // Puts the chests in dead-end cells that are far (walking distance) from the start.
    private List<Vector2Int> PlaceChests(Transform parent, int[,] dist)
    {
        List<Vector2Int> chosen = new List<Vector2Int>();
        if (chestPrefab == null)
        {
            Debug.LogWarning("MazeGenerator: no chest prefab assigned, no chests spawned.", this);
            return chosen;
        }

        // Candidates: dead ends first (farthest first), then every other cell as a fallback.
        List<Vector2Int> deadEnds = new List<Vector2Int>();
        List<Vector2Int> others = new List<Vector2Int>();
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (x == 0 && y == 0) continue;
                Vector2Int c = new Vector2Int(x, y);
                if (CountOpenSides(c) == 1) deadEnds.Add(c); else others.Add(c);
            }
        }
        System.Comparison<Vector2Int> farthestFirst = (a, b) => dist[b.x, b.y].CompareTo(dist[a.x, a.y]);
        deadEnds.Sort(farthestFirst);
        others.Sort(farthestFirst);
        List<Vector2Int> candidates = new List<Vector2Int>(deadEnds);
        candidates.AddRange(others);

        // Pick the farthest cells while keeping the chests apart; relax the spacing if needed.
        for (int spacing = chestMinSpacing; spacing >= 0 && chosen.Count < chestCount; spacing -= 2)
        {
            chosen.Clear();
            foreach (Vector2Int c in candidates)
            {
                if (chosen.Count >= chestCount) break;
                bool farEnough = true;
                foreach (Vector2Int other in chosen)
                    if (Mathf.Abs(c.x - other.x) + Mathf.Abs(c.y - other.y) < spacing) { farEnough = false; break; }
                if (farEnough) chosen.Add(c);
            }
        }

        for (int i = 0; i < chosen.Count; i++)
        {
            GameObject chest = SpawnPrefab(chestPrefab, parent);
            chest.name = "MapChest_" + (i + 1);
            chest.transform.localPosition = CellCenter(chosen[i]);

            // The chest front (+Z) faces the open side of its dead end.
            Vector3 facing = Vector3.forward;
            foreach (Vector2Int d in Dirs)
                if (IsOpen(chosen[i], d)) { facing = new Vector3(d.x, 0f, d.y); break; }
            chest.transform.localRotation = Quaternion.LookRotation(facing);
        }
        return chosen;
    }

    private int CountOpenSides(Vector2Int c)
    {
        int count = 0;
        foreach (Vector2Int d in Dirs) if (IsOpen(c, d)) count++;
        return count;
    }

    // ---------------------------------------------------------------- decorations

    // Scatters trees and rocks in cell corners so the middle of every path stays open.
    private void PlaceDecorations(Transform parent, System.Random rng, List<Vector2Int> chestCells)
    {
        if (decorations == null || decorations.Length == 0 || decorationCount <= 0) return;

        Transform holder = new GameObject("Decorations").transform;
        holder.SetParent(parent, false);

        HashSet<Vector2Int> blocked = new HashSet<Vector2Int>(chestCells) { new Vector2Int(0, 0) }; // keep start + chests clear
        float inset = cellSize * 0.5f - wallThickness * 0.5f - 0.25f; // distance from cell centre to the corner spot

        for (int i = 0; i < decorationCount; i++)
        {
            GameObject prefab = decorations[rng.Next(decorations.Length)];
            if (prefab == null) continue;

            Vector2Int cell = new Vector2Int(rng.Next(width), rng.Next(height));
            if (blocked.Contains(cell)) continue;

            float signX = rng.Next(2) == 0 ? -1f : 1f;
            float signZ = rng.Next(2) == 0 ? -1f : 1f;

            GameObject deco = SpawnPrefab(prefab, holder);
            deco.transform.localPosition = CellCenter(cell) + new Vector3(signX * inset, 0f, signZ * inset);
            deco.transform.localRotation = Quaternion.Euler(0f, (float)(rng.NextDouble() * 360.0), 0f);
            deco.transform.localScale = Vector3.one * decorationScale * Mathf.Lerp(0.8f, 1.3f, (float)rng.NextDouble());
            SetStatic(deco);
        }
    }

    // Instantiates a prefab. In Edit mode the prefab link is kept.
    private GameObject SpawnPrefab(GameObject prefab, Transform parent)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying) return (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
#endif
        return Instantiate(prefab, parent);
    }

    // ---------------------------------------------------------------- player

    // Teleports the player to the middle of the start cell (0,0).
    private void MovePlayerToStart()
    {
        Transform p = player;
        if (p == null)
        {
            GameObject found = GameObject.FindGameObjectWithTag("Player");
            if (found != null) p = found.transform;
        }
        if (p == null)
        {
            Debug.LogWarning("MazeGenerator: no player found (assign one or tag it 'Player').", this);
            return;
        }

        Vector3 pos = transform.TransformPoint(CellCenter(new Vector2Int(0, 0)));
        pos.y = transform.position.y + playerSpawnHeight;

        // A CharacterController overrides transform changes, so switch it off while teleporting.
        CharacterController cc = p.GetComponent<CharacterController>();
        bool wasEnabled = cc != null && cc.enabled;
        if (cc != null) cc.enabled = false;

        p.position = pos;

        if (cc != null) cc.enabled = wasEnabled;
        Physics.SyncTransforms();

#if UNITY_EDITOR
        if (!Application.isPlaying) EditorUtility.SetDirty(p);
#endif
    }
}
