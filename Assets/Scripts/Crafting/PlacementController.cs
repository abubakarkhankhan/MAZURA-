using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

/// <summary>
/// Puts crafted Campfires and Spike Traps into the world.
/// [5] campfire, [6] spike trap: a see-through ghost follows the crosshair (green = ok, red = blocked).
/// Left mouse places it, right mouse / Esc cancels, R or the mouse wheel turns it.
/// A small hint under the hotbar shows what you carry. Created automatically.
/// </summary>
public class PlacementController : MonoBehaviour
{
    public static PlacementController Instance { get; private set; }
    public static bool IsPlacing { get { return Instance != null && Instance.item != null; } }

    const float MaxReach = 6.5f;
    const float MinReach = 1.1f;

    class Placeable
    {
        public CraftItem item;
        public string name, prefabPath;
        public float spacing;      // keep this far from other items of the same kind
        public float clearance;    // how far to stand away from a wall when aimed at one
        public GameObject prefab;
    }

    readonly Placeable campfire = new Placeable { item = CraftItem.Campfire, name = "CAMPFIRE", prefabPath = "Crafting/Campfire", spacing = 3.5f, clearance = 0.85f };
    readonly Placeable trap = new Placeable { item = CraftItem.SpikeTrap, name = "SPIKE TRAP", prefabPath = "Crafting/SpikeTrap", spacing = 1.2f, clearance = 0.75f };

    Placeable item;
    GameObject ghost;
    Material ghostMat;
    Renderer[] ghostRends;
    float yaw;
    bool valid;
    Vector3 point;
    ToolType previousTool = ToolType.Axe;

    TextMeshProUGUI hint, carry;
    string shownCarry = "~";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (Instance != null) return;
        new GameObject("PlacementController").AddComponent<PlacementController>();
    }

    void Awake()
    {
        Instance = this;
        RectTransform canvas = CombatSkin.MakeCanvas(transform, "PlacementCanvas", 8);
        hint = CombatSkin.Text(canvas, "", 22f, new Color(1f, 0.9f, 0.7f), TextAlignmentOptions.Center);
        hint.characterSpacing = 4f;
        CombatSkin.Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 300f), new Vector2(1100f, 40f), new Vector2(0.5f, 0f));
        hint.alpha = 0f;

        carry = CombatSkin.Text(canvas, "", 15f, new Color(0.93f, 0.76f, 0.38f), TextAlignmentOptions.Center);
        carry.characterSpacing = 3f;
        CombatSkin.Place(carry.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 128f), new Vector2(700f, 26f), new Vector2(0.5f, 0f));
    }

    void Update()
    {
        ToolController tc = ToolController.Instance;
        CraftingState cs = CraftingState.Instance;
        if (tc == null || cs == null) return;
        Keyboard kb = Keyboard.current;
        Mouse mouse = Mouse.current;
        if (kb == null || mouse == null) return;

        bool free = Cursor.lockState == CursorLockMode.Locked && !CraftingUI.IsOpen;

        // ---- carry hint
        int nc = cs.Count(CraftItem.Campfire), nt = cs.Count(CraftItem.SpikeTrap);
        string c = "";
        if (nc > 0) c += "[5] CAMPFIRE x" + nc;
        if (nc > 0 && nt > 0) c += "     ";
        if (nt > 0) c += "[6] SPIKE TRAP x" + nt;
        if (c != shownCarry) { shownCarry = c; carry.text = c; }
        carry.alpha = (item == null && free) ? 1f : 0f;

        if (item == null)
        {
            hint.alpha = Mathf.MoveTowards(hint.alpha, 0f, Time.unscaledDeltaTime * 6f);
            if (!free) return;
            if (kb.digit5Key.wasPressedThisFrame) Begin(campfire, tc, cs);
            else if (kb.digit6Key.wasPressedThisFrame) Begin(trap, tc, cs);
            return;
        }

        // ---- placing
        hint.alpha = Mathf.MoveTowards(hint.alpha, 1f, Time.unscaledDeltaTime * 8f);
        bool leave = !free || kb.escapeKey.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame
                     || kb.digit1Key.wasPressedThisFrame || kb.digit2Key.wasPressedThisFrame || kb.digit3Key.wasPressedThisFrame
                     || kb.digit4Key.wasPressedThisFrame || kb.tKey.wasPressedThisFrame;
        if (kb.digit5Key.wasPressedThisFrame && item != campfire) { Cancel(tc, false); Begin(campfire, tc, cs); return; }
        if (kb.digit6Key.wasPressedThisFrame && item != trap) { Cancel(tc, false); Begin(trap, tc, cs); return; }
        if (leave) { Cancel(tc, true); return; }
        if (cs.Count(item.item) <= 0) { Cancel(tc, true); return; }

        if (kb.rKey.wasPressedThisFrame) yaw += 45f;
        float wheel = mouse.scroll.ReadValue().y;
        if (Mathf.Abs(wheel) > 0.1f) yaw += Mathf.Sign(wheel) * 15f;

        UpdateGhost(tc);
        hint.text = valid
            ? "PLACE " + item.name + "     [LMB] PLACE    [R] ROTATE    [RMB] CANCEL"
            : item.name + " - NO ROOM HERE     [R] ROTATE    [RMB] CANCEL";
        hint.color = valid ? new Color(1f, 0.9f, 0.7f) : new Color(1f, 0.55f, 0.45f);

        if (mouse.leftButton.wasPressedThisFrame && valid) Place(tc, cs);
    }

    // ------------------------------------------------------------------ start / stop

    void Begin(Placeable p, ToolController tc, CraftingState cs)
    {
        if (cs.Count(p.item) <= 0) return;
        if (p.prefab == null) p.prefab = Resources.Load<GameObject>(p.prefabPath);
        if (p.prefab == null) { HudMessage.Show("Missing prefab: " + p.prefabPath); return; }

        if (!TorchController.IsHeld && tc.CurrentTool != ToolType.None) previousTool = tc.CurrentTool;
        tc.Equip(ToolType.None);
        item = p;
        yaw = tc.transform.eulerAngles.y;

        ghost = Instantiate(p.prefab);
        ghost.name = "PlacementGhost";
        foreach (MonoBehaviour mb in ghost.GetComponentsInChildren<MonoBehaviour>(true)) mb.enabled = false;
        foreach (Collider col in ghost.GetComponentsInChildren<Collider>(true)) col.enabled = false;
        foreach (Light l in ghost.GetComponentsInChildren<Light>(true)) l.enabled = false;
        foreach (ParticleSystem ps in ghost.GetComponentsInChildren<ParticleSystem>(true))
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.gameObject.SetActive(false);
        }
        if (ghostMat == null) ghostMat = MakeGhostMaterial();
        ghostRends = ghost.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer r in ghostRends)
        {
            if (r is ParticleSystemRenderer) continue;
            Material[] ms = new Material[r.sharedMaterials.Length];
            for (int i = 0; i < ms.Length; i++) ms[i] = ghostMat;
            r.sharedMaterials = ms;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        ghost.SetActive(false);
    }

    void Cancel(ToolController tc, bool restoreTool)
    {
        if (ghost != null) Destroy(ghost);
        ghost = null;
        item = null;
        if (restoreTool && !TorchController.IsHeld && tc.CurrentTool == ToolType.None && previousTool != ToolType.None) tc.Equip(previousTool);
    }

    // ------------------------------------------------------------------ ghost

    void UpdateGhost(ToolController tc)
    {
        Camera cam = Camera.main;
        valid = false;
        if (cam == null || ghost == null) return;

        Transform pl = tc.transform;
        Ray ray = cam.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f));
        RaycastHit[] hits = Physics.RaycastAll(ray, 40f, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        bool got = false;
        RaycastHit best = default(RaycastHit);
        foreach (RaycastHit h in hits)
        {
            if (h.collider.transform.IsChildOf(pl)) continue;
            if (h.collider.GetComponentInParent<EnemyBase>() != null) continue;
            best = h; got = true; break;
        }

        Vector3 p;
        Vector3 n = Vector3.up;
        if (got)
        {
            p = best.point; n = best.normal;
            // aimed at a wall, a tree or a rock: stand the item in front of it instead of inside it
            if (n.y < 0.8f)
            {
                Vector3 off = n; off.y = 0f;
                if (off.sqrMagnitude < 0.001f) off = -ray.direction;
                p += off.normalized * item.clearance;
            }
        }
        else p = pl.position + pl.forward * 2.5f;

        // never farther than reach: pull it back towards the player along the ground
        Vector3 flat = p - pl.position; flat.y = 0f;
        bool tooFar = flat.magnitude > MaxReach;
        if (tooFar) p = pl.position + flat.normalized * MaxReach;
        bool tooClose = flat.magnitude < MinReach;
        if (tooClose && flat.sqrMagnitude > 0.001f) p = pl.position + flat.normalized * MinReach;

        // snap to the ground below
        RaycastHit down;
        if (Physics.Raycast(p + Vector3.up * 1.5f, Vector3.down, out down, 4f, ~0, QueryTriggerInteraction.Ignore) && !down.collider.transform.IsChildOf(pl))
        {
            p = down.point; n = down.normal;
        }
        else n = Vector3.down;

        point = p;
        ghost.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, yaw, 0f));
        ghost.SetActive(true);

        bool flatGround = n.y > 0.8f;
        NavMeshHit nh;
        bool onNav = NavMesh.SamplePosition(p, out nh, 0.6f, NavMesh.AllAreas);
        bool clear = !Physics.CheckSphere(p + Vector3.up * 0.62f, 0.4f, ~0, QueryTriggerInteraction.Ignore);
        bool apart = true;
        float sp = item.spacing;
        if (item == campfire) { foreach (Campfire cf in Campfire.All) if ((cf.transform.position - p).sqrMagnitude < sp * sp) apart = false; }
        else { foreach (SpikeTrap st in SpikeTrap.All) if ((st.transform.position - p).sqrMagnitude < sp * sp) apart = false; }

        valid = flatGround && onNav && clear && apart;
        Color col = valid ? new Color(0.35f, 1f, 0.45f, 0.42f) : new Color(1f, 0.3f, 0.25f, 0.42f);
        ghostMat.SetColor("_BaseColor", col);
    }

    static Material MakeGhostMaterial()
    {
        Shader sh = Shader.Find("Universal Render Pipeline/Lit");
        Material m = new Material(sh);
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.SetFloat("_SrcBlend", 5f);
        m.SetFloat("_DstBlend", 10f);
        m.SetFloat("_ZWrite", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.SetOverrideTag("RenderType", "Transparent");
        m.renderQueue = 3000;
        m.SetShaderPassEnabled("ShadowCaster", false);
        m.SetColor("_BaseColor", new Color(0.35f, 1f, 0.45f, 0.42f));
        m.SetFloat("_Smoothness", 0f);
        return m;
    }

    // ------------------------------------------------------------------ place

    void Place(ToolController tc, CraftingState cs)
    {
        if (!cs.Consume(item.item)) { Cancel(tc, true); return; }
        GameObject go = Instantiate(item.prefab, point, Quaternion.Euler(0f, yaw, 0f));
        go.name = item.name == "CAMPFIRE" ? "Campfire_Placed" : "SpikeTrap_Placed";
        PlaceFX.Play(PlaceFX.Thud(), point, 0.9f);
        PlaceFX.Dust(point + Vector3.up * 0.05f, 0.7f, new Color(0.6f, 0.52f, 0.4f, 1f));
        if (tc.animator != null) tc.animator.SetTrigger("Craft");

        // keep placing while there are more in the bag, else leave the mode
        if (cs.Count(item.item) <= 0) Cancel(tc, true);
    }
}
