using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Minecraft-style item drop. When a tree or stone breaks, small items pop out, land on the ground,
/// spin and bob, and fly into the player when he walks close. Only then they are added to the inventory.
/// Spawned by ResourceNode through WorldPickup.Spawn(...). Nothing to set up in the scene.
/// </summary>
public class WorldPickup : MonoBehaviour
{
    public ItemType item;
    public int amount = 1;

    const float PickupDelay = 0.55f;   // seconds before it can be collected (so it is seen flying out)
    const float MagnetRange = 3.2f;    // starts flying to the player inside this distance
    const float CollectRange = 0.55f;
    const float Lifetime = 240f;       // disappears after 4 minutes if nobody takes it
    const float Gravity = 18f;

    Vector3 velocity;
    bool grounded, magnet;
    float age, groundY, spinSpeed, bobPhase;
    Transform visual;

    static AudioClip popClip;
    static Material woodMat, woodEndMat;

    // ------------------------------------------------------------------ spawning

    /// <summary>Drops items at pos. pieces are spread out like a little burst.</summary>
    public static void Spawn(ItemType item, int amount, Vector3 pos, Material stoneMat, Material gemMat, Mesh gemMesh, Vector3 bias = default(Vector3))
    {
        if (amount <= 0) return;
        int pieces = Mathf.Clamp(amount, 1, 5);
        int each = amount / pieces, extra = amount % pieces;
        for (int i = 0; i < pieces; i++)
        {
            int a = each + (i < extra ? 1 : 0);
            if (a <= 0) continue;
            GameObject go = new GameObject("Pickup_" + item);
            go.transform.position = pos + Random.insideUnitSphere * 0.15f;
            WorldPickup p = go.AddComponent<WorldPickup>();
            p.item = item;
            p.amount = a;
            p.BuildVisual(stoneMat, gemMat, gemMesh);
            Vector2 r = Random.insideUnitCircle.normalized * Random.Range(0.8f, 1.8f);
            Vector3 h = new Vector3(r.x, 0f, r.y) + bias * Random.Range(1.2f, 2.2f);   // mostly towards the player
            p.velocity = new Vector3(h.x, Random.Range(4.2f, 6f), h.z);
        }
    }

    void BuildVisual(Material stoneMat, Material gemMat, Mesh gemMesh)
    {
        visual = new GameObject("Visual").transform;
        visual.SetParent(transform, false);
        spinSpeed = Random.Range(80f, 120f) * (Random.value < 0.5f ? -1f : 1f);
        bobPhase = Random.value * 6.28f;

        if (item == ItemType.Wood)
        {
            if (woodMat == null) woodMat = FindMat("DarkWood");
            if (woodEndMat == null) woodEndMat = FindMat("LightWood");
            Transform log = Prim(PrimitiveType.Cylinder, woodMat, visual);
            log.localRotation = Quaternion.Euler(0f, 0f, 90f);
            log.localScale = new Vector3(0.17f, 0.17f, 0.17f);        // a short log lying on its side
            for (int s = -1; s <= 1; s += 2)
            {
                Transform cap = Prim(PrimitiveType.Cylinder, woodEndMat != null ? woodEndMat : woodMat, visual);
                cap.localRotation = Quaternion.Euler(0f, 0f, 90f);
                cap.localPosition = new Vector3(s * 0.17f, 0f, 0f);
                cap.localScale = new Vector3(0.145f, 0.004f, 0.145f);   // lighter rings on both ends
            }
        }
        else if (item == ItemType.Stone || gemMesh == null)
        {
            Transform rock = Prim(PrimitiveType.Cube, stoneMat, visual);
            rock.localScale = Vector3.one * 0.2f;
            rock.localRotation = Random.rotation;
            if (gemMat != null && item != ItemType.Stone)
            {
                Transform g = Prim(PrimitiveType.Cube, gemMat, visual);
                g.localScale = Vector3.one * 0.14f;
                g.localRotation = Random.rotation;
            }
        }
        else
        {
            // gem: the same crystal mesh as in the ore, small and shiny
            GameObject g = new GameObject("Gem");
            g.transform.SetParent(visual, false);
            g.AddComponent<MeshFilter>().sharedMesh = gemMesh;
            MeshRenderer mr = g.AddComponent<MeshRenderer>();
            mr.sharedMaterial = gemMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Bounds b = gemMesh.bounds;
            float ext = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            g.transform.localScale = Vector3.one * (0.3f / Mathf.Max(0.00001f, ext));
            g.transform.localPosition = -b.center * g.transform.localScale.x;
            // a tiny light makes gems sparkle in the dark forest
            Light l = g.AddComponent<Light>();
            l.type = LightType.Point; l.range = 1.6f; l.intensity = 1.2f;
            l.color = Inventory.ColorFor(item);
            l.shadows = LightShadows.None;
        }
    }

    static Transform Prim(PrimitiveType t, Material m, Transform parent)
    {
        GameObject go = GameObject.CreatePrimitive(t);
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        Renderer r = go.GetComponent<Renderer>();
        if (m != null) r.sharedMaterial = m;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        return go.transform;
    }

    static Material FindMat(string n)
    {
        foreach (Renderer r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            foreach (Material m in r.sharedMaterials)
                if (m != null && m.name == n) return m;
        return null;
    }

    // ------------------------------------------------------------------ behaviour

    void Start()
    {
        groundY = FindGround(transform.position);
    }

    float FindGround(Vector3 p)
    {
        RaycastHit hit;
        int mask = ~(1 << 8);   // not the player
        if (Physics.Raycast(p + Vector3.up * 0.5f, Vector3.down, out hit, 10f, mask, QueryTriggerInteraction.Ignore))
            return hit.point.y;
        return 0f;
    }

    void Update()
    {
        age += Time.deltaTime;
        if (age > Lifetime) { Destroy(gameObject); return; }

        ToolController player = ToolController.Instance;
        Vector3 target = player != null ? player.transform.position + Vector3.up * 0.9f : transform.position;

        if (!magnet && player != null && age > PickupDelay)
        {
            Vector3 d = target - transform.position;
            if (d.magnitude < MagnetRange && !WallBetween(transform.position, target)) magnet = true;
        }

        if (magnet && player != null)
        {
            // fly into the player, faster the closer it gets
            Vector3 d = target - transform.position;
            float dist = d.magnitude;
            if (dist < CollectRange) { Collect(); return; }
            float speed = Mathf.Lerp(14f, 5f, Mathf.Clamp01(dist / MagnetRange));
            transform.position += d / dist * Mathf.Min(dist, speed * Time.deltaTime);
            visual.localScale = Vector3.one * Mathf.Lerp(0.6f, 1f, Mathf.Clamp01(dist / 1.5f));
        }
        else if (!grounded)
        {
            velocity.y -= Gravity * Time.deltaTime;
            Vector3 next = transform.position + velocity * Time.deltaTime;
            float rest = groundY + 0.18f;
            if (next.y <= rest && velocity.y < 0f)
            {
                next.y = rest;
                if (velocity.y < -3f) { velocity.y *= -0.32f; velocity.x *= 0.5f; velocity.z *= 0.5f; }   // small bounce
                else { grounded = true; velocity = Vector3.zero; }
            }
            next = BlockedBy(transform.position, next);
            transform.position = next;
            visual.Rotate(velocity.magnitude * 60f * Time.deltaTime, spinSpeed * Time.deltaTime, 0f, Space.Self);
        }
        else
        {
            // resting: spin and bob like a Minecraft item
            visual.localRotation = Quaternion.Euler(0f, age * spinSpeed, 0f);
            visual.localPosition = new Vector3(0f, 0.06f * Mathf.Sin(age * 2.4f + bobPhase), 0f);
        }
    }

    // stops the flight at a wall and bounces back a little
    Vector3 BlockedBy(Vector3 from, Vector3 to)
    {
        Vector3 d = to - from;
        float len = d.magnitude;
        if (len < 0.0001f) return to;
        RaycastHit[] hits = Physics.SphereCastAll(from, 0.12f, d / len, len, ~(1 << 8), QueryTriggerInteraction.Ignore);
        float nearest = float.MaxValue; RaycastHit best = default(RaycastHit);
        foreach (RaycastHit h in hits)
            if (ResourceNode.IsWall(h.collider) && h.distance < nearest) { nearest = h.distance; best = h; }
        if (nearest == float.MaxValue) return to;
        Vector3 n = best.normal; n.y = 0f; n.Normalize();
        Vector3 hv = new Vector3(velocity.x, 0f, velocity.z);
        hv = Vector3.Reflect(hv, n) * 0.3f;
        velocity = new Vector3(hv.x, velocity.y, hv.z);
        Vector3 stop = from + d / len * Mathf.Max(0f, nearest - 0.02f) + n * 0.05f;
        stop.y = to.y;
        return stop;
    }

    static bool WallBetween(Vector3 a, Vector3 b)
    {
        Vector3 d = b - a;
        foreach (RaycastHit h in Physics.RaycastAll(a, d.normalized, d.magnitude, ~(1 << 8), QueryTriggerInteraction.Ignore))
            if (ResourceNode.IsWall(h.collider)) return true;
        return false;
    }

    void Collect()
    {
        if (Inventory.Instance != null) Inventory.Instance.Add(item, amount);
        PlayPop(transform.position);
        Destroy(gameObject);
    }

    static void PlayPop(Vector3 pos)
    {
        if (popClip == null)
        {
            const int rate = 44100;
            int n = (int)(rate * 0.09f);
            float[] data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate, k = i / (float)n;
                float f = Mathf.Lerp(520f, 1250f, k);                      // rising blip
                data[i] = Mathf.Sin(6.2832f * f * t) * Mathf.Exp(-t * 32f) * 0.55f;
            }
            popClip = AudioClip.Create("pop", n, 1, rate, false);
            popClip.SetData(data, 0);
        }
        GameObject go = new GameObject("SFX_Pop");
        go.transform.position = pos;
        AudioSource a = go.AddComponent<AudioSource>();
        a.clip = popClip; a.volume = 0.5f; a.pitch = Random.Range(0.9f, 1.25f); a.spatialBlend = 0.4f;
        a.Play();
        Destroy(go, 0.3f);
    }
}
