using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Spawns enemies around the player, out of sight, on the NavMesh.
///   Day:   a few calm enemies (dayMax).
///   Night: "THE NIGHT HAS COME" - a first wave at once, then more creep in every few seconds up to nightMax.
///          Night-only enemies (Shadow Wraith) can appear.
///   Dawn:  "DAWN" - every enemy that came with the night melts away (as soon as you are not looking,
///          or after a short grace time). Day enemies stay.
/// Enemies far behind you (and not hunting) are recycled, so the maze stays alive around the player
/// and the total never goes over hardCap (keeps FPS high).
/// Put it on one object in the scene and fill the 'types' list (Tools/Mazura/Build Enemy Types made the prefabs).
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    [System.Serializable]
    public class SpawnType
    {
        public GameObject prefab;
        [Tooltip("Chance weight during the day (0 = never by day).")]
        public float dayWeight = 1f;
        [Tooltip("Chance weight at night (0 = never at night).")]
        public float nightWeight = 1f;
    }

    public static EnemySpawner Instance { get; private set; }

    public List<SpawnType> types = new List<SpawnType>();

    [Header("How many")]
    public int dayMax = 3;
    public int nightMax = 8;
    [Tooltip("Never more enemies than this alive at once.")]
    public int hardCap = 10;
    [Tooltip("Enemies spawned the moment night falls.")]
    public int nightFirstWave = 4;
    [Tooltip("Seconds between spawns while under the limit.")]
    public float dayInterval = 25f;
    public float nightInterval = 9f;

    [Header("Where")]
    public float minDistance = 16f;
    public float maxDistance = 36f;
    [Tooltip("Not-hunting enemies further than this from the player are recycled.")]
    public float recycleDistance = 70f;

    [Header("Boss")]
    [Tooltip("Boss that rises once every night (empty = no boss).")]
    public GameObject bossPrefab;
    [Tooltip("How far into the night (0..1) the boss rises.")]
    [Range(0f, 1f)] public float bossAtNight = 0.4f;
    [Tooltip("First night with a boss (1 = the first night).")]
    public int bossFromNight = 1;

    [Header("Dawn")]
    [Tooltip("Night enemies the player can still see get this many seconds before they are forced away.")]
    public float dawnGrace = 20f;

    readonly HashSet<EnemyBase> nightSpawned = new HashSet<EnemyBase>();
    float nextSpawn;
    bool bossThisNight;
    int nightNumber;
    Transform player;

    public bool IsNight { get { return DayNightManager.Instance != null && DayNightManager.Instance.IsNight; } }

    void Awake() { Instance = this; }

    void Start()
    {
        DayNightManager dn = DayNightManager.Instance;
        if (dn != null)
        {
            dn.OnNightStart.AddListener(OnNight);
            dn.OnDawn.AddListener(OnDawn);
        }
        nextSpawn = Time.time + 4f;
    }

    void OnDestroy()
    {
        DayNightManager dn = DayNightManager.Instance;
        if (dn != null)
        {
            dn.OnNightStart.RemoveListener(OnNight);
            dn.OnDawn.RemoveListener(OnDawn);
        }
    }

    bool FindPlayer()
    {
        if (player != null) return true;
        ToolController tc = ToolController.Instance;
        if (tc != null) player = tc.transform;
        return player != null;
    }

    // ------------------------------------------------------------------ phases

    void OnNight()
    {
        NightUI.Banner("THE NIGHT HAS COME", "Survive until dawn", true);
        nightNumber++;
        bossThisNight = false;
        if (!FindPlayer()) return;
        int n = Mathf.Min(nightFirstWave, Mathf.Max(0, nightMax - AliveCount()));
        for (int i = 0; i < n; i++) SpawnOne(true);
        nextSpawn = Time.time + nightInterval;
    }

    void OnDawn()
    {
        NightUI.Banner("DAWN", "The creatures of the night retreat", false);
        StartCoroutine(DawnRoutine());
    }

    IEnumerator DawnRoutine()
    {
        float end = Time.time + dawnGrace;
        while (true)
        {
            bool any = false;
            List<EnemyBase> list = new List<EnemyBase>(nightSpawned);
            foreach (EnemyBase e in list)
            {
                if (e == null || e.IsDead) { nightSpawned.Remove(e); continue; }
                any = true;
                // melt away when the player isn't looking, or when the grace time is over
                if (e.data.isBoss && e.IsHuntingPlayer && Time.time < end + 60f) continue;   // the fight goes on
                if (!AimAssist.InSight(e) || Time.time > end)
                {
                    nightSpawned.Remove(e);
                    e.Vanish();
                }
            }
            if (!any || IsNight) yield break;
            yield return new WaitForSeconds(0.5f);
        }
    }

    // ------------------------------------------------------------------ spawning

    void Update()
    {
        if (!FindPlayer()) return;

        Recycle();

        if (IsNight && !bossThisNight && bossPrefab != null && nightNumber >= bossFromNight && DayNightManager.Instance.PhaseProgress >= bossAtNight)
        {
            bossThisNight = true;
            SpawnBoss();
        }

        if (Time.time < nextSpawn) return;
        bool night = IsNight;
        nextSpawn = Time.time + (night ? nightInterval : dayInterval);
        if (AliveCount() < (night ? nightMax : dayMax)) SpawnOne(night);
    }

    int AliveCount()
    {
        int n = 0;
        foreach (EnemyBase e in EnemyBase.All) if (e != null && !e.IsDead && !e.data.isBoss) n++;
        return n;
    }

    void Recycle()
    {
        for (int i = EnemyBase.All.Count - 1; i >= 0; i--)
        {
            EnemyBase e = EnemyBase.All[i];
            if (e == null || e.IsDead || e.IsHuntingPlayer || e.data.isBoss) continue;
            if ((e.transform.position - player.position).sqrMagnitude < recycleDistance * recycleDistance) continue;
            nightSpawned.Remove(e);
            e.Vanish();
        }
    }

    GameObject PickPrefab(bool night)
    {
        float total = 0f;
        foreach (SpawnType t in types) if (t.prefab != null) total += night ? t.nightWeight : t.dayWeight;
        if (total <= 0f) return null;
        float r = Random.value * total;
        foreach (SpawnType t in types)
        {
            if (t.prefab == null) continue;
            r -= night ? t.nightWeight : t.dayWeight;
            if (r <= 0f) return t.prefab;
        }
        return null;
    }

    /// <summary>Spawns one enemy out of sight around the player. Returns it (or null if no spot was found).</summary>
    public EnemyBase SpawnOne(bool night)
    {
        if (!FindPlayer() || AliveCount() >= hardCap) return null;
        GameObject prefab = PickPrefab(night);
        if (prefab == null) return null;

        Vector3 pos;
        if (!FindSpot(out pos)) return null;

        GameObject go = Instantiate(prefab, pos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
        NavMeshAgent agent = go.GetComponent<NavMeshAgent>();
        if (agent != null) agent.Warp(pos);
        EnemyBase e = go.GetComponent<EnemyBase>();
        if (e == null) return null;

        if (e.data != null && e.data.patrols) e.patrolPoints = EnemyWaypoints.Loop(pos);   // Dungeon Guard walks a loop
        if (night || (e.data != null && e.data.nightOnly)) nightSpawned.Add(e);
        return e;
    }

    /// <summary>Raises the boss out of sight, 18-30 m away, already hunting the player.</summary>
    public EnemyBase SpawnBoss()
    {
        if (bossPrefab == null || !FindPlayer()) return null;
        float mn = minDistance, mx = maxDistance;
        minDistance = 18f; maxDistance = 30f;
        Vector3 pos;
        bool ok = FindSpot(out pos);
        minDistance = mn; maxDistance = mx;
        if (!ok) return null;
        GameObject go = Instantiate(bossPrefab, pos, Quaternion.LookRotation(player.position - pos));
        NavMeshAgent agent = go.GetComponent<NavMeshAgent>();
        if (agent != null) agent.Warp(pos);
        EnemyBase e = go.GetComponent<EnemyBase>();
        if (e == null) return null;
        e.Alert(player.position);
        nightSpawned.Add(e);
        string n = e.data != null ? e.data.displayName.ToUpperInvariant() : "THE BOSS";
        NightUI.Banner(n + " AWAKENS", "Something huge is hunting you", true);
        return e;
    }

    bool FindSpot(out Vector3 pos)
    {
        Camera cam = Camera.main;
        Vector3 eye = cam != null ? cam.transform.position : player.position + Vector3.up * 1.5f;
        NavMeshPath path = new NavMeshPath();

        for (int attempt = 0; attempt < 30; attempt++)
        {
            Vector2 dir = Random.insideUnitCircle.normalized;
            float dist = Random.Range(minDistance, maxDistance);
            Vector3 p = player.position + new Vector3(dir.x, 0f, dir.y) * dist;

            NavMeshHit hit;
            if (!NavMesh.SamplePosition(p, out hit, 2.5f, NavMesh.AllAreas)) continue;
            p = hit.position;

            // never in plain view
            bool hidden = EnemyBase.WallBetween(eye, p + Vector3.up * 1.2f) && EnemyBase.WallBetween(eye, p + Vector3.up * 0.4f);
            if (!hidden) continue;

            // not on top of another enemy
            bool crowded = false;
            foreach (EnemyBase o in EnemyBase.All)
                if (o != null && (o.transform.position - p).sqrMagnitude < 16f) { crowded = true; break; }
            if (crowded) continue;

            // it must be able to walk to the player
            if (!NavMesh.CalculatePath(p, player.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) continue;

            pos = p;
            return true;
        }
        pos = Vector3.zero;
        return false;
    }
}
