using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

/// <summary>
/// The boss hall. When the player walks in (after the sealed door opened) the Dungeon Lord rises.
/// When it dies, an escape portal opens; [E] in the portal wins the game.
/// </summary>
public class BossArena : MonoBehaviour
{
    public Bounds area;
    public GameObject bossPrefab;
    public Transform bossPoint, portalPoint;
    [HideInInspector] public GameObject portal;

    EnemyBase boss;
    bool spawned, done;
    WorldPrompt portalPrompt;

    void OnEnable() { EnemyBase.OnAnyDied += OnDied; }
    void OnDisable() { EnemyBase.OnAnyDied -= OnDied; }

    void Update()
    {
        DungeonQuest q = DungeonQuest.Instance;
        ToolController tc = ToolController.Instance;
        if (q == null || tc == null) return;

        if (!spawned && q.Stage == QuestStage.BossDoor && area.Contains(tc.transform.position)) SpawnBoss();

        if (portal != null)
        {
            portal.transform.GetChild(0).Rotate(0f, 0f, 60f * Time.deltaTime, Space.Self);
            bool near = WorldPrompt.PlayerNear(portal.transform.position, 2.6f);
            portalPrompt.Tick(near);
            if (near && WorldPrompt.CanAct() && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame) q.Escape();
        }
    }

    void SpawnBoss()
    {
        spawned = true;
        if (bossPrefab == null || bossPoint == null) { Finish(); return; }
        Vector3 pos = bossPoint.position;
        NavMeshHit hit;
        if (NavMesh.SamplePosition(pos, out hit, 3f, NavMesh.AllAreas)) pos = hit.position;
        GameObject go = Instantiate(bossPrefab, pos, bossPoint.rotation);
        NavMeshAgent ag = go.GetComponent<NavMeshAgent>();
        if (ag != null) ag.Warp(pos);
        boss = go.GetComponent<EnemyBase>();
        if (boss != null && ToolController.Instance != null) boss.Alert(ToolController.Instance.transform.position);
        string n = boss != null && boss.data != null ? boss.data.displayName.ToUpperInvariant() : "THE DUNGEON LORD";
        NightUI.Banner(n + " AWAKENS", "Defeat it to escape the maze", true);
        if (CameraShake.Instance != null) CameraShake.Instance.Shake(0.4f, 1f);
        if (DungeonQuest.Instance != null) DungeonQuest.Instance.BossStarted();
    }

    void OnDied(EnemyBase e)
    {
        if (done || boss == null || e != boss) return;
        Invoke("Finish", 2.5f);
        done = true;
    }

    void Finish()
    {
        done = true;
        if (DungeonQuest.Instance != null) DungeonQuest.Instance.BossDefeated();
        if (portal == null) portal = BuildPortal(portalPoint != null ? portalPoint.position : area.center);
    }

    GameObject BuildPortal(Vector3 at)
    {
        GameObject root = new GameObject("EscapePortal");
        root.transform.position = at;
        Transform spin = new GameObject("Spin").transform;
        spin.SetParent(root.transform, false);
        spin.localPosition = Vector3.up * 1.7f;

        Shader sh = Shader.Find("Universal Render Pipeline/Lit");
        Material ring = new Material(sh);
        ring.SetColor("_BaseColor", new Color(0.25f, 0.1f, 0.45f));
        ring.EnableKeyword("_EMISSION");
        ring.SetColor("_EmissionColor", new Color(0.7f, 0.35f, 2.2f) * 2.2f);
        Material core = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        core.SetColor("_BaseColor", new Color(0.55f, 0.35f, 1f, 0.75f));
        core.SetFloat("_Surface", 1f); core.SetFloat("_Blend", 0f);
        core.SetOverrideTag("RenderType", "Transparent");
        core.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        core.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
        core.SetInt("_ZWrite", 0);
        core.renderQueue = 3000;
        core.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

        for (int i = 0; i < 16; i++)
        {
            float a = i / 16f * Mathf.PI * 2f;
            GameObject s = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(s.GetComponent<Collider>());
            s.transform.SetParent(spin, false);
            s.transform.localPosition = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * 1.25f;
            s.transform.localRotation = Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg);
            s.transform.localScale = new Vector3(0.22f, 0.5f, 0.22f);
            s.GetComponent<Renderer>().sharedMaterial = ring;
        }
        GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(disc.GetComponent<Collider>());
        disc.transform.SetParent(spin, false);
        disc.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        disc.transform.localScale = new Vector3(2.3f, 0.02f, 2.3f);
        disc.GetComponent<Renderer>().sharedMaterial = core;

        Light l = new GameObject("Light").AddComponent<Light>();
        l.transform.SetParent(root.transform, false);
        l.transform.localPosition = Vector3.up * 1.8f;
        l.type = LightType.Point; l.color = new Color(0.7f, 0.45f, 1f); l.range = 12f; l.intensity = 4f;
        l.gameObject.AddComponent<TorchFlicker>().amount = 0.2f;

        ParticleSystem ps = new GameObject("Motes").AddComponent<ParticleSystem>();
        ps.transform.SetParent(spin, false);
        var main = ps.main; main.startLifetime = 1.6f; main.startSpeed = 0.4f; main.startSize = 0.08f; main.maxParticles = 200;
        main.startColor = new Color(0.8f, 0.6f, 1f); main.simulationSpace = ParticleSystemSimulationSpace.World;
        var em = ps.emission; em.rateOverTime = 60f;
        var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = 1.2f;
        ParticleSystemRenderer pr = ps.GetComponent<ParticleSystemRenderer>();
        pr.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        pr.sharedMaterial.SetColor("_BaseColor", new Color(0.85f, 0.7f, 1f));

        portalPrompt = new WorldPrompt(root.transform, 3.4f, WorldPrompt.Key("Escape the maze"));
        AudioSource.PlayClipAtPoint(SimpleSfx.Chime(), at, 1f);
        return root;
    }
}
