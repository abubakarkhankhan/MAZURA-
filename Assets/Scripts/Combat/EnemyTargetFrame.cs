using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Top-centre "target" panel, like an action-RPG boss/target bar. Created automatically when the game starts.
/// Shows the enemy you are fighting: the last one you hit, or else the nearest one hunting you.
/// Name in gold capitals, a tagText (FOREST / DUNGEON / NIGHT / BOSS), a wide health bar with a draining
/// damage chunk and white hit flash, and "136 / 160". Fades in, and fades out a few seconds after the fight.
/// </summary>
public class EnemyTargetFrame : MonoBehaviour
{
    static EnemyTargetFrame instance;

    /// <summary>The enemy shown in the frame (other UI can skip it).</summary>
    public static EnemyBase Current { get { return instance != null ? instance.target : null; } }
    public static bool Visible { get { return instance != null && instance.alpha > 0.5f; } }

    const float W = 520f;
    const float BarW = 460f;
    const float BarH = 16f;

    RectTransform root;
    CanvasGroup group;
    TextMeshProUGUI title, tagText, hpText;
    Image fill, lag, flash;

    EnemyBase target, lastHitByPlayer;
    float alpha, lagValue = 1f, pop, lastHealth, lostTime;
    int shownHp = -999;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (instance != null) return;
        instance = new GameObject("EnemyTargetFrame").AddComponent<EnemyTargetFrame>();
    }

    void Awake()
    {
        RectTransform canvas = CombatSkin.MakeCanvas(transform, "TargetFrameCanvas", 6);

        GameObject go = new GameObject("TargetFrame", typeof(RectTransform));
        root = (RectTransform)go.transform;
        root.SetParent(canvas, false);
        CombatSkin.Place(root, new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(W, 92f), new Vector2(0.5f, 1f));
        group = go.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.alpha = 0f;

        Image panel = CombatSkin.Panel(root);
        RectTransform prt = panel.rectTransform;
        prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one; prt.offsetMin = prt.offsetMax = Vector2.zero;

        title = CombatSkin.Text(root, "", 22f, CombatSkin.Gold, TextAlignmentOptions.Left);
        title.characterSpacing = 6f;
        CombatSkin.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(30f, -12f), new Vector2(360f, 30f), new Vector2(0f, 1f));

        tagText = CombatSkin.Text(root, "", 13f, new Color(0.75f, 0.68f, 0.55f), TextAlignmentOptions.Right);
        tagText.characterSpacing = 4f;
        CombatSkin.Place(tagText.rectTransform, new Vector2(1f, 1f), new Vector2(-30f, -16f), new Vector2(160f, 24f), new Vector2(1f, 1f));

        Image frame = CombatSkin.Frame(root, BarW + 12f, BarH + 12f);
        CombatSkin.Place(frame.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(BarW + 12f, BarH + 12f), new Vector2(0.5f, 0f));
        lag = CombatSkin.Fill(frame.rectTransform, BarW, BarH, CombatSkin.Chunk);
        fill = CombatSkin.Fill(frame.rectTransform, BarW, BarH, CombatSkin.Crimson);
        flash = CombatSkin.Fill(frame.rectTransform, BarW, BarH, new Color(1f, 1f, 1f, 0f));

        hpText = CombatSkin.Text(frame.rectTransform, "", 14f, Color.white, TextAlignmentOptions.Center);
        CombatSkin.Place(hpText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 1f), new Vector2(BarW, BarH + 8f), new Vector2(0.5f, 0.5f));
    }

    void OnEnable() { EnemyBase.OnAnyDamaged += OnDamaged; }
    void OnDisable() { EnemyBase.OnAnyDamaged -= OnDamaged; }

    void OnDamaged(EnemyBase e, float amount) { lastHitByPlayer = e; }

    EnemyBase Pick()
    {
        if (lastHitByPlayer != null && !lastHitByPlayer.IsDead && lastHitByPlayer.TimeSinceHit < 6f && !lastHitByPlayer.data.isBoss && AimAssist.InSight(lastHitByPlayer)) return lastHitByPlayer;
        EnemyBase aimed = AimAssist.Target;
        if (aimed != null && !aimed.data.isBoss && (aimed.IsHuntingPlayer || aimed.TimeSinceHit < 6f)) return aimed;

        ToolController tc = ToolController.Instance;
        if (tc == null) return null;
        EnemyBase best = null;
        float bestD = 15f;
        foreach (EnemyBase e in EnemyBase.All)
        {
            if (e == null || e.IsDead || e.data == null) continue;
            if (!e.IsHuntingPlayer || e.data.isBoss) continue;   // bosses use the big BossBar
            if (!AimAssist.InSight(e)) continue;   // never through walls
            float d = Vector3.Distance(tc.transform.position, e.transform.position);
            if (e.data.isBoss) d *= 0.4f;   // bosses win
            if (d < bestD) { bestD = d; best = e; }
        }
        return best;
    }

    void LateUpdate()
    {
        float dt = Time.unscaledDeltaTime;
        EnemyBase pick = Pick();

        if (pick != null && pick != target)
        {
            target = pick;
            lagValue = Mathf.Clamp01(target.Health / target.data.maxHealth);
            lastHealth = target.Health;
            shownHp = -999;
            Refresh();
        }

        bool show = target != null && pick == target;
        if (target != null && target.IsDead)
        {
            // keep showing an empty bar for a moment so the kill reads
            if (lostTime <= 0f) lostTime = Time.unscaledTime;
            show = Time.unscaledTime - lostTime < 1.1f;
        }
        else lostTime = 0f;

        alpha = Mathf.MoveTowards(alpha, show ? 1f : 0f, dt * (show ? 5f : 2f));
        group.alpha = alpha;
        root.anchoredPosition = new Vector2(0f, -22f + 10f * (1f - alpha));
        if (target == null) return;

        float frac = target.IsDead ? 0f : Mathf.Clamp01(target.Health / target.data.maxHealth);
        if (target.Health < lastHealth - 0.01f) pop = 1f;
        lastHealth = target.Health;
        pop = Mathf.MoveTowards(pop, 0f, dt * 3f);
        if (target.TimeSinceHit > 0.45f || target.IsDead) lagValue = Mathf.MoveTowards(lagValue, frac, dt * 0.45f);
        if (lagValue < frac) lagValue = frac;

        fill.fillAmount = frac;
        lag.fillAmount = lagValue;
        flash.fillAmount = frac;
        flash.color = new Color(1f, 1f, 1f, pop * 0.8f);
        fill.color = CombatSkin.HealthColor(frac);
        root.localScale = Vector3.one * (1f + 0.025f * pop);

        int hp = Mathf.CeilToInt(Mathf.Max(0f, target.IsDead ? 0f : target.Health));
        if (hp != shownHp)
        {
            shownHp = hp;
            hpText.text = target.IsDead ? "DEFEATED" : hp + "  /  " + Mathf.CeilToInt(target.data.maxHealth);
        }
    }

    void Refresh()
    {
        EnemyData d = target.data;
        title.text = d.displayName.ToUpperInvariant();
        title.color = d.isBoss ? new Color(1f, 0.42f, 0.3f) : CombatSkin.Gold;
        string t = d.isBoss ? "BOSS" : (d.biome == EnemyBiome.Dungeon ? "DUNGEON" : (d.biome == EnemyBiome.Forest ? "FOREST" : ""));
        if (d.nightOnly) t = t.Length > 0 ? t + "  ·  NIGHT" : "NIGHT";
        tagText.text = t;
    }
}
