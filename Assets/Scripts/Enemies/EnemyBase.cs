using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public enum EnemyState { Idle, Wander, Patrol, Suspicious, Hunt, Attack, Hurt, Dead }

/// <summary>
/// The brain of every enemy (one FSM for all types, the numbers come from EnemyData).
///   Idle / Wander / Patrol  ->  Suspicious (heard or glimpsed something)  ->  Hunt  ->  Attack (telegraphed)
///   Hurt (short stagger + knockback) and Dead (falls, sinks, fades, may drop loot).
/// Senses: a sight cone blocked by maze walls, and hearing of the player's noise (PlayerNoise).
/// The model has no animation clips, so the body is animated in code: bob, lean, wind-up and lunge.
/// Put it on a root object with a NavMeshAgent; the model goes into the child called "Visual".
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class EnemyBase : MonoBehaviour
{
    public EnemyData data;
    [Tooltip("The model. If empty, the first child is used.")]
    public Transform visual;
    [Tooltip("Waypoints for patrolling types (filled by the spawner).")]
    public List<Vector3> patrolPoints = new List<Vector3>();

    public EnemyState State { get; private set; } = EnemyState.Idle;
    public float Health { get; private set; }
    public bool IsDead { get { return State == EnemyState.Dead; } }
    public bool IsHuntingPlayer { get { return State == EnemyState.Hunt || State == EnemyState.Attack; } }
    /// <summary>Seconds since it was last hit (for the health bar).</summary>
    public float TimeSinceHit { get { return Time.time - lastHitTime; } }

    public static readonly List<EnemyBase> All = new List<EnemyBase>();
    public static event Action<EnemyBase> OnAnyDied;
    public static event Action<EnemyBase, float> OnAnyDamaged;
    /// <summary>Hook for Phase 2 (boss gets a new attack below 50% health).</summary>
    public event Action<EnemyBase> OnHalfHealth;
    /// <summary>Raised when a wind-up starts (the boss draws its slam circle from this).</summary>
    public event Action<EnemyBase> OnAttackStart;
    /// <summary>Raised the moment a ground slam lands.</summary>
    public event Action<EnemyBase> OnSlam;

    NavMeshAgent agent;
    Transform player;
    HealthManager playerHealth;
    Vector3 lastKnown;
    float lastContact = -999f, stateTime, nextAttack, lastHitTime = -999f, idleUntil, repathAt;
    int patrolIndex;
    bool halfFired;
    Renderer[] rends;
    MaterialPropertyBlock mpb;
    float flash, glow;
    Color baseTint = Color.white;
    Vector3 visualBasePos, visualBaseScale;
    Quaternion visualBaseRot;
    float lean, attackPose, bobT;
    Coroutine attackRoutine;
    EnemyRig rig;
    Animator anim;
    [Tooltip("Where in the attack clip (0..1) the weapon hits. The clip is sped up so this moment matches the end of the wind-up.")]
    [Range(0.1f, 0.9f)] public float attackHitPoint = 0.45f;
    float deadLimp;

    // ------------------------------------------------------------------ setup

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (visual == null && transform.childCount > 0) visual = transform.GetChild(0);
        rends = visual != null ? visual.GetComponentsInChildren<Renderer>() : GetComponentsInChildren<Renderer>();
        mpb = new MaterialPropertyBlock();
        if (visual != null)
        {
            rig = visual.GetComponent<EnemyRig>();   // procedural bones only when added by hand (off by default)
            if (rig != null) rig.body = transform;
            anim = visual.GetComponent<Animator>();
            if (anim != null) anim.applyRootMotion = false;
        }
    }

    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); }

    void Start()
    {
        if (data == null) { Debug.LogWarning(name + ": EnemyBase has no EnemyData.", this); enabled = false; return; }
        Health = data.maxHealth;
        agent.speed = data.walkSpeed;
        agent.angularSpeed = data.turnSpeed;
        agent.acceleration = data.acceleration;
        agent.stoppingDistance = Mathf.Max(0.2f, data.attackRange * 0.75f);
        agent.autoBraking = true;
        transform.localScale = Vector3.one * data.scale;
        baseTint = data.tint;
        if (visual != null)
        {
            visualBasePos = visual.localPosition;
            visualBaseRot = visual.localRotation;
            visualBaseScale = visual.localScale;
        }
        if (anim != null && anim.runtimeAnimatorController != null && data.crawls) anim.SetBool("Crawl", true);
        ApplyColours();
        bobT = UnityEngine.Random.value * 10f;
        Enter(data.patrols && patrolPoints.Count > 1 ? EnemyState.Patrol : EnemyState.Wander);
    }

    // ------------------------------------------------------------------ main loop

    void Update()
    {
        if (data == null) return;
        FindPlayer();
        stateTime += Time.deltaTime;
        if (State != EnemyState.Dead)
        {
            Sense();
            Think();
            ApplySlow();
        }
        AnimateBody();
        flash = Mathf.MoveTowards(flash, 0f, Time.deltaTime * 6f);
        ApplyColours();
    }

    void FindPlayer()
    {
        if (player != null) return;
        ToolController tc = ToolController.Instance;
        if (tc == null) return;
        player = tc.transform;
        playerHealth = player.GetComponent<HealthManager>();
    }

    bool PlayerAlive { get { return player != null && (playerHealth == null || !playerHealth.IsDead); } }
    Vector3 Eye { get { return transform.position + Vector3.up * 1.4f * data.scale; } }
    Vector3 PlayerChest { get { return player.position + Vector3.up * 1.1f; } }

    // ------------------------------------------------------------------ senses

    void Sense()
    {
        if (!PlayerAlive) return;
        bool contact = false;
        if (CanSeePlayer()) { contact = true; }
        else if (CanHearPlayer())
        {
            lastKnown = player.position;
            lastContact = Time.time;
            if (State == EnemyState.Idle || State == EnemyState.Wander || State == EnemyState.Patrol) Enter(EnemyState.Suspicious);
            if (!data.canSee) contact = true;   // a blind hunter hunts by sound only
        }
        if (contact)
        {
            lastKnown = player.position;
            lastContact = Time.time;
            if (State != EnemyState.Hunt && State != EnemyState.Attack && State != EnemyState.Hurt)
            {
                Enter(EnemyState.Hunt);
                PlaySound(data.alertSound, GenClip.Alert, 0.7f);
                if (data.alertRadius > 0f) AlertOthers();
            }
        }
    }

    public bool CanSeePlayer()
    {
        if (!data.canSee || player == null) return false;
        Vector3 to = PlayerChest - Eye;
        float dist = to.magnitude;
        // already hunting: a wider view, it keeps you in its eye
        float range = IsHuntingPlayer ? data.sightRange * 1.3f : data.sightRange;
        if (dist > range) return false;
        float angle = IsHuntingPlayer ? Mathf.Max(data.sightAngle, 200f) : data.sightAngle;
        Vector3 flat = new Vector3(to.x, 0f, to.z);
        if (dist > 1.5f && Vector3.Angle(transform.forward, flat) > angle * 0.5f) return false;
        return !WallBetween(Eye, PlayerChest);
    }

    bool CanHearPlayer()
    {
        float level = PlayerNoise.Level;
        if (level <= 0.01f) return false;
        float d = Vector3.Distance(transform.position, player.position);
        if (d > data.hearingRange * level) return false;
        // walls muffle the sound to half
        if (WallBetween(Eye, PlayerChest) && d > data.hearingRange * level * 0.5f) return false;
        return true;
    }

    public static bool WallBetween(Vector3 a, Vector3 b)
    {
        Vector3 d = b - a;
        RaycastHit[] hits = Physics.RaycastAll(a, d.normalized, d.magnitude, ~(1 << 8), QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++) if (ResourceNode.IsWall(hits[i].collider)) return true;
        return false;
    }

    void AlertOthers()
    {
        foreach (EnemyBase e in All)
        {
            if (e == this || e.IsDead || e.IsHuntingPlayer) continue;
            if (Vector3.Distance(e.transform.position, transform.position) > data.alertRadius) continue;
            e.Alert(lastKnown);
        }
    }

    /// <summary>Another enemy shouted: come and look.</summary>
    public void Alert(Vector3 where)
    {
        if (IsDead) return;
        lastKnown = where;
        lastContact = Time.time;
        Enter(EnemyState.Suspicious);
    }

    // ------------------------------------------------------------------ states

    void Enter(EnemyState s)
    {
        if (State == EnemyState.Dead) return;
        State = s;
        stateTime = 0f;
        if (!agent.isOnNavMesh) return;
        switch (s)
        {
            case EnemyState.Idle:
                agent.isStopped = true;
                idleUntil = Time.time + UnityEngine.Random.Range(data.idleTime.x, data.idleTime.y);
                break;
            case EnemyState.Wander:
                agent.isStopped = false; agent.speed = data.walkSpeed;
                PickWanderPoint();
                break;
            case EnemyState.Patrol:
                agent.isStopped = false; agent.speed = data.walkSpeed;
                if (patrolPoints.Count > 0) agent.SetDestination(patrolPoints[patrolIndex % patrolPoints.Count]);
                break;
            case EnemyState.Suspicious:
                agent.isStopped = false; agent.speed = (data.walkSpeed + data.runSpeed) * 0.5f;
                agent.SetDestination(lastKnown);
                break;
            case EnemyState.Hunt:
                agent.isStopped = false; agent.speed = data.runSpeed;
                break;
            case EnemyState.Attack:
            case EnemyState.Hurt:
                agent.isStopped = true;
                break;
        }
    }

    void Think()
    {
        if (!agent.isOnNavMesh) return;
        switch (State)
        {
            case EnemyState.Idle:
                if (Time.time > idleUntil) Enter(data.patrols && patrolPoints.Count > 1 ? EnemyState.Patrol : EnemyState.Wander);
                break;

            case EnemyState.Wander:
                if (Arrived() || stateTime > 14f) Enter(EnemyState.Idle);
                break;

            case EnemyState.Patrol:
                if (Arrived())
                {
                    patrolIndex = (patrolIndex + 1) % Mathf.Max(1, patrolPoints.Count);
                    Enter(stateTime > 0.5f ? EnemyState.Idle : EnemyState.Patrol);
                }
                break;

            case EnemyState.Suspicious:
                LookTowards(lastKnown, 0.5f);
                if (Arrived() || stateTime > 10f) Enter(EnemyState.Idle);
                break;

            case EnemyState.Hunt:
                if (!PlayerAlive) { Enter(EnemyState.Wander); break; }
                if (data.fearsLight) { Vector3 lightFrom; if (FearLight.Scares(transform.position, out lightFrom)) { Flee(lightFrom); break; } }
                if (Time.time - lastContact > data.loseTime)
                {
                    // lost you: check the last place it knew, then give up
                    Enter(EnemyState.Suspicious);
                    break;
                }
                if (Time.time > repathAt)
                {
                    repathAt = Time.time + 0.2f;
                    agent.SetDestination(Time.time - lastContact < 0.3f ? player.position : lastKnown);
                }
                float d = Vector3.Distance(transform.position, player.position);
                if (d <= data.attackRange + 0.25f && Time.time >= nextAttack && !WallBetween(Eye, PlayerChest))
                {
                    Enter(EnemyState.Attack);
                    attackRoutine = StartCoroutine(AttackRoutine());
                }
                else if (d <= data.attackRange) LookTowards(player.position, 1f);
                break;

            case EnemyState.Attack:
                break;   // driven by AttackRoutine

            case EnemyState.Hurt:
                if (stateTime > 0.35f) Enter(PlayerAlive ? EnemyState.Hunt : EnemyState.Wander);
                break;
        }
    }

    /// <summary>Backs away from a scary light (torch / campfire) instead of hunting.</summary>
    void Flee(Vector3 from)
    {
        lastContact = Time.time;   // keep the hunt alive so it returns once the light is gone
        if (Time.time < repathAt) return;
        repathAt = Time.time + 0.25f;
        Vector3 away = transform.position - from; away.y = 0f;
        if (away.sqrMagnitude < 0.01f) away = -transform.forward;
        Vector3 target = transform.position + away.normalized * 9f;
        NavMeshHit hit;
        if (NavMesh.SamplePosition(target, out hit, 6f, NavMesh.AllAreas))
        {
            agent.speed = data.runSpeed;
            agent.SetDestination(hit.position);
        }
    }

    float slowUntil, slowFactor = 1f;
    bool slowed;

    /// <summary>Slows the enemy to factor x its speed for the given seconds (spike trap).</summary>
    public void Slow(float factor, float seconds)
    {
        slowFactor = Mathf.Clamp(factor, 0.1f, 1f);
        slowUntil = Mathf.Max(slowUntil, Time.time + seconds);
    }

    void ApplySlow()
    {
        if (agent == null || data == null) return;
        float baseSpeed = (State == EnemyState.Hunt || State == EnemyState.Attack) ? data.runSpeed : data.walkSpeed;
        if (Time.time < slowUntil) { slowed = true; agent.speed = baseSpeed * slowFactor; }
        else if (slowed) { slowed = false; agent.speed = baseSpeed; }
    }

    bool Arrived()
    {
        return !agent.pathPending && agent.remainingDistance <= Mathf.Max(agent.stoppingDistance, 0.6f);
    }

    void PickWanderPoint()
    {
        for (int i = 0; i < 10; i++)
        {
            Vector3 p = transform.position + UnityEngine.Random.insideUnitSphere * data.wanderRadius;
            NavMeshHit hit;
            if (NavMesh.SamplePosition(p, out hit, 2f, NavMesh.AllAreas)) { agent.SetDestination(hit.position); return; }
        }
    }

    void LookTowards(Vector3 p, float strength)
    {
        Vector3 d = p - transform.position; d.y = 0f;
        if (d.sqrMagnitude < 0.01f) return;
        Quaternion target = Quaternion.LookRotation(d);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, target, data.turnSpeed * strength * Time.deltaTime);
    }

    // ------------------------------------------------------------------ attack

    IEnumerator AttackRoutine()
    {
        // 1) wind-up: stops, faces you, glows and pulls back (the telegraph)
        float t = 0f;
        PlaySound(null, GenClip.Windup, 0.45f);
        OnAttackStart?.Invoke(this);
        if (anim != null && anim.runtimeAnimatorController != null)
        {
            string trig = data.slamRadius > 0f ? "Slam" : "Attack";   // bosses use the ground slam
            AnimationClip c = FindClip(trig);
            float hitAt = c != null ? c.length * attackHitPoint : 1f;
            anim.SetFloat("AttackSpeed", Mathf.Clamp(hitAt / Mathf.Max(0.1f, data.attackWindup), 0.5f, 4f));
            anim.SetTrigger(trig);
        }
        while (t < data.attackWindup)
        {
            if (State != EnemyState.Attack) yield break;
            t += Time.deltaTime;
            float k = t / data.attackWindup;
            if (player != null) LookTowards(player.position, 2f);
            glow = k;
            attackPose = -k;              // leans back
            yield return null;
        }
        // 2) strike
        attackPose = 1f;
        glow = 0f;
        PlaySound(data.attackSound, GenClip.Attack, 0.8f);
        if (data.lungeDistance > 0f && agent.isOnNavMesh) StartCoroutine(Lunge());
        if (data.slamRadius > 0f) Slam();
        else if (PlayerAlive)
        {
            Vector3 to = player.position - transform.position; to.y = 0f;
            float reach = data.attackRange + 0.45f + data.lungeDistance;
            bool inFront = Vector3.Dot(transform.forward, to.normalized) > 0.35f;
            if (to.magnitude <= reach && inFront && !WallBetween(Eye, PlayerChest)) HitPlayer(data.damage);
        }
        nextAttack = Time.time + data.attackCooldown;
        // 3) recover
        float r = 0f;
        while (r < 0.4f) { r += Time.deltaTime; attackPose = Mathf.Lerp(1f, 0f, r / 0.4f); yield return null; }
        attackPose = 0f;
        attackRoutine = null;
        if (State == EnemyState.Attack) Enter(EnemyState.Hunt);
    }

    IEnumerator Lunge()
    {
        float t = 0f;
        while (t < 0.18f && !IsDead)
        {
            t += Time.deltaTime;
            agent.Move(transform.forward * (data.lungeDistance / 0.18f) * Time.deltaTime);
            yield return null;
        }
    }

    void Slam()
    {
        if (!PlayerAlive) return;
        float d = Vector3.Distance(transform.position, player.position);
        if (d <= data.slamRadius && !WallBetween(Eye, PlayerChest)) HitPlayer(data.damage * Mathf.Lerp(1f, 0.5f, d / data.slamRadius));
        OnSlam?.Invoke(this);
        CameraShake cs = CameraShake.Instance;
        if (cs != null) cs.Shake(Mathf.Lerp(0.18f, 0.05f, Mathf.Clamp01(d / 20f)), 0.45f);
    }

    void HitPlayer(float amount)
    {
        amount = ShieldController.Filter(amount, transform.position);   // shield block (frontal hits)
        if (playerHealth != null) playerHealth.TakeDamage(amount);
        PlayerHurtFX.Notify(transform.position, amount);
    }

    // ------------------------------------------------------------------ taking damage

    /// <summary>Damage from the player. 'from' is used for the knockback direction.</summary>
    public void TakeDamage(float amount, Vector3 from)
    {
        if (IsDead || amount <= 0f) return;
        Health -= amount;
        lastHitTime = Time.time;
        flash = 1f;
        OnAnyDamaged?.Invoke(this, amount);
        if (!halfFired && Health <= data.maxHealth * 0.5f) { halfFired = true; OnHalfHealth?.Invoke(this); }
        lastKnown = player != null ? player.position : from;
        lastContact = Time.time;

        if (Health <= 0f) { Die(); return; }

        PlaySound(data.hurtSound, GenClip.Hurt, 0.7f);
        // knockback, less for heavy enemies; a hit does not cancel a boss attack
        Vector3 away = transform.position - from; away.y = 0f;
        if (away.sqrMagnitude > 0.001f && agent.isOnNavMesh) StartCoroutine(Knockback(away.normalized * Mathf.Lerp(1.2f, 0f, data.knockbackResist)));
        if (!data.isBoss || State != EnemyState.Attack)
        {
            if (attackRoutine != null) { StopCoroutine(attackRoutine); attackRoutine = null; glow = 0f; attackPose = 0f; }
            Enter(EnemyState.Hurt); if (anim != null && anim.runtimeAnimatorController != null) anim.SetTrigger("Hit");
        }
        if (data.alertRadius > 0f) AlertOthers();
    }

    IEnumerator Knockback(Vector3 push)
    {
        float t = 0f;
        while (t < 0.15f && !IsDead && agent.isOnNavMesh)
        {
            t += Time.deltaTime;
            agent.Move(push * (Time.deltaTime / 0.15f));
            yield return null;
        }
    }

    void Die()
    {
        State = EnemyState.Dead;
        stateTime = 0f;
        StopAllCoroutines();
        if (agent.isOnNavMesh) agent.isStopped = true;
        agent.enabled = false;
        foreach (Collider c in GetComponentsInChildren<Collider>()) c.enabled = false;
        PlaySound(data.deathSound, GenClip.Death, 0.9f);
        if (anim != null && anim.runtimeAnimatorController != null) anim.SetTrigger("Die");
        if (UnityEngine.Random.value < data.lootChance)
        {
            int n = UnityEngine.Random.Range(data.lootAmount.x, data.lootAmount.y + 1);
            WorldPickup.Spawn(data.lootItem, n, transform.position + Vector3.up * 0.8f, null, null, null);
        }
        OnAnyDied?.Invoke(this);
        StartCoroutine(DeathRoutine());
    }

    IEnumerator DeathRoutine()
    {
        // falls over, lies still, sinks into the ground and fades
        Quaternion start = transform.rotation;
        Vector3 side = transform.right * (UnityEngine.Random.value < 0.5f ? 1f : -1f);
        float t = 0f;
        bool hasAnim = anim != null && anim.runtimeAnimatorController != null;
        while (!hasAnim && t < 0.5f) { t += Time.deltaTime; float k = Mathf.SmoothStep(0f, 1f, t / 0.5f); transform.rotation = Quaternion.AngleAxis(85f * k, side) * start; yield return null; }
        yield return new WaitForSeconds(1.2f);
        t = 0f;
        Vector3 p0 = transform.position;
        while (t < 1.2f)
        {
            t += Time.deltaTime;
            float k = t / 1.2f;
            transform.position = p0 + Vector3.down * k * 0.9f;
            fade = 1f - k;
            yield return null;
        }
        Destroy(gameObject);
    }

    /// <summary>Dawn: fades out quietly (used by the spawner, never in the player's face).</summary>
    public void Vanish()
    {
        if (IsDead) return;
        State = EnemyState.Dead;
        StopAllCoroutines();
        if (agent.isOnNavMesh) agent.isStopped = true;
        StartCoroutine(VanishRoutine());
    }

    IEnumerator VanishRoutine()
    {
        float t = 0f;
        while (t < 1.5f) { t += Time.deltaTime; fade = 1f - t / 1.5f; yield return null; }
        Destroy(gameObject);
    }

    // ------------------------------------------------------------------ body animation (no clips needed)

    float fade = 1f;

    void AnimateBody()
    {
        if (visual == null) return;
        float speed = agent != null && agent.enabled ? agent.velocity.magnitude : 0f;
        if (anim != null && anim.runtimeAnimatorController != null)
        {
            anim.SetFloat("Speed", speed / Mathf.Max(0.01f, data.scale), 0.1f, Time.deltaTime);
            // a short stagger on hit (the clips have no hit reaction)
            float hurt = State == EnemyState.Hurt ? Mathf.Sin(Mathf.Min(1f, stateTime / 0.35f) * Mathf.PI) * -14f : 0f;
            visual.localRotation = visualBaseRot * Quaternion.Euler(hurt, 0f, 0f);
            visual.localPosition = visualBasePos;
            return;
        }
        float run = Mathf.InverseLerp(0.2f, Mathf.Max(0.5f, data.runSpeed), speed);
        bobT += Time.deltaTime * Mathf.Lerp(2f, 11f, run);
        float bob = State == EnemyState.Dead ? 0f : Mathf.Abs(Mathf.Sin(bobT)) * Mathf.Lerp(0.02f, 0.12f, run);
        float breathe = 1f + Mathf.Sin(Time.time * 2f + bobT * 0.1f) * 0.02f;
        float sway = Mathf.Sin(bobT * 0.5f) * Mathf.Lerp(2f, 7f, run);
        lean = Mathf.Lerp(lean, run * 14f, Time.deltaTime * 6f);
        float pose = attackPose < 0f ? attackPose * 22f : attackPose * 28f;   // wind-up back, strike forward
        float hurtTilt = State == EnemyState.Hurt ? Mathf.Sin(Mathf.Min(1f, stateTime / 0.35f) * Mathf.PI) * -18f : 0f;
        visual.localPosition = visualBasePos + Vector3.up * bob / Mathf.Max(0.01f, data.scale)
                             + Vector3.forward * (attackPose > 0f ? attackPose * 0.25f : attackPose * 0.12f);
        visual.localRotation = visualBaseRot * Quaternion.Euler(lean + pose + hurtTilt, 0f, sway);
        float squash = attackPose < 0f ? 1f - attackPose * 0.08f : 1f;   // swells up while winding up
        visual.localScale = Vector3.Scale(visualBaseScale, new Vector3(squash, breathe * (2f - squash), squash));

        if (rig != null)
        {
            rig.run = State == EnemyState.Dead ? 0f : Mathf.Max(run, speed > 0.15f ? 0.3f : 0f);
            rig.cycle = bobT;   // one bob per step, two steps per stride
            rig.attack = attackPose;
            rig.lookAt = (IsHuntingPlayer || State == EnemyState.Suspicious) && player != null ? (Vector3?)(IsHuntingPlayer ? PlayerChest : lastKnown + Vector3.up) : null;
            if (State == EnemyState.Dead) deadLimp = Mathf.MoveTowards(deadLimp, 1f, Time.deltaTime * 2f);
            rig.dead = deadLimp;
        }
    }

    AnimationClip FindClip(string state)
    {
        if (anim == null || anim.runtimeAnimatorController == null) return null;
        AnimationClip best = null;
        foreach (AnimationClip c in anim.runtimeAnimatorController.animationClips)
            if (c != null && c.name.Contains(state)) { best = c; break; }   // the one non-looping clip is the attack
        return best;
    }

    void ApplyColours()
    {
        if (rends == null) return;
        Color c = baseTint;
        c = Color.Lerp(c, data != null ? (Color)data.glowColor : Color.red, glow * 0.85f);  // telegraph glow
        c = Color.Lerp(c, Color.white, flash);                                               // hit flash
        c.a = (data != null ? data.opacity : 1f) * fade;
        foreach (Renderer r in rends)
        {
            if (r == null) continue;
            r.GetPropertyBlock(mpb);
            mpb.SetColor("_BaseColor", c);
            mpb.SetColor("_Color", c);
            r.SetPropertyBlock(mpb);
        }
    }

    // ------------------------------------------------------------------ sound

    enum GenClip { Alert, Windup, Attack, Hurt, Death }
    static readonly Dictionary<GenClip, AudioClip> gen = new Dictionary<GenClip, AudioClip>();

    void PlaySound(AudioClip clip, GenClip fallback, float volume)
    {
        if (clip == null) clip = Generated(fallback);
        if (clip == null) return;
        GameObject go = new GameObject("SFX_Enemy");
        go.transform.position = transform.position + Vector3.up;
        AudioSource a = go.AddComponent<AudioSource>();
        a.clip = clip; a.volume = volume;
        a.pitch = UnityEngine.Random.Range(0.9f, 1.1f) / Mathf.Max(0.6f, Mathf.Sqrt(data.scale));
        a.spatialBlend = 1f; a.minDistance = 3f; a.maxDistance = 35f; a.rolloffMode = AudioRolloffMode.Linear;
        a.Play();
        Destroy(go, clip.length / a.pitch + 0.2f);
    }

    static AudioClip Generated(GenClip kind)
    {
        AudioClip c;
        if (gen.TryGetValue(kind, out c)) return c;
        System.Random rnd = new System.Random((int)kind * 97 + 13);
        float lp = 0f;
        Func<float, float, float> f;
        float len;
        switch (kind)
        {
            case GenClip.Alert:   // low growl rising
                len = 0.9f;
                f = (t, k) => { float n = (float)(rnd.NextDouble() * 2 - 1); lp += (n - lp) * 0.08f; return (Mathf.Sin(6.2832f * (70f + 40f * k) * t) * 0.6f + lp * 2.2f) * Mathf.Sin(k * Mathf.PI); };
                break;
            case GenClip.Windup:  // breathy inhale
                len = 0.5f;
                f = (t, k) => { float n = (float)(rnd.NextDouble() * 2 - 1); lp += (n - lp) * (0.05f + 0.2f * k); return lp * k * 1.4f; };
                break;
            case GenClip.Attack:  // snarl + whoosh
                len = 0.45f;
                f = (t, k) => { float n = (float)(rnd.NextDouble() * 2 - 1); lp += (n - lp) * 0.3f; return (lp * 1.2f + Mathf.Sin(6.2832f * 110f * t) * 0.5f) * Mathf.Exp(-t * 7f); };
                break;
            case GenClip.Hurt:    // short yelp
                len = 0.3f;
                f = (t, k) => Mathf.Sin(6.2832f * Mathf.Lerp(380f, 220f, k) * t) * Mathf.Exp(-t * 12f) * 0.7f;
                break;
            default:              // death groan falling
                len = 1.1f;
                f = (t, k) => { float n = (float)(rnd.NextDouble() * 2 - 1); lp += (n - lp) * 0.06f; return (Mathf.Sin(6.2832f * Mathf.Lerp(160f, 55f, k) * t) * 0.6f + lp * 1.5f) * (1f - k); };
                break;
        }
        const int rate = 44100;
        int count = (int)(rate * len);
        float[] d = new float[count];
        for (int i = 0; i < count; i++) d[i] = Mathf.Clamp(f(i / (float)rate, i / (float)count), -1f, 1f);
        c = AudioClip.Create("enemy_" + kind, count, 1, rate, false);
        c.SetData(d, 0);
        gen[kind] = c;
        return c;
    }

    // ------------------------------------------------------------------ gizmos

    void OnDrawGizmosSelected()
    {
        if (data == null) return;
        Vector3 p = transform.position + Vector3.up * 0.1f;
        Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.6f);
        DrawCircle(p, data.hearingRange);
        if (data.canSee)
        {
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.9f);
            float half = data.sightAngle * 0.5f;
            Vector3 l = Quaternion.AngleAxis(-half, Vector3.up) * transform.forward * data.sightRange;
            Vector3 r = Quaternion.AngleAxis(half, Vector3.up) * transform.forward * data.sightRange;
            Gizmos.DrawLine(p, p + l);
            Gizmos.DrawLine(p, p + r);
            int seg = 24;
            Vector3 prev = p + l;
            for (int i = 1; i <= seg; i++)
            {
                Vector3 q = p + Quaternion.AngleAxis(-half + data.sightAngle * i / seg, Vector3.up) * transform.forward * data.sightRange;
                Gizmos.DrawLine(prev, q); prev = q;
            }
        }
        if (data.alertRadius > 0f) { Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.4f); DrawCircle(p, data.alertRadius); }
        Gizmos.color = Color.cyan;
        for (int i = 0; i < patrolPoints.Count; i++) Gizmos.DrawLine(patrolPoints[i] + Vector3.up * 0.2f, patrolPoints[(i + 1) % patrolPoints.Count] + Vector3.up * 0.2f);
    }

    static void DrawCircle(Vector3 c, float r)
    {
        Vector3 prev = c + Vector3.forward * r;
        for (int i = 1; i <= 40; i++)
        {
            Vector3 q = c + Quaternion.AngleAxis(i * 9f, Vector3.up) * Vector3.forward * r;
            Gizmos.DrawLine(prev, q); prev = q;
        }
    }
}
