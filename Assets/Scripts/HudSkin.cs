using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Puts the MAZURA HUD art (Assets/HUD/Resources/HUD) on the scene HUD at start:
/// health + stamina + map pieces (top-left), day/night clock and supplies list (top-right).
/// GameHUD keeps driving the values; this only re-lays the visuals and adds the stamina bar.
/// </summary>
public class HudSkin : MonoBehaviour
{
    GameHUD hud;
    Image dimSun, dimMoon, staminaFill;
    Image staminaPanel;
    Sprite stamGreen, stamOrange;
    float stamFlash;

    static Sprite S(string n) { return Resources.Load<Sprite>("HUD/" + n); }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        GameObject gh = GameObject.Find("HUDCanvas/GameHUD");
        if (gh != null && gh.GetComponent<HudSkin>() == null) gh.AddComponent<HudSkin>();
    }

    static RectTransform TL(Transform t, Transform parent, float x, float y, float w, float h)
    {
        RectTransform r = (RectTransform)t;
        r.SetParent(parent, false);
        r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
        r.pivot = new Vector2(0f, 1f);
        r.anchoredPosition = new Vector2(x, -y);
        r.sizeDelta = new Vector2(w, h);
        r.localScale = Vector3.one;
        r.localRotation = Quaternion.identity;
        return r;
    }

    static Image NewImg(Transform parent, string name, Sprite s, float x, float y, float w, float h)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        Image im = go.GetComponent<Image>();
        im.sprite = s;
        im.raycastTarget = false;
        TL(go.transform, parent, x, y, w, h);
        return im;
    }

    static void Hide(Transform t)
    {
        if (t == null) return;
        if (Application.isPlaying) t.gameObject.SetActive(false);
        else Object.DestroyImmediate(t.gameObject);
    }

    static Image MakeFill(Image holder, Sprite fill, float padX, float padY)
    {
        holder.sprite = null;
        holder.color = new Color(0f, 0f, 0f, 0f);
        Image f;
        if (holder.transform.childCount > 0 && holder.transform.GetChild(0).GetComponent<Image>() != null)
            f = holder.transform.GetChild(0).GetComponent<Image>();
        else
        {
            GameObject go = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(holder.transform, false);
            f = go.GetComponent<Image>();
        }
        RectTransform r = f.rectTransform;
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = Vector2.zero;
        r.sizeDelta = new Vector2(-padX * 2f, -padY * 2f);
        f.sprite = fill;
        f.type = Image.Type.Filled;
        f.fillMethod = Image.FillMethod.Horizontal;
        f.fillOrigin = 0;
        f.preserveAspect = false;
        f.color = Color.white;
        f.raycastTarget = false;
        return f;
    }

    TextMeshProUGUI CloneText(TMP_Text like, Transform parent, string name, string text, float x, float y, float w, float h, float size, TextAlignmentOptions al)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        TextMeshProUGUI t = go.GetComponent<TextMeshProUGUI>();
        if (like != null) { t.font = like.font; t.fontSharedMaterial = like.fontSharedMaterial; }
        t.text = text; t.fontSize = size; t.alignment = al; t.color = new Color(1f, 0.86f, 0.55f); t.raycastTarget = false;
        TL(go.transform, parent, x, y, w, h);
        return t;
    }

    static void Txt(TMP_Text t, Transform parent, float x, float y, float w, float h, float size, TextAlignmentOptions al)
    {
        if (t == null) return;
        TL(t.transform, parent, x, y, w, h);
        t.fontSize = size; t.enableAutoSizing = false; t.alignment = al;
    }

    void Start()
    {
        if (transform.Find("StatusPanel/HealthPanel") != null) Cache();
        else Apply();
    }

    void Cache()
    {
        hud = GetComponent<GameHUD>();
        stamGreen = S("stamina_fill_yellow_green"); stamOrange = S("stamina_fill_low_orange");
        Transform f = transform.Find("StatusPanel/StaminaBar/Fill");
        if (f != null) staminaFill = f.GetComponent<Image>();
        Transform a = transform.Find("TimePanel/DimSun"), b = transform.Find("TimePanel/DimMoon");
        if (a != null) dimSun = a.GetComponent<Image>();
        if (b != null) dimMoon = b.GetComponent<Image>();
    }

    /// <summary>Builds the new HUD look. Also runs in the editor (bakes it into the scene).</summary>
    [ContextMenu("Apply HUD Skin")]
    public void Apply()
    {
        hud = GetComponent<GameHUD>();
        if (hud == null || S("health_panel") == null) { enabled = false; return; }

        // GameHUD tints: the art already has its colours
        hud.hpColor = Color.white;
        hud.hpLowColor = new Color(1f, 0.72f, 0.55f);
        hud.pipOn = Color.white;
        hud.pipOff = new Color(1f, 1f, 1f, 0f);
        hud.dayBar = Color.white;
        hud.nightBar = new Color(0.65f, 0.78f, 1f);

        BuildStatus();
        BuildClock();
        BuildSupplies();
    }

    void BuildStatus()
    {
        Transform sp = transform.Find("StatusPanel");
        if (sp == null) return;
        RectTransform r = (RectTransform)sp;
        r.anchorMin = r.anchorMax = new Vector2(0f, 1f); r.pivot = new Vector2(0f, 1f);
        r.anchoredPosition = new Vector2(22f, -18f);
        r.sizeDelta = new Vector2(450f, 280f);
        Hide(sp.Find("Frame")); Hide(sp.Find("HeartIcon")); Hide(sp.Find("MapIcon")); Hide(sp.Find("MapLabel")); Hide(sp.Find("MapText"));

        // health
        const float k = 0.75f;
        Image hp = NewImg(sp, "HealthPanel", S("health_panel"), 0f, 0f, 590f * k, 136f * k);
        hp.transform.SetAsFirstSibling();
        Transform bg = sp.Find("HealthBarBg");
        if (bg != null)
        {
            TL(bg, sp, 151f * k, 37f * k, 402f * k, 57f * k);
            MakeFill(bg.GetComponent<Image>(), S("health_fill_red"), 3f, 4f);
            Transform ht = bg.Find("HealthText");
            if (ht != null)
            {
                ht.SetAsLastSibling();
                TextMeshProUGUI t = ht.GetComponent<TextMeshProUGUI>();
                if (t != null) { t.fontSize = 26f; t.enableAutoSizing = false; t.outlineWidth = 0.2f; t.outlineColor = new Color32(40, 10, 5, 255); }
            }
        }

        // stamina
        const float k2 = 0.72f;
        float y2 = 136f * k + 2f;
        staminaPanel = NewImg(sp, "StaminaPanel", S("stamina_panel_with_boot"), 6f, y2, 470f * k2, 105f * k2);
        Image sbg = NewImg(sp, "StaminaBar", null, 6f + 86f * k2, y2 + 23f * k2, 366f * k2, 40f * k2);
        stamGreen = S("stamina_fill_yellow_green"); stamOrange = S("stamina_fill_low_orange");
        staminaFill = MakeFill(sbg, stamGreen, 2f, 3f);

        // map pieces
        const float k3 = 0.62f;
        float y3 = y2 + 105f * k2 + 2f;
        NewImg(sp, "MapPanel", S("map_panel_row"), 0f, y3, 510f * k3, 136f * k3);
        float[] cx = { 203f, 279f, 354f, 428f };
        Sprite gold = S("map_socket_gold");
        for (int i = 0; i < 4; i++)
        {
            Transform pip = sp.Find("Pip" + (i + 1));
            if (pip == null) continue;
            TL(pip, sp, (cx[i] - 32f) * k3, y3 + (79f - 32f) * k3, 64f * k3, 64f * k3);
            Image im = pip.GetComponent<Image>();
            im.sprite = gold; im.preserveAspect = true; im.type = Image.Type.Simple;
            pip.SetAsLastSibling();
        }
    }

    void BuildClock()
    {
        Transform tp = transform.Find("TimePanel");
        if (tp == null) return;
        const float k = 0.56f;
        RectTransform r = (RectTransform)tp;
        r.anchorMin = r.anchorMax = new Vector2(1f, 1f); r.pivot = new Vector2(1f, 1f);
        r.anchoredPosition = new Vector2(-22f, -18f);
        r.sizeDelta = new Vector2(695f * k, 142f * k);
        Hide(tp.Find("Frame"));
        Image icon = tp.Find("TimeIcon") != null ? tp.Find("TimeIcon").GetComponent<Image>() : null;
        if (icon != null) { hud.timeIcon = null; Hide(icon.transform); }

        Image panel = NewImg(tp, "ClockPanel", S("day_night_clock_panel"), 0f, 0f, 695f * k, 142f * k);
        panel.transform.SetAsFirstSibling();
        Sprite dim = S("dim_circle");
        dimSun = NewImg(tp, "DimSun", dim, 26f * k, 16f * k, 111f * k, 111f * k);
        dimMoon = NewImg(tp, "DimMoon", dim, 566f * k, 19f * k, 107f * k, 107f * k);

        Txt(hud.timeTitle, tp, 165f * k, 24f * k, 140f * k, 70f * k, 22f, TextAlignmentOptions.MidlineLeft);
        Txt(hud.timeText, tp, 290f * k, 18f * k, 245f * k, 82f * k, 38f, TextAlignmentOptions.MidlineRight);

        Transform bar = tp.Find("TimeBarBg");
        if (bar != null)
        {
            TL(bar, tp, 177f * k, 105f * k, 344f * k, 20f * k);
            MakeFill(bar.GetComponent<Image>(), S("day_night_gold_progress_fill"), 1f, 1f);
        }
    }

    void BuildSupplies()
    {
        Transform rp = transform.Find("ResourcePanel");
        if (rp == null) return;
        const float k = 0.55f;
        RectTransform r = (RectTransform)rp;
        r.anchorMin = r.anchorMax = new Vector2(1f, 1f); r.pivot = new Vector2(1f, 1f);
        r.anchoredPosition = new Vector2(-22f, -112f);
        r.sizeDelta = new Vector2(340f * k, 645f * k);
        Hide(rp.Find("Frame"));
        Image panel = NewImg(rp, "SuppliesPanel", S("resource_list_panel_7_rows"), 0f, 0f, 340f * k, 645f * k);
        panel.transform.SetAsFirstSibling();
        CloneText(hud.hpText, rp, "Title", "SUPPLIES", 0f, 40f * k, 340f * k, 50f * k, 19f, TextAlignmentOptions.Center);

        string[] names = { "Wood", "Stone", "Herb", "Ruby", "Emerald", "Sapphire", "Amber" };
        string[] spr = { "in_resource_wood_log", "in_resource_stone", "in_resource_green_herb", "in_resource_red_ruby", "in_resource_green_emerald", "in_resource_blue_sapphire", "in_resource_orange_amber" };
        for (int i = 0; i < names.Length; i++)
        {
            Transform cell = rp.Find(names[i]);
            if (cell == null) continue;
            float cy = (98f + 67f * i + 33.5f) * k;
            RectTransform cr = (RectTransform)cell;
            cr.anchorMin = cr.anchorMax = new Vector2(0f, 1f);
            cr.pivot = new Vector2(0.5f, 0.5f);
            cr.anchoredPosition = new Vector2(170f * k, -cy);
            cr.sizeDelta = new Vector2(270f * k, 62f * k);
            Transform ic = cell.Find("Icon");
            if (ic != null)
            {
                RectTransform ir = (RectTransform)ic;
                ir.anchorMin = ir.anchorMax = new Vector2(0f, 0.5f); ir.pivot = new Vector2(0f, 0.5f);
                ir.anchoredPosition = new Vector2(4f, 0f); ir.sizeDelta = new Vector2(30f, 30f);
                Image im = ic.GetComponent<Image>(); Sprite s = S(spr[i]);
                if (s != null) { im.sprite = s; im.preserveAspect = true; }
            }
            Transform ct = cell.Find("Count");
            if (ct != null)
            {
                RectTransform tr = (RectTransform)ct;
                tr.anchorMin = tr.anchorMax = new Vector2(1f, 0.5f); tr.pivot = new Vector2(1f, 0.5f);
                tr.anchoredPosition = new Vector2(-6f, 0f); tr.sizeDelta = new Vector2(80f, 32f);
                TextMeshProUGUI t = ct.GetComponent<TextMeshProUGUI>();
                if (t != null) { t.fontSize = 24f; t.enableAutoSizing = false; t.alignment = TextAlignmentOptions.MidlineRight; }
            }
        }
    }

    void LateUpdate()
    {
        DayNightManager dn = DayNightManager.Instance;
        if (dn != null && dimSun != null)
        {
            bool night = dn.IsNight;
            dimSun.enabled = night;
            dimMoon.enabled = !night;
        }

        Stamina st = Stamina.Instance;
        if (st != null && staminaFill != null)
        {
            float n = st.Normalized;
            staminaFill.fillAmount = n;
            bool low = st.Exhausted || n < 0.3f;
            staminaFill.sprite = low ? stamOrange : stamGreen;
            if (st.Exhausted)
            {
                stamFlash += Time.unscaledDeltaTime * 6f;
                staminaFill.color = Color.Lerp(Color.white, new Color(1f, 0.5f, 0.4f), 0.5f + 0.5f * Mathf.Sin(stamFlash));
            }
            else staminaFill.color = Color.white;
        }
    }
}
