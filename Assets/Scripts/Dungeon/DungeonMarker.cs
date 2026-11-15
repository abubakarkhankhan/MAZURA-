using UnityEngine;

/// <summary>Named spots of the dungeon (where the player arrives, where the boss rises...).</summary>
public class DungeonMarker : MonoBehaviour
{
    public enum Kind { DungeonStart, BossSpawn, Portal }
    public Kind kind;

    void OnDrawGizmos()
    {
        Gizmos.color = kind == Kind.DungeonStart ? Color.green : kind == Kind.BossSpawn ? Color.red : Color.magenta;
        Gizmos.DrawWireSphere(transform.position + Vector3.up, 0.6f);
        Gizmos.DrawLine(transform.position + Vector3.up, transform.position + Vector3.up + transform.forward * 1.5f);
    }
}
