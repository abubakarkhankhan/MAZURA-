using UnityEngine;

/// <summary>
/// Static helper that gives every system (enemies, spawners, managers, UI...)
/// one cached way to find the player. Nothing is tied to a specific player object:
/// whatever active object is tagged "Player" is the player.
///
/// Usage:
///   Transform p = PlayerRef.Transform;          // null if there is no player yet
///   float d = Vector3.Distance(transform.position, PlayerRef.Position);
///   if (PlayerRef.Exists) { ... }
/// </summary>
public static class PlayerRef
{
    public const string PlayerTag = "Player";

    private static Transform cached;

    /// <summary>The player's transform, found once through the "Player" tag and then cached.</summary>
    public static Transform Transform
    {
        get
        {
            // "cached == null" is also true when the old player was destroyed, so we search again.
            if (cached == null)
            {
                GameObject found = GameObject.FindGameObjectWithTag(PlayerTag);
                cached = found != null ? found.transform : null;
            }
            return cached;
        }
    }

    /// <summary>The player's GameObject (null if none).</summary>
    public static GameObject GameObject
    {
        get { return Transform != null ? Transform.gameObject : null; }
    }

    /// <summary>True when an active object tagged "Player" exists.</summary>
    public static bool Exists
    {
        get { return Transform != null; }
    }

    /// <summary>The player's world position (Vector3.zero if there is no player).</summary>
    public static Vector3 Position
    {
        get { return Transform != null ? Transform.position : Vector3.zero; }
    }

    /// <summary>Forget the cached player (call this if you swap the player at runtime).</summary>
    public static void Refresh()
    {
        cached = null;
    }

    // Keeps the cache clean when "Enter Play Mode" runs without a domain reload.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        cached = null;
    }
}
