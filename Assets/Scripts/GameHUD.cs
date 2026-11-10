using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The in-game HUD: health bar, map pieces, day/night clock, tool hotbar and resource counters.
/// Everything is built in the Hierarchy by  Tools > Mazura > Build HUD  and can be edited by hand.
/// This script only reads the game (HealthManager, MapManager, DayNightManager, ToolController, Inventory)
/// and updates the pictures and numbers, so it never changes how the game works.
/// </summary>
public class GameHUD : MonoBehaviour
{
    [Header("Health")]
    public Image hpFill;
    public TMP_Text hpText;

    [Header("Map pieces")]
    public TMP_Text mapText;
    public Image[] mapPips;

    [Header("Day / night")]
    public Image timeIcon;
    public Sprite sunSprite, moonSprite;
    public TMP_Text timeTitle, timeText;
    public Image timeBar;

    [Header("Tool hotbar (Axe, Pickaxe, Weapon, Empty)")]
    public GameObject[] toolSelected;

    [Header("Resources")]
    public ItemType[] resourceItems;
    public RectTransform[] resourceCells;
    public TMP_Text[] resourceCounts;
    public Image[] resourceIcons;

    [Header("Colours")]
    public Color hpColor = new Color(0.85f, 0.18f, 0.2f, 1f);
    public Color hpLowColor = new Color(1f, 0.35f, 0.1f, 1f);
    public Color pipOn = new Color(1f, 0.82f, 0.35f, 1f);
    public Color pipOff = new Color(0.25f, 0.23f, 0.22f, 1f);
    public Color dayBar = new Color(1f, 0.78f, 0.25f, 1f);
    public Color nightBar = new Color(0.45f, 0.6f, 1f, 1f);

    HealthManager health;
    int[] lastCount;
    float[] punch;

    void Start()
    {
        health = FindAnyObjectByType<HealthManager>();
        int n = resourceItems != null ? resourceItems.Length : 0;
        lastCount = new int[n];
        punch = new float[n];
        for (int i = 0; i < n; i++) lastCount[i] = -1;
    }

    void Update()
    {
        UpdateHealth();
        UpdateMap();
        UpdateTime();
        UpdateTools();
        UpdateResources();
    }

    void UpdateHealth()
    {
        if (health == null) health = FindAnyObjectByType<HealthManager>();
        if (health == null) return;
        float max = Mathf.Max(1f, health.maxHealth);
        float cur = Mathf.Clamp(health.CurrentHealth, 0f, max);
        float k = cur / max;
        if (hpFill != null)
        {
            hpFill.fillAmount = k;
            // a slow pulse when the health is low
            Color c = Color.Lerp(hpLowColor, hpColor, Mathf.InverseLerp(0.15f, 0.45f, k));
            if (k < 0.3f) c = Color.Lerp(c, Color.white, 0.25f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f)));
            hpFill.color = c;
        }
        if (hpText != null) hpText.text = Mathf.CeilToInt(cur) + " / " + Mathf.RoundToInt(max);
    }

    void UpdateMap()
    {
        MapManager m = MapManager.Instance;
        if (m == null) return;
        int got = m.CollectedPieces, max = m.MaxPieces;
        if (mapText != null) mapText.text = got + " / " + max;
        if (mapPips != null)
            for (int i = 0; i < mapPips.Length; i++)
                if (mapPips[i] != null) mapPips[i].color = i < got ? pipOn : pipOff;
    }

    void UpdateTime()
    {
        DayNightManager dn = DayNightManager.Instance;
        if (dn == null) return;
        bool night = dn.IsNight;
        float left = Mathf.Max(0f, dn.PhaseTimeLeft);
        int mm = Mathf.FloorToInt(left / 60f), ss = Mathf.FloorToInt(left % 60f);
        if (timeTitle != null) { timeTitle.text = night ? "NIGHT" : "DAY"; }
        if (timeText != null) timeText.text = mm.ToString("00") + ":" + ss.ToString("00");
        if (timeIcon != null) timeIcon.sprite = night ? moonSprite : sunSprite;
        if (timeBar != null)
        {
            timeBar.fillAmount = Mathf.Clamp01(1f - dn.PhaseProgress);   // time that is left in this phase
            timeBar.color = night ? nightBar : dayBar;
        }
    }

    void UpdateTools()
    {
        ToolController tc = ToolController.Instance;
        if (tc == null || toolSelected == null) return;
        int index = 3;
        if (tc.CurrentTool == ToolType.Axe) index = 0;
        else if (tc.CurrentTool == ToolType.Pickaxe) index = 1;
        else if (tc.CurrentTool == ToolType.Weapon) index = 2;
        for (int i = 0; i < toolSelected.Length; i++)
            if (toolSelected[i] != null && toolSelected[i].activeSelf != (i == index)) toolSelected[i].SetActive(i == index);
    }

    void UpdateResources()
    {
        Inventory inv = Inventory.Instance;
        if (inv == null || resourceItems == null) return;
        for (int i = 0; i < resourceItems.Length; i++)
        {
            int count = inv.Get(resourceItems[i]);
            if (count != lastCount[i])
            {
                if (lastCount[i] >= 0 && count > lastCount[i]) punch[i] = 1f;   // little pop when something is collected
                lastCount[i] = count;
                if (resourceCounts[i] != null) resourceCounts[i].text = count.ToString();
            }
            punch[i] = Mathf.MoveTowards(punch[i], 0f, Time.unscaledDeltaTime * 4f);
            if (resourceCells[i] != null) resourceCells[i].localScale = Vector3.one * (1f + 0.28f * Mathf.Sin(punch[i] * Mathf.PI));
            float a = count > 0 ? 1f : 0.4f;                                      // empty resources look dim
            if (resourceIcons[i] != null) { Color c = resourceIcons[i].color; c.a = a; resourceIcons[i].color = c; }
            if (resourceCounts[i] != null) { Color c = resourceCounts[i].color; c.a = a; resourceCounts[i].color = c; }
        }
    }
}
