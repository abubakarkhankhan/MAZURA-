using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Big boss health bar across the lower middle of the screen (action-RPG style). Created automatically.
/// Appears when a boss is fighting you (or is close and in view): the name in wide gold capitals,
/// a long crimson bar with a draining damage chunk, a notch at 50% where phase 2 starts,
/// and "ENRAGED" once it is in phase 2. Slides away a moment after the boss dies.
/// </summary>
public class BossBar : MonoBehaviour
{
    static BossBar instance;

    const float BarW = 860f, BarH = 16f;

    RectTransform root;
    CanvasGroup group;
    TextMeshProUGUI title, tagText, hpText;
    Image fill, lag, flash;
    EnemyBase boss;
    float alpha, lagValue = 1f, pop, lastHealth, deadAt = -1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (instance != null) return;
        instance = new GameObject("BossBar").AddComponent<BossBar>();
    }

    void Awake()
    {
        RectTransform canvas = CombatSkin.MakeCanvas(transform, "BossBarCanvas", 7);
        GameObject go = new GameObject("BossBar", typeof(RectTransform));
        root = (RectTransform)go.transform;
        root.SetParent(canvas, false);
        CombatSkin.Place(root, new Vector2(0.5f, 0f), new Vector2(0f, 185f), new Vector2(BarW + 40f, 70f), new Vector2(0.5f, 0f));
        group = go.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.alpha = 0f;

        title = CombatSkin.Text(root, "", 24f, CombatSkin.Gold, TextAlignmentOptions.BottomLeft);
        title.characterSpacing = 8f;
        CombatSkin.Place(title.rectTransform, new Vector2(0f, 0f), new Vector2(20f, BarH + 22f), new Vector2(600f, 34f), new Vector2(0f, 0f));

        tagText = CombatSkin.Text(root, "", 15f, new Color(1f, 0.45f, 0.2f), TextAlignmentOptions.BottomRight);
        tagText.characterSpacing = 6f;
        CombatSkin.Place(tagText.rectTransform, new Vector2(1f, 0f), new Vector2(-20f, BarH + 24f), new Vector2(240f, 28f), new Vector2(1f, 0f));

        Image frame = CombatSkin.Frame(root, BarW + 14f, BarH + 14f);
        CombatSkin.Place(frame.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(BarW + 14f, BarH + 14f), new Vector2(0.5f, 0f));
        lag = CombatSkin.Fill(frame.rectTransform, BarW, BarH, CombatSkin.Chunk);
        fill = CombatSkin.Fill(frame.rectTransform, BarW, BarH, CombatSkin.Crimson);
        flash = CombatSkin.Fill(frame.rectTransform, BarW, BarH, new Color(1f, 1f, 1f, 0f));

        // phase-2 notch at 50%
        GameObject notch = new GameObject("Notch", typeof(RectTransform));
        notch.transform.SetParent(frame.rectTransform, false);
        Image n = notch.AddComponent<Image>();
        n.color = new Color(0.95f, 0.8f, 0.45f, 0.9f);
        n.raycastTarget = false;
        CombatSkin.Place(n.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(2f, BarH + 8f), new Vector2(0.5f, 0.5f));

        hpText = CombatSkin.Text(frame.rectTransform, "", 13f, Color.white, TextAlignmentOptions.Center);
        CombatSkin.Place(hpText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 1f), new Vector2(BarW, BarH + 6f), new Vector2(0.5f, 0.5f));
    }

    /// <summary>The boss currently shown (other UI can skip it).</summary>
    public static EnemyBase Current { get { return instance != null && instance.alpha > 0.05f ? instance.boss : null; } }

    EnemyBase Pick()
    {
        ToolController tc = ToolController.Instance;
        if (tc == null) return null;
        EnemyBase best = null;
        float bestD = float.MaxValue;
        foreach (EnemyBase e in EnemyBase.All)
        {
            if (e == null || e.IsDead || e.data == null || !e.data.isBoss) continue;
            float d = Vector3.Distance(tc.transform.position, e.transform.position);
            bool engaged = e.IsHuntingPlayer || e.TimeSinceHit < 8f || (d < 26f && AimAssist.InSight(e));
            if (!engaged) continue;
            if (d < bestD) { bestD = d; best = e; }
        }
        return best;
    }

    void LateUpdate()
    {
        float dt = Time.unscaledDeltaTime;
        EnemyBase pick = Pick();
        if (pick != null && pick != boss)
        {
            boss = pick;
            lagValue = Mathf.Clamp01(boss.Health / boss.data.maxHealth);
            lastHealth = boss.Health;
            deadAt = -1f;
            title.text = boss.data.displayName.ToUpperInvariant();
        }

        bool show = boss != null && (pick == boss || (boss.IsDead && deadAt > 0f && Time.unscaledTime - deadAt < 1.6f));
        if (boss != null && boss.IsDead && deadAt < 0f) { deadAt = Time.unscaledTime; show = true; }
        alpha = Mathf.MoveTowards(alpha, show ? 1f : 0f, dt * (show ? 3f : 1.5f));
        group.alpha = alpha;
        root.anchoredPosition = new Vector2(0f, 185f - 24f * (1f - alpha));
        if (boss == null) return;

        float frac = boss.IsDead ? 0f : Mathf.Clamp01(boss.Health / boss.data.maxHealth);
        if (boss.Health < lastHealth - 0.01f) pop = 1f;
        lastHealth = boss.Health;
        pop = Mathf.MoveTowards(pop, 0f, dt * 3f);
        if (boss.TimeSinceHit > 0.5f || boss.IsDead) lagValue = Mathf.MoveTowards(lagValue, frac, dt * 0.35f);
        if (lagValue < frac) lagValue = frac;

        fill.fillAmount = frac;
        lag.fillAmount = lagValue;
        flash.fillAmount = frac;
        flash.color = new Color(1f, 1f, 1f, pop * 0.7f);
        fill.color = CombatSkin.HealthColor(frac);

        BossController bc = boss.GetComponent<BossController>();
        bool enraged = bc != null && bc.InPhase2;
        tagText.text = boss.IsDead ? "" : (enraged ? "ENRAGED" : "");
        tagText.color = new Color(1f, 0.45f, 0.2f, 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 6f));
        title.color = enraged ? new Color(1f, 0.5f, 0.32f) : CombatSkin.Gold;
        hpText.text = boss.IsDead ? "" : Mathf.CeilToInt(Mathf.Max(0f, boss.Health)) + "  /  " + Mathf.CeilToInt(boss.data.maxHealth);
    }
}
