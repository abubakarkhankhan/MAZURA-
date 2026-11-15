using UnityEngine;

/// <summary>Numbers for the win / death screens: time played, enemies killed, nights survived.</summary>
public static class GameStats
{
    public static float StartTime { get; private set; }
    public static int Kills { get; private set; }
    public static int BossKills { get; private set; }
    public static int Nights { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        StartTime = Time.time; Kills = 0; BossKills = 0; Nights = 0;
        EnemyBase.OnAnyDied -= OnDied;
        EnemyBase.OnAnyDied += OnDied;
        if (DayNightManager.Instance != null) DayNightManager.Instance.OnDawn.AddListener(() => Nights++);
    }

    static void OnDied(EnemyBase e)
    {
        Kills++;
        if (e != null && e.data != null && e.data.isBoss) BossKills++;
    }

    public static string TimeText
    {
        get
        {
            float t = Time.time - StartTime;
            int h = (int)(t / 3600f), m = (int)(t % 3600f / 60f), s = (int)(t % 60f);
            string mm = m.ToString("00"), ss = s.ToString("00");
            return h > 0 ? h + ":" + mm + ":" + ss : mm + ":" + ss;
        }
    }

    public static string Summary()
    {
        int maps = MapManager.Instance != null ? MapManager.Instance.CollectedPieces : 0;
        return "Time  <color=#FFD27A>" + TimeText + "</color>      Nights survived  <color=#FFD27A>" + Nights + "</color>\n" +
               "Enemies defeated  <color=#FFD27A>" + Kills + "</color>      Bosses  <color=#FFD27A>" + BossKills + "</color>      Map pieces  <color=#FFD27A>" + maps + "/4</color>";
    }
}
