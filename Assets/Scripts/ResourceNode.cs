using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public enum NodeType { Tree, Stone }

/// <summary>One line of a drop table: item, amount range and chance (0-1).</summary>
[Serializable]
public class ResourceDrop
{
    public ItemType item = ItemType.Wood;
    public int minAmount = 1;
    public int maxAmount = 1;
    [Range(0f, 1f)] public float chance = 1f;
}

/// <summary>
/// Put on a tree or rock. Listens to ToolController.OnToolHit.
/// A hit only counts if the player holds the right tool and the node is within
/// 2.5 m in front of the player. When the last hit lands, the drops go into the
/// Inventory, the node disappears and comes back after respawnTime seconds.
/// </summary>
public class ResourceNode : MonoBehaviour
{
    public NodeType nodeType = NodeType.Tree;
    public ToolType requiredTool = ToolType.Axe;
    [Min(1)] public int hitsRequired = 4;
    public List<ResourceDrop> drops = new List<ResourceDrop>();
    [Tooltip("Seconds until the node is back after it was depleted.")]
    public float respawnTime = 90f;
    [Tooltip("When the node grows back it appears at a NEW free spot (stones next to walls, trees along walls or mid-corridor) instead of the same place.")]
    public bool respawnElsewhere = true;
    [Tooltip("Minecraft style: items fall out and must be picked up by walking to them.")]
    public bool dropAsPickups = true;

    [Header("Hit feedback")]
    [Tooltip("Scale punch on every hit (0 = off).")]
    public float punchScale = 0.06f;
    public float punchDuration = 0.15f;
    [Tooltip("Hook up a particle or a sound here.")]
    public UnityEvent onHit;
    public UnityEvent onDepleted;

    public const float HitRange = 1.6f;   // metres from the player to the node surface (was 2.5, felt too far)
    const float MinFacingDot = 0.55f;     // node must be in front of the player (about +-56 degrees)

    static readonly List<ResourceNode> allNodes = new List<ResourceNode>();

    /// <summary>All resource nodes that are in the scene right now.</summary>
    public static IReadOnlyList<ResourceNode> All { get { return allNodes; } }

    /// <summary>Raised on every valid hit (node, was it the last hit, hit position).</summary>
    public static event Action<ResourceNode, bool, Vector3> OnAnyNodeHit;
    /// <summary>Raised when a falling tree touches the ground (node, ground position).</summary>
    public static event Action<ResourceNode, Vector3> OnTreeLanded;

    [Header("Falling tree")]
    [Tooltip("Seconds a tree takes to fall over after the last hit (0 = it just disappears).")]
    public float fallDuration = 1.1f;

    Vector3 hitPoint, hitFromPos;
    Quaternion baseLocalRot;

    int hits;
    bool depleted;
    Vector3 baseScale;
    Collider[] colliders;
    Renderer[] renderers;
    Coroutine punchRoutine;

    void Awake()
    {
        baseScale = transform.localScale;
        baseLocalRot = transform.localRotation;
        colliders = GetComponentsInChildren<Collider>();
        renderers = GetComponentsInChildren<Renderer>();
    }

    void OnEnable()
    {
        allNodes.Add(this);
        // remove first so the handler is never added twice
        ToolController.OnToolHit -= HandleToolHit;
        ToolController.OnToolHit += HandleToolHit;
    }

    void OnDisable()
    {
        allNodes.Remove(this);
    }

    /// <summary>Called once per swing: finds the nearest valid node in front of the player.</summary>
    static void HandleToolHit(ToolType tool)
    {
        ToolController player = ToolController.Instance;
        if (player == null) return;

        Vector3 pos = player.transform.position;
        Vector3 fwd = player.transform.forward;
        fwd.y = 0f;
        fwd.Normalize();

        ResourceNode best = null;
        float bestDist = float.MaxValue;
        ResourceNode other = null;          // nearest node that needs a different tool
        float otherDist = float.MaxValue;

        for (int i = 0; i < allNodes.Count; i++)
        {
            ResourceNode n = allNodes[i];
            if (n == null || n.depleted) continue;

            Vector3 to = n.ClosestPointTo(pos) - pos;
            to.y = 0f;
            float d = to.magnitude;
            if (d > HitRange) continue;
            if (d > 0.01f && Vector3.Dot(fwd, to / d) < MinFacingDot) continue;

            // a node that matches the equipped tool always wins, even if another node is a bit closer
            if (n.requiredTool == tool)
            {
                if (d < bestDist) { bestDist = d; best = n; }
            }
            else if (d < otherDist) { otherDist = d; other = n; }
        }

        if (best == null)
        {
            // nothing for this tool in reach: tell the player what the nearby node needs
            if (other != null) HudMessage.Show(other.requiredTool == ToolType.Axe ? "Need an axe" : "Need a pickaxe");
            return;
        }

        if (tool != best.requiredTool)
        {
            HudMessage.Show(best.requiredTool == ToolType.Axe ? "Need an axe" : "Need a pickaxe");
            return;
        }

        best.hitFromPos = pos;
        best.hitPoint = best.ClosestPointTo(pos);
        if (best.nodeType == NodeType.Tree) best.hitPoint.y = pos.y + 1.2f;
        best.ApplyHit();
    }

    Vector3 ClosestPointTo(Vector3 point)
    {
        bool found = false;
        Vector3 best = transform.position;
        float bestSqr = float.MaxValue;
        foreach (Collider c in colliders)
        {
            if (c == null || !c.enabled) continue;
            Vector3 p = c.ClosestPoint(point);
            float sqr = (p - point).sqrMagnitude;
            if (sqr < bestSqr) { bestSqr = sqr; best = p; found = true; }
        }
        return found ? best : transform.position;
    }

    void ApplyHit()
    {
        hits += CraftingState.ToolPower(requiredTool);   // upgraded tools count as several hits
        bool last = hits >= hitsRequired;
        Punch();
        onHit?.Invoke();
        OnAnyNodeHit?.Invoke(this, last, hitPoint);
        if (last) Deplete();
    }

    void Deplete()
    {
        depleted = true;
        onDepleted?.Invoke();
        if (nodeType == NodeType.Tree && fallDuration > 0f)
        {
            StartCoroutine(FallRoutine());   // the tree falls over first, the wood drops when it lands
        }
        else
        {
            GiveDrops();
            SetVisible(false);
            StartCoroutine(RespawnAfterDelay());
        }
    }

    IEnumerator FallRoutine()
    {
        foreach (Collider c in colliders) if (c != null) c.enabled = false;
        UnityEngine.AI.NavMeshObstacle fallObstacle = GetComponent<UnityEngine.AI.NavMeshObstacle>();
        if (fallObstacle != null) fallObstacle.enabled = false;

        // the tree falls away from the player
        Vector3 away = transform.position - hitFromPos;
        away.y = 0f;
        if (away.sqrMagnitude < 0.01f) away = transform.forward;
        away.Normalize();
        away = ClearFallDirection(away);        // never fall into or through a wall
        Vector3 axis = Vector3.Cross(Vector3.up, away);

        float height = 4f;
        if (renderers.Length > 0)
        {
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            height = b.size.y;
        }

        Quaternion start = transform.rotation;
        float t = 0f;
        while (t < fallDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / fallDuration);
            transform.rotation = Quaternion.AngleAxis(86f * k * k, axis) * start; // slow start, fast finish
            yield return null;
        }
        transform.rotation = Quaternion.AngleAxis(86f, axis) * start;
        OnTreeLanded?.Invoke(this, transform.position + away * (height * 0.5f));

        yield return new WaitForSeconds(0.8f);   // lies on the ground for a moment
        GiveDrops();
        SetVisible(false);
        transform.localRotation = baseLocalRot;
        StartCoroutine(RespawnAfterDelay());
    }

    void GiveDrops()
    {
        if (Inventory.Instance == null)
        {
            Debug.LogWarning("ResourceNode: no Inventory in the scene, drops are lost.", this);
            return;
        }
        foreach (ResourceDrop d in drops)
        {
            if (UnityEngine.Random.value > d.chance) continue;
            int amount = UnityEngine.Random.Range(d.minAmount, Mathf.Max(d.minAmount, d.maxAmount) + 1);
            if (amount <= 0) continue;
            if (dropAsPickups) SpawnPickup(d.item, amount);
            else Inventory.Instance.Add(d.item, amount);
        }
    }

    // Picks the fall direction closest to 'wanted' that has no maze wall in the way for the tree's height.
    Vector3 ClearFallDirection(Vector3 wanted)
    {
        float height = 4f;
        if (renderers.Length > 0)
        {
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            height = b.size.y;
        }
        float reach = Mathf.Max(1.5f, height * 0.85f);
        Vector3 origin = transform.position + Vector3.up * 0.8f;
        float[] tryAngles = { 0f, 35f, -35f, 70f, -70f, 105f, -105f, 140f, -140f, 180f };
        Vector3 bestDir = wanted;
        float bestFree = -1f;
        foreach (float ang in tryAngles)
        {
            Vector3 d = Quaternion.AngleAxis(ang, Vector3.up) * wanted;
            float free = WallFreeDistance(origin, d, reach);
            if (free >= reach) return d;                      // completely free: take it
            if (free > bestFree) { bestFree = free; bestDir = d; }
        }
        return bestDir;                                       // no fully free side: the most open one
    }

    static float WallFreeDistance(Vector3 origin, Vector3 dir, float reach)
    {
        float best = reach;
        RaycastHit[] hits = Physics.SphereCastAll(origin, 0.25f, dir, reach, ~0, QueryTriggerInteraction.Ignore);
        foreach (RaycastHit h in hits)
        {
            if (!IsWall(h.collider)) continue;
            if (h.distance < best) best = h.distance;
        }
        return best;
    }

    public static bool IsWall(Collider c)
    {
        if (c == null) return false;
        Transform t = c.transform;
        return t.name.StartsWith("Wall") || (t.parent != null && t.parent.name == "Walls");
    }

    void SpawnPickup(ItemType item, int amount)
    {
        // drop point: the middle of what is visible (a fallen tree lies on the ground, a rock stands)
        Vector3 pos = transform.position + Vector3.up;
        bool any = false;
        Bounds b = new Bounds(pos, Vector3.zero);
        foreach (Renderer r in renderers)
        {
            if (r == null || !r.enabled) continue;
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
        }
        if (any) pos = new Vector3(b.center.x, Mathf.Min(b.max.y, transform.position.y + 1.2f), b.center.z);

        Material stoneMat = null, gemMat = null;
        Mesh gemMesh = null;
        Renderer own = GetComponent<Renderer>();
        if (own != null) stoneMat = own.sharedMaterial;
        foreach (Transform c in transform)
        {
            if (!c.name.StartsWith("Ore") && !c.name.StartsWith("Crystal")) continue;
            Renderer cr = c.GetComponent<Renderer>();
            if (cr != null) gemMat = cr.sharedMaterial;
            MeshFilter mf = c.GetComponent<MeshFilter>();
            SkinnedMeshRenderer smr = c.GetComponent<SkinnedMeshRenderer>();
            gemMesh = mf != null ? mf.sharedMesh : (smr != null ? smr.sharedMesh : null);
            break;
        }
        Vector3 towardsPlayer = Vector3.zero;
        ToolController pl = ToolController.Instance;
        if (pl != null)
        {
            Vector3 eye = pl.transform.position + Vector3.up * 1.0f;
            Vector3 d = eye - pos;
            towardsPlayer = new Vector3(d.x, 0f, d.z).normalized;
            // if a wall is between the player and the drop point, drop it on the player's side of that wall
            RaycastHit[] hits = Physics.RaycastAll(eye, -d.normalized, d.magnitude, ~0, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            foreach (RaycastHit h in hits)
                if (IsWall(h.collider) && h.distance < nearest) { nearest = h.distance; pos = h.point + d.normalized * 0.45f; pos.y = Mathf.Max(pos.y, transform.position.y + 0.6f); }
        }
        WorldPickup.Spawn(item, amount, pos, stoneMat, item == ItemType.Stone ? null : gemMat, gemMesh, towardsPlayer);
    }

    IEnumerator RespawnAfterDelay()
    {
        yield return new WaitForSeconds(respawnTime);
        if (respawnElsewhere) MoveToNewSpot();
        hits = 0;
        depleted = false;
        transform.localScale = baseScale;
        SetVisible(true);
    }

    // Grows back somewhere else, so the forest never runs empty and the same tree is not chopped again and again.
    void MoveToNewSpot()
    {
        ToolController pl = ToolController.Instance;
        Vector3 playerPos = pl != null ? pl.transform.position : Vector3.zero;
        bool middleTree = nodeType == NodeType.Tree && UnityEngine.Random.value < 0.4f;
        Vector3 spot;
        if (ResourcePlacement.TryFindSpot(this, nodeType == NodeType.Stone, middleTree, playerPos, out spot))
        {
            transform.position = new Vector3(spot.x, transform.position.y, spot.z);
            transform.rotation = Quaternion.Euler(0f, UnityEngine.Random.value * 360f, 0f);
            baseLocalRot = transform.localRotation;
        }
        // no free spot found: it simply grows back where it was
    }

    void SetVisible(bool visible)
    {
        foreach (Renderer r in renderers) if (r != null) r.enabled = visible;
        foreach (Collider c in colliders) if (c != null) c.enabled = visible;
        UnityEngine.AI.NavMeshObstacle obstacle = GetComponent<UnityEngine.AI.NavMeshObstacle>();
        if (obstacle != null) obstacle.enabled = visible;   // enemies can walk where the tree stood
    }

    void Punch()
    {
        if (punchScale <= 0f) return;
        if (punchRoutine != null) StopCoroutine(punchRoutine);
        punchRoutine = StartCoroutine(PunchRoutine());
    }

    IEnumerator PunchRoutine()
    {
        float t = 0f;
        while (t < punchDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / punchDuration);
            transform.localScale = baseScale * (1f + punchScale * Mathf.Sin(k * Mathf.PI));
            yield return null;
        }
        transform.localScale = baseScale;
        punchRoutine = null;
    }
}
