using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Tools > Mazura > Bake NavMesh
/// Bakes the walkable area of the maze for the enemies: floor + walls only.
/// Trees and stones are left out of the bake and cut holes into it at run time (NavMeshObstacle, carve),
/// so paths stay right when a tree falls or grows back somewhere else.
/// </summary>
public static class MazuraNavMeshBaker
{
    [MenuItem("Tools/Mazura/Bake NavMesh")]
    public static void Bake()
    {
        if (Application.isPlaying) { Debug.LogError("Stop Play mode first."); return; }
        GameObject mg = GameObject.Find("MazeGenerator");
        Transform root = mg != null ? mg.transform.Find("GeneratedMaze") : null;
        if (root == null) { Debug.LogError("MazuraNavMeshBaker: GeneratedMaze not found."); return; }

        // 1) surface on the maze root, only its children, from the colliders (not the render meshes)
        NavMeshSurface surface = root.GetComponent<NavMeshSurface>();
        if (surface == null) surface = Undo.AddComponent<NavMeshSurface>(root.gameObject);
        surface.collectObjects = CollectObjects.Children;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.layerMask = ~0;
        surface.agentTypeID = 0;               // Humanoid

        // 2) agent: the default Humanoid (radius 0.5) fits the 1.3 m gaps beside the middle trees

        // 3) leave decorations and details out of the bake
        IgnoreChild(root, "Decorations");
        IgnoreChild(root, "Details");

        // 4) every tree / stone gets a carving obstacle
        int obstacles = 0;
        foreach (ResourceNode n in root.GetComponentsInChildren<ResourceNode>(true))
        {
            if (AddObstacle(n)) obstacles++;
        }

        surface.BuildNavMesh();
        EditorUtility.SetDirty(surface);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        Debug.Log("MazuraNavMeshBaker: NavMesh baked, " + obstacles + " carving obstacles.");
    }

    static void IgnoreChild(Transform root, string name)
    {
        Transform t = root.Find(name);
        if (t == null) return;
        NavMeshModifier m = t.GetComponent<NavMeshModifier>();
        if (m == null) m = Undo.AddComponent<NavMeshModifier>(t.gameObject);
        m.ignoreFromBuild = true;
        m.applyToChildren = true;
    }

    static bool AddObstacle(ResourceNode n)
    {
        NavMeshObstacle o = n.GetComponent<NavMeshObstacle>();
        if (o == null) o = Undo.AddComponent<NavMeshObstacle>(n.gameObject);
        o.carving = true;
        o.carveOnlyStationary = true;
        Vector3 ls = n.transform.lossyScale;
        float sx = Mathf.Max(0.0001f, Mathf.Abs(ls.x)), sy = Mathf.Max(0.0001f, Mathf.Abs(ls.y));
        CapsuleCollider cap = n.GetComponent<CapsuleCollider>();
        if (cap != null)
        {
            o.shape = NavMeshObstacleShape.Capsule;          // the trunk only, branches do not block
            o.center = cap.center;
            o.radius = cap.radius;
            o.height = cap.height;
        }
        else
        {
            Collider c = n.GetComponentInChildren<Collider>();
            if (c == null) return false;
            Bounds b = c.bounds;
            o.shape = NavMeshObstacleShape.Box;
            o.center = n.transform.InverseTransformPoint(b.center);
            o.size = new Vector3(b.size.x / sx, b.size.y / sy, b.size.z / sx) * 0.9f;
        }
        EditorUtility.SetDirty(o);
        return true;
    }
}
