using UnityEngine;

public enum EnemyBiome { Any, Forest, Dungeon }

/// <summary>
/// All numbers of one enemy type, so every type can be tuned in the Inspector without touching code.
/// Create: right click in the Project > Create > Mazura > Enemy Data.
/// </summary>
[CreateAssetMenu(menuName = "Mazura/Enemy Data", fileName = "EnemyData")]
public class EnemyData : ScriptableObject
{
    [Header("Identity")]
    public string displayName = "Enemy";
    public EnemyBiome biome = EnemyBiome.Any;
    [Tooltip("Only exists at night (despawns at dawn even if it is a day enemy otherwise).")]
    public bool nightOnly = false;
    public bool isBoss = false;

    [Header("Movement")]
    public float walkSpeed = 1.6f;
    public float runSpeed = 4.2f;
    public float turnSpeed = 360f;
    public float acceleration = 10f;
    [Tooltip("How far it wanders from where it is.")]
    public float wanderRadius = 10f;
    public Vector2 idleTime = new Vector2(1.5f, 4f);
    [Tooltip("Walks a loop of waypoints instead of wandering (Dungeon Guard).")]
    public bool patrols = false;

    [Header("Senses")]
    public bool canSee = true;
    public float sightRange = 14f;
    [Range(10f, 360f)] public float sightAngle = 110f;
    [Tooltip("Player noise is heard inside this radius (running = full radius, walking = less, standing = nothing).")]
    public float hearingRange = 9f;
    [Tooltip("Seconds without seeing or hearing the player before it gives up the hunt.")]
    public float loseTime = 5f;
    [Tooltip("When it spots the player, enemies inside this radius are alerted too (0 = off).")]
    public float alertRadius = 0f;

    [Header("Combat")]
    public float maxHealth = 60f;
    public float damage = 12f;
    public float attackRange = 1.7f;
    [Tooltip("Telegraph time before the hit lands, so the player can react.")]
    public float attackWindup = 0.55f;
    public float attackCooldown = 1.6f;
    [Tooltip("Lunges forward when it strikes (Crawler).")]
    public float lungeDistance = 0f;
    [Range(0f, 1f)] public float knockbackResist = 0f;
    [Tooltip("Boss: radius of the ground slam (0 = normal hit).")]
    public float slamRadius = 0f;

    [Header("Look")]
    public float scale = 1f;
    public Color tint = Color.white;
    [Range(0f, 1f)] public float opacity = 1f;
    [ColorUsage(false, true)] public Color glowColor = new Color(1f, 0.15f, 0.1f);
    public bool smoke = false;
    [Tooltip("Moves on all fours with the crawl animation (Forest Crawler).")]
    public bool crawls = false;
    [Tooltip("Shadow Wraith: heartbeat gets louder inside this distance (0 = none).")]
    public float heartbeatRange = 0f;
    [Tooltip("Will not come near a torch / campfire light: it backs away from it (Shadow Wraith).")]
    public bool fearsLight = false;

    [Header("Loot")]
    [Range(0f, 1f)] public float lootChance = 0.35f;
    public ItemType lootItem = ItemType.Stone;
    public Vector2Int lootAmount = new Vector2Int(1, 2);

    [Header("Sounds (empty = generated)")]
    public AudioClip attackSound;
    public AudioClip hurtSound;
    public AudioClip deathSound;
    public AudioClip alertSound;
}
