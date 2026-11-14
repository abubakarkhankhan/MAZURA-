using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Tools > Mazura > Build Enemy Types
/// Creates the five enemy types (EnemyData + prefab) in Assets/Enemies. Existing assets are NOT overwritten,
/// so anything you tuned by hand stays. Delete an asset first if you want it rebuilt.
/// </summary>
public static class MazuraEnemyBuilder
{
    const string Root = "Assets/Enemies";
    const string Chars = "Assets/Project Assest/Monster/Characters/";
    const string Imp = "Assets/Project Assest/Monster/Bestiary - Dungeon Monsters Kit[Standard]/Bestiary - Dungeon Monsters Kit[Standard]/Exports/FBX (Unity)/Imp.fbx";
    const string Puglin = "Assets/Project Assest/Monster/Bestiary - Dungeon Monsters Kit[Standard]/Bestiary - Dungeon Monsters Kit[Standard]/Exports/FBX (Unity)/Puglin.fbx";

    class Spec
    {
        public string id, model;
        public float visualScale = 1f, capH = 1.8f, capR = 0.45f, agentR = 0.45f;
        public bool transparentMat, smoke, heartbeat;
        public System.Action<EnemyData> fill;
    }

    [MenuItem("Tools/Mazura/Build Enemy Types")]
    public static void Build() { Build(null); }

    /// <summary>Rebuilds only the listed prefabs (by id, e.g. "ShadowWraith"); their EnemyData is kept.</summary>
    public static void Build(string[] force)
    {
        Spec[] specs =
        {
            new Spec { id = "ForestStalker", model = Chars + "Warrok.fbx", capH = 2.45f, capR = 0.6f, agentR = 0.6f, fill = d => {
                d.displayName = "Forest Stalker"; d.biome = EnemyBiome.Forest;
                d.walkSpeed = 1.3f; d.runSpeed = 2.7f; d.acceleration = 6f; d.turnSpeed = 200f; d.wanderRadius = 14f;
                d.canSee = true; d.sightRange = 26f; d.sightAngle = 150f; d.hearingRange = 18f; d.loseTime = 8f;
                d.maxHealth = 160f; d.damage = 28f; d.attackRange = 2.4f; d.attackWindup = 0.85f; d.attackCooldown = 3.6f; d.knockbackResist = 0.6f;
                d.scale = 1f; d.tint = new Color(0.82f, 0.8f, 0.76f); d.glowColor = new Color(2.2f, 0.35f, 0.08f);
                d.lootChance = 0.5f; d.lootAmount = new Vector2Int(1, 3); } },

            new Spec { id = "ForestCrawler", model = Puglin, capH = 0.7f, capR = 0.4f, agentR = 0.35f, fill = d => {
                d.displayName = "Forest Crawler"; d.biome = EnemyBiome.Forest;
                d.walkSpeed = 3.0f; d.runSpeed = 6.8f; d.acceleration = 22f; d.turnSpeed = 540f; d.wanderRadius = 9f; d.idleTime = new Vector2(0.6f, 1.8f);
                d.canSee = false; d.hearingRange = 17f; d.loseTime = 4f; d.crawls = true;
                d.maxHealth = 38f; d.damage = 10f; d.attackRange = 1.4f; d.attackWindup = 0.3f; d.attackCooldown = 1.1f; d.lungeDistance = 1.7f;
                d.scale = 1f; d.tint = new Color(0.55f, 0.5f, 0.5f); d.glowColor = new Color(1.8f, 0.2f, 0.1f);
                d.lootChance = 0.25f; } },

            new Spec { id = "DungeonGuard", model = Imp, capH = 1.8f, fill = d => {
                d.displayName = "Dungeon Guard"; d.biome = EnemyBiome.Dungeon;
                d.walkSpeed = 1.9f; d.runSpeed = 4.4f; d.patrols = true; d.idleTime = new Vector2(1.5f, 3f);
                d.canSee = true; d.sightRange = 17f; d.sightAngle = 100f; d.hearingRange = 9f; d.loseTime = 6f; d.alertRadius = 22f;
                d.maxHealth = 95f; d.damage = 16f; d.attackRange = 1.9f; d.attackWindup = 0.6f; d.attackCooldown = 1.8f; d.knockbackResist = 0.25f;
                d.scale = 1f; d.tint = new Color(0.72f, 0.78f, 1f); d.glowColor = new Color(2f, 1.2f, 0.2f);
                d.lootChance = 0.4f; } },

            new Spec { id = "DungeonSpecter", model = Chars + "Skeletonzombie.fbx", visualScale = 0.86f, capH = 1.9f, capR = 0.3f, agentR = 0.22f, transparentMat = true, fill = d => {
                d.displayName = "Dungeon Specter"; d.biome = EnemyBiome.Dungeon;
                d.walkSpeed = 3.2f; d.runSpeed = 7.6f; d.acceleration = 20f; d.turnSpeed = 480f; d.wanderRadius = 12f;
                d.canSee = true; d.sightRange = 19f; d.sightAngle = 55f; d.hearingRange = 8f; d.loseTime = 4f;
                d.maxHealth = 30f; d.damage = 14f; d.attackRange = 1.6f; d.attackWindup = 0.35f; d.attackCooldown = 1.3f;
                d.scale = 1f; d.tint = new Color(0.55f, 0.85f, 1f); d.opacity = 0.45f; d.glowColor = new Color(0.3f, 1.4f, 2.2f);
                d.lootChance = 0.2f; } },

            new Spec { id = "ShadowWraith", model = Chars + "Parasite.fbx", visualScale = 1.05f, capH = 2.3f, capR = 0.5f, agentR = 0.45f, transparentMat = true, smoke = true, heartbeat = true, fill = d => {
                d.displayName = "Shadow Wraith"; d.biome = EnemyBiome.Any; d.nightOnly = true;
                d.walkSpeed = 2.2f; d.runSpeed = 5.2f; d.acceleration = 12f; d.wanderRadius = 12f;
                d.canSee = true; d.sightRange = 22f; d.sightAngle = 120f; d.hearingRange = 16f; d.loseTime = 7f;
                d.maxHealth = 125f; d.damage = 32f; d.attackRange = 2.1f; d.attackWindup = 0.7f; d.attackCooldown = 2.4f; d.knockbackResist = 0.3f;
                d.scale = 1f; d.tint = new Color(0.07f, 0.07f, 0.1f); d.opacity = 0.88f; d.glowColor = new Color(1.6f, 0.1f, 0.6f);
                d.smoke = true; d.heartbeatRange = 24f; d.lootChance = 0.6f; d.lootAmount = new Vector2Int(1, 3); } },
        };

        EnsureFolder(Root); EnsureFolder(Root + "/Data"); EnsureFolder(Root + "/Prefabs"); EnsureFolder(Root + "/Materials");
        RuntimeAnimatorController ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(Root + "/Enemy.controller");
        Material puff = MakePuffMaterial();

        foreach (Spec s in specs)
        {
            string dataPath = Root + "/Data/" + s.id + ".asset";
            EnemyData data = AssetDatabase.LoadAssetAtPath<EnemyData>(dataPath);
            if (data == null) { data = ScriptableObject.CreateInstance<EnemyData>(); s.fill(data); AssetDatabase.CreateAsset(data, dataPath); }

            string prefabPath = Root + "/Prefabs/" + s.id + ".prefab";
            bool forced = force != null && System.Array.IndexOf(force, s.id) >= 0;
            if (!forced && AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null) { Debug.Log(s.id + ": prefab exists, kept."); continue; }

            GameObject root = new GameObject(s.id);
            CapsuleCollider cap = root.AddComponent<CapsuleCollider>();
            cap.radius = s.capR; cap.height = s.capH; cap.center = new Vector3(0f, s.capH * 0.5f, 0f);
            NavMeshAgent agent = root.AddComponent<NavMeshAgent>();
            agent.radius = s.agentR; agent.height = s.capH; agent.baseOffset = 0f;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
            EnemyBase eb = root.AddComponent<EnemyBase>();
            eb.data = data;

            GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(s.model), root.transform);
            model.name = "Visual";
            model.transform.localScale = Vector3.one * s.visualScale;
            Animator an = model.GetComponent<Animator>();
            if (an == null) an = model.AddComponent<Animator>();
            an.runtimeAnimatorController = ctrl; an.applyRootMotion = false;
            eb.visual = model.transform;

            if (s.transparentMat) MakeTransparent(model, s.id, s.model);

            if (s.smoke) AddSmoke(root.transform, puff, s.capH);
            if (s.heartbeat) root.AddComponent<EnemyHeartbeat>();

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            Debug.Log("Built " + s.id);
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    // ---------------------------------------------------------------- helpers

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path));
    }

    /// <summary>Copies each material to a transparent URP/Lit one, so EnemyData.opacity actually fades the model.</summary>
    static void MakeTransparent(GameObject model, string id, string modelPath)
    {
        int n = 0;
        string texDir = Path.GetDirectoryName(modelPath).Replace('\\', '/') + "/Textures_" + Path.GetFileNameWithoutExtension(modelPath);
        Texture2D body = FindTex(texDir, "_body_diffuse"), diff = FindTex(texDir, "_diffuse", "_body_diffuse"), nrm = FindTex(texDir, "_normal"), glow = FindTex(texDir, "_glow");
        foreach (Renderer r in model.GetComponentsInChildren<Renderer>())
        {
            Material[] mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null) continue;
                Material m = new Material(mats[i]);
                m.name = id + "_Mat" + (n++);
                Texture2D bt = (i == 0 ? body : diff); if (bt == null) bt = diff != null ? diff : body;
                if (bt != null) m.SetTexture("_BaseMap", bt);
                if (nrm != null) { m.SetTexture("_BumpMap", nrm); m.EnableKeyword("_NORMALMAP"); }
                if (glow != null && id == "DungeonSpecter") { m.SetTexture("_EmissionMap", glow); m.SetColor("_EmissionColor", new Color(0.4f, 1.1f, 1.6f)); m.EnableKeyword("_EMISSION"); }
                m.SetFloat("_Smoothness", 0.15f);
                m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f);
                m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.SetOverrideTag("RenderType", "Transparent");
                m.renderQueue = 3000;
                AssetDatabase.CreateAsset(m, Root + "/Materials/" + m.name + ".mat");
                mats[i] = m;
            }
            r.sharedMaterials = mats;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }

    static Texture2D FindTex(string dir, string suffix, string exclude = null)
    {
        foreach (string g in AssetDatabase.FindAssets("t:Texture2D", new[] { dir }))
        {
            string p = AssetDatabase.GUIDToAssetPath(g);
            string n = Path.GetFileNameWithoutExtension(p);
            if (n.EndsWith(suffix) && (exclude == null || !n.EndsWith(exclude))) return AssetDatabase.LoadAssetAtPath<Texture2D>(p);
        }
        return null;
    }

    static Material MakePuffMaterial()
    {
        string tp = Root + "/Materials/SoftPuff.png", mp = Root + "/Materials/WraithSmoke.mat";
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(mp);
        if (existing != null) return existing;

        // soft radial gradient
        int sz = 64; Texture2D t = new Texture2D(sz, sz, TextureFormat.RGBA32, false);
        for (int y = 0; y < sz; y++) for (int x = 0; x < sz; x++)
        {
            float d = Vector2.Distance(new Vector2(x, y), new Vector2(sz / 2f, sz / 2f)) / (sz / 2f);
            float a = Mathf.Clamp01(1f - d); a = a * a * (3f - 2f * a);
            t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        File.WriteAllBytes(Path.GetFullPath(tp), t.EncodeToPNG());
        AssetDatabase.ImportAsset(tp);
        TextureImporter ti = (TextureImporter)AssetImporter.GetAtPath(tp); ti.alphaIsTransparency = true; ti.mipmapEnabled = false; ti.SaveAndReimport();

        Material m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f);
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.SetOverrideTag("RenderType", "Transparent");
        m.renderQueue = 3000;
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(tp));
        m.SetColor("_BaseColor", Color.white);
        AssetDatabase.CreateAsset(m, mp);
        return m;
    }

    /// <summary>Dark smoke that trails the Wraith (world space, so it hangs in the air behind it).</summary>
    static void AddSmoke(Transform parent, Material mat, float height)
    {
        GameObject g = new GameObject("Smoke");
        g.transform.SetParent(parent, false);
        g.transform.localPosition = new Vector3(0f, height * 0.45f, 0f);
        ParticleSystem ps = g.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, 2.4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.7f, 1.5f);
        main.startSpeed = 0.35f;
        main.startColor = new Color(0.03f, 0.02f, 0.05f, 0.55f);
        main.maxParticles = 60;
        var em = ps.emission; em.rateOverTime = 22f;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.55f;
        var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(-0.1f, 0.1f);   // all three axes must use the same curve mode
        vel.y = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
        vel.z = new ParticleSystem.MinMaxCurve(-0.1f, 0.1f);
        var col = ps.colorOverLifetime; col.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                     new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(0f, 1f) });
        col.color = grad;
        var sol = ps.sizeOverLifetime; sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.5f));
        ParticleSystemRenderer pr = g.GetComponent<ParticleSystemRenderer>();
        pr.sharedMaterial = mat; pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }
}
