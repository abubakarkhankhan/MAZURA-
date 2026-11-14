using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// In tight maze corridors the follow camera is pushed right up to the player's head.
/// When that happens the player (and the tool in his hand) switches to "shadows only",
/// so the view is never blocked by the inside of the character. Created automatically.
/// </summary>
public class PlayerCameraFade : MonoBehaviour
{
    static PlayerCameraFade instance;

    public float hideDistance = 0.85f;

    readonly List<Renderer> renderers = new List<Renderer>();
    readonly List<ShadowCastingMode> original = new List<ShadowCastingMode>();
    Transform player;
    bool hidden;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (instance != null) return;
        instance = new GameObject("PlayerCameraFade").AddComponent<PlayerCameraFade>();
    }

    void Collect()
    {
        renderers.Clear();
        original.Clear();
        foreach (Renderer r in player.GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
            renderers.Add(r);
            original.Add(r.shadowCastingMode);
        }
    }

    void LateUpdate()
    {
        if (player == null)
        {
            ToolController tc = ToolController.Instance;
            if (tc == null) return;
            player = tc.transform;
            Collect();
        }
        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 head = player.position + Vector3.up * 1.45f;
        Vector3 chest = player.position + Vector3.up * 1.0f;
        float d = Mathf.Min(Vector3.Distance(cam.transform.position, head), Vector3.Distance(cam.transform.position, chest));
        bool hide = d < hideDistance;
        if (hide == hidden) return;
        hidden = hide;
        for (int i = 0; i < renderers.Count; i++)
        {
            if (renderers[i] == null) continue;
            renderers[i].shadowCastingMode = hide ? ShadowCastingMode.ShadowsOnly : original[i];
        }
    }
}
