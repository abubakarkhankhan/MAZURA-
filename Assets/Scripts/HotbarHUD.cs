using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives the HUD hotbar (HUDCanvas/GameHUD/Hotbar) from Hotbar: 5 slots, keys 1-5,
/// each slot shows only what the player really has. Rebuilds the scene's 4 slots into 5 at start.
/// </summary>
public class HotbarHUD : MonoBehaviour
{
    class SlotUI { public GameObject selected; public Image icon, slot; public TextMeshProUGUI key, name, count; }
    Sprite slotNormal, slotEmpty;

    static Sprite HudIcon(HotbarEntry e)
    {
        string n = null;
        switch (e.kind)
        {
            case HotbarKind.Tool: n = e.tool == ToolType.Axe ? "in_item_axe" : e.tool == ToolType.Pickaxe ? "in_item_pickaxe" : Weapons.Info.hudIcon.Substring(4); break;
            case HotbarKind.Torch: n = "in_item_torch"; break;
            case HotbarKind.Place: n = e.item == CraftItem.Campfire ? "in_item_campfire" : e.item == CraftItem.SpikeTrap ? "in_item_spike_trap" : "in_item_wooden_workbench"; break;
        }
        Sprite s = n != null ? Resources.Load<Sprite>("HUD/" + n) : null;
        return s != null ? s : e.Icon;
    }
    readonly SlotUI[] ui = new SlotUI[Hotbar.Size];

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        GameObject bar = GameObject.Find("HUDCanvas/GameHUD/Hotbar");
        if (bar != null && bar.GetComponent<HotbarHUD>() == null) bar.AddComponent<HotbarHUD>();
    }

    void Start()
    {
        GameHUD gh = GetComponentInParent<GameHUD>();
        if (gh != null) gh.toolSelected = null;   // this script owns the selection highlight now

        Transform proto = transform.Find("Tool_1");
        if (proto == null) { enabled = false; return; }
        const float step = 118f;
        for (int i = 0; i < Hotbar.Size; i++)
        {
            Transform t = transform.Find("Tool_" + (i + 1));
            if (t == null || t.Find("Icon") == null || t.Find("Name") == null)
            {
                if (t != null) Destroy(t.gameObject);
                t = Instantiate(proto.gameObject, transform).transform;
                t.name = "Tool_" + (i + 1);
            }
            t.SetSiblingIndex(i);
            RectTransform r = (RectTransform)t;
            r.anchoredPosition = new Vector2(i * step, r.anchoredPosition.y);

            SlotUI s = new SlotUI();
            s.selected = t.Find("Selected").gameObject;
            s.icon = t.Find("Icon").GetComponent<Image>();
            s.key = t.Find("Key").GetComponent<TextMeshProUGUI>();
            s.name = t.Find("Name").GetComponent<TextMeshProUGUI>();
            Transform ct = t.Find("Count");
            if (ct == null)
            {
                GameObject go = Instantiate(s.key.gameObject, t);
                go.name = "Count";
                ct = go.transform;
                RectTransform cr = (RectTransform)ct;
                cr.anchorMin = cr.anchorMax = new Vector2(1f, 1f);
                cr.pivot = new Vector2(1f, 1f);
                cr.anchoredPosition = new Vector2(-7f, -4f);
                cr.sizeDelta = new Vector2(60f, 28f);
            }
            s.count = ct.GetComponent<TextMeshProUGUI>();
            s.count.alignment = TextAlignmentOptions.TopRight;
            s.key.text = (i + 1).ToString();
            Skin(t, s, i);
            ui[i] = s;
        }
        RectTransform me = (RectTransform)transform;
        me.sizeDelta = new Vector2((Hotbar.Size - 1) * step + 104f, me.sizeDelta.y);
        Refresh();
    }

    void Skin(Transform t, SlotUI s, int i)
    {
        if (slotNormal == null) { slotNormal = Resources.Load<Sprite>("HUD/hotbar_slot_normal"); slotEmpty = Resources.Load<Sprite>("HUD/hotbar_slot_disabled"); }
        if (slotNormal == null) return;
        Transform sl = t.Find("Slot");
        if (sl != null) { s.slot = sl.GetComponent<Image>(); s.slot.sprite = slotNormal; s.slot.type = Image.Type.Simple; s.slot.color = Color.white; }
        Image sel = s.selected.GetComponent<Image>();
        Sprite glow = Resources.Load<Sprite>("HUD/hotbar_slot_selected_glowing");
        if (sel != null && glow != null) { sel.sprite = glow; sel.type = Image.Type.Simple; sel.color = Color.white; ((RectTransform)sel.transform).sizeDelta = new Vector2(112f, 106f); }
        RectTransform ir = s.icon.rectTransform;
        ir.anchorMin = ir.anchorMax = new Vector2(0.5f, 0.5f); ir.anchoredPosition = new Vector2(0f, 2f); ir.sizeDelta = new Vector2(72f, 72f);
        // number badge behind the key number
        Sprite badge = Resources.Load<Sprite>("HUD/hotbar_number_badge_" + (i + 1));
        if (badge != null && t.Find("Badge") == null)
        {
            GameObject b = new GameObject("Badge", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            b.transform.SetParent(t, false);
            Image bi = b.GetComponent<Image>(); bi.sprite = badge; bi.raycastTarget = false;
            RectTransform br = (RectTransform)b.transform;
            br.anchorMin = br.anchorMax = new Vector2(0f, 1f); br.pivot = new Vector2(0.5f, 0.5f);
            br.anchoredPosition = new Vector2(12f, -12f); br.sizeDelta = new Vector2(30f, 30f);
            RectTransform kr = s.key.rectTransform;
            kr.anchorMin = kr.anchorMax = new Vector2(0f, 1f); kr.pivot = new Vector2(0.5f, 0.5f);
            kr.anchoredPosition = new Vector2(12f, -12f); kr.sizeDelta = new Vector2(30f, 30f);
            s.key.alignment = TextAlignmentOptions.Center; s.key.fontSize = 18f; s.key.enableAutoSizing = false;
            s.key.transform.SetAsLastSibling();
        }
        s.name.fontSize = 15f; s.name.enableAutoSizing = false;
        RectTransform nr = s.name.rectTransform; nr.anchoredPosition = new Vector2(0f, 3f);
        s.count.transform.SetAsLastSibling();
    }

    void LateUpdate() { Refresh(); }

    void Refresh()
    {
        Hotbar hb = Hotbar.Instance;
        if (hb == null || ui[0] == null) return;
        int sel = hb.Selected;
        for (int i = 0; i < Hotbar.Size; i++)
        {
            SlotUI s = ui[i];
            HotbarEntry e = hb[i];
            bool has = e.kind != HotbarKind.Empty;
            s.icon.enabled = has;
            if (has) { Sprite ic = HudIcon(e); if (s.icon.sprite != ic) s.icon.sprite = ic; }
            if (s.slot != null && slotEmpty != null) s.slot.sprite = has ? slotNormal : slotEmpty;
            s.icon.preserveAspect = true;
            s.name.text = e.Name;
            s.count.text = has ? hb.CountText(i) : "";
            bool on = i == sel && has;
            if (s.selected.activeSelf != on) s.selected.SetActive(on);
        }
    }
}
