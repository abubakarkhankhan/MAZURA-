using UnityEngine;

/// <summary>
/// How much noise the player makes right now (0 = silent, 1 = running, more while swinging a tool).
/// Enemies multiply this with their hearing range. No setup needed.
/// </summary>
public static class PlayerNoise
{
    static float swingUntil;
    static bool hooked;
    static Vector3 lastPos;
    static float lastTime = -1f, speed;

    static void Hook()
    {
        if (hooked) return;
        hooked = true;
        ToolController.OnSwingStart += _ => swingUntil = Time.time + 0.6f;
    }

    /// <summary>Loudness of the player this frame.</summary>
    public static float Level
    {
        get
        {
            Hook();
            ToolController p = ToolController.Instance;
            if (p == null) return 0f;
            if (Time.time != lastTime)
            {
                Vector3 pos = p.transform.position;
                float dt = Mathf.Max(0.0001f, Time.time - lastTime);
                if (lastTime > 0f) speed = Mathf.Lerp(speed, new Vector2(pos.x - lastPos.x, pos.z - lastPos.z).magnitude / dt, 0.3f);
                lastPos = pos; lastTime = Time.time;
            }
            float move = Mathf.InverseLerp(0.3f, 5.5f, speed);         // walking about 0.35, sprinting 1
            float level = move * move * 0.6f + (move > 0.05f ? 0.15f : 0f);
            if (speed > 4f) level = Mathf.Max(level, 1f);
            if (Time.time < swingUntil) level = Mathf.Max(level, 1.15f); // chopping and fighting is loud
            return level;
        }
    }
}
