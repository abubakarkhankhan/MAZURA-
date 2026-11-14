using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Builds a patrol loop for the Dungeon Guard along the maze corridors.
/// Points are random spots on the NavMesh around 'origin' that are really reachable (path check),
/// then ordered by angle so the guard walks a loop instead of zig-zagging.
/// </summary>
public static class EnemyWaypoints
{
    public static List<Vector3> Loop(Vector3 origin, int count = 5, float radius = 18f, float minSpacing = 5f)
    {
        List<Vector3> pts = new List<Vector3>();
        NavMeshPath path = new NavMeshPath();
        int tries = count * 30;
        while (pts.Count < count && tries-- > 0)
        {
            Vector2 r = Random.insideUnitCircle * radius;
            Vector3 probe = origin + new Vector3(r.x, 0f, r.y);
            if (!NavMesh.SamplePosition(probe, out NavMeshHit hit, 3f, NavMesh.AllAreas)) continue;
            if (!NavMesh.CalculatePath(origin, hit.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) continue;
            bool tooClose = false;
            for (int i = 0; i < pts.Count; i++) if ((pts[i] - hit.position).sqrMagnitude < minSpacing * minSpacing) { tooClose = true; break; }
            if (!tooClose) pts.Add(hit.position);
        }
        pts.Sort((a, b) => Mathf.Atan2(a.z - origin.z, a.x - origin.x).CompareTo(Mathf.Atan2(b.z - origin.z, b.x - origin.x)));
        return pts;
    }
}
