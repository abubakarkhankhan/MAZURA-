using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Finds a new free spot for a tree or stone that grows back, with the same rules as the maze layout:
///   stones only next to a wall, trees next to a wall or in the middle of a corridor,
///   never next to each other, never close to the player, the chests or the start.
/// Used by ResourceNode when "Respawn Elsewhere" is on.
/// </summary>
public static class ResourcePlacement
{
    static readonly List<Bounds> walls = new List<Bounds>();
    static readonly List<Vector3> chests = new List<Vector3>();
    static float cell = 4f;
    static int cellsX = 25, cellsZ = 25;
    static readonly Vector3 start = new Vector3(2f, 0f, 2f);
    static MazeGenerator cachedFor;
    static bool ready;

    /// <summary>How far from the player a node may re-appear (so nothing pops up in front of you).</summary>
    public const float MinPlayerDistance = 10f;

    static void Prepare()
    {
        MazeGenerator mg = Object.FindAnyObjectByType<MazeGenerator>();
        if (mg == null) { ready = false; return; }
        if (ready && cachedFor == mg) return;

        cachedFor = mg;
        walls.Clear();
        chests.Clear();
        cellsX = ReadInt(mg, "width", 25);
        cellsZ = ReadInt(mg, "height", 25);
        cell = ReadFloat(mg, "cellSize", 4f);

        Transform root = mg.transform.Find("GeneratedMaze");
        if (root != null)
        {
            Transform w = root.Find("Walls");
            if (w != null)
                foreach (Transform t in w)
                {
                    Collider c = t.GetComponent<Collider>();
                    if (c != null) walls.Add(c.bounds);
                }
            foreach (Transform t in root)
                if (t.name.StartsWith("MapChest")) chests.Add(t.position);
        }
        ready = walls.Count > 0;
    }

    /// <summary>Distance on the ground from a point to the nearest wall surface.</summary>
    public static float WallDistance(float x, float z)
    {
        float best = float.MaxValue;
        for (int i = 0; i < walls.Count; i++)
        {
            Bounds b = walls[i];
            float dx = Mathf.Max(Mathf.Max(b.min.x - x, 0f), x - b.max.x);
            float dz = Mathf.Max(Mathf.Max(b.min.z - z, 0f), z - b.max.z);
            float d = Mathf.Sqrt(dx * dx + dz * dz);
            if (d < best) best = d;
        }
        return best;
    }

    public static bool TryFindSpot(ResourceNode self, bool isRock, bool middleTree, Vector3 playerPos, out Vector3 spot)
    {
        spot = Vector3.zero;
        Prepare();
        if (!ready) return false;

        for (int tries = 0; tries < 250; tries++)
        {
            int cx = Random.Range(0, cellsX), cz = Random.Range(0, cellsZ);
            float mx = cx * cell + cell * 0.5f, mz = cz * cell + cell * 0.5f;
            float ox = Random.Range(-1.7f, 1.7f), oz = Random.Range(-1.7f, 1.7f);
            if (middleTree) { ox *= 0.3f; oz *= 0.3f; }
            Vector3 q = new Vector3(mx + ox, 0f, mz + oz);

            float d = WallDistance(q.x, q.z);
            if (isRock) { if (d < 0.8f || d > 1.15f) continue; }          // stones: right next to a wall
            else if (middleTree) { if (d < 1.45f) continue; }             // some trees in the middle of the path
            else { if (d < 1.25f || d > 1.8f) continue; }                 // other trees: along the wall

            if (Flat(q, playerPos) < MinPlayerDistance) continue;
            if (Flat(q, start) < 5.5f) continue;

            bool ok = true;
            for (int i = 0; i < chests.Count; i++) if (Flat(q, chests[i]) < 3f) { ok = false; break; }
            if (!ok) continue;

            IReadOnlyList<ResourceNode> all = ResourceNode.All;
            for (int i = 0; i < all.Count; i++)
            {
                ResourceNode o = all[i];
                if (o == null || o == self) continue;
                bool otherIsRock = o.nodeType == NodeType.Stone;
                // a tree and a stone never stand together
                float need = isRock ? (otherIsRock ? 2.0f : 3.2f) : (otherIsRock ? 3.2f : 2.4f);
                if (Flat(q, o.transform.position) < need) { ok = false; break; }
            }
            if (!ok) continue;

            spot = q;
            return true;
        }
        return false;
    }

    static float Flat(Vector3 a, Vector3 b)
    {
        a.y = 0f; b.y = 0f;
        return (a - b).magnitude;
    }

    static int ReadInt(object o, string name, int fallback)
    {
        FieldInfo f = o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        return f != null && f.FieldType == typeof(int) ? (int)f.GetValue(o) : fallback;
    }

    static float ReadFloat(object o, string name, float fallback)
    {
        FieldInfo f = o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        return f != null && f.FieldType == typeof(float) ? (float)f.GetValue(o) : fallback;
    }
}
