using UnityEngine;

/// <summary>One of the 3 dungeon keys. Floats over its pedestal; walk into it to take it.</summary>
public class DungeonKey : MonoBehaviour
{
    public string id = "red";
    public string niceName = "Red Key";
    public Color color = Color.red;
    public Transform model;
    public Light glow;

    float baseY, gone = -1f;

    void Start() { if (model != null) baseY = model.localPosition.y; }

    void Update()
    {
        if (gone >= 0f)
        {
            gone += Time.deltaTime;
            if (model != null) { model.localScale = Vector3.one * Mathf.Max(0f, 1f - gone * 3f) * 0.8f; model.localPosition += Vector3.up * Time.deltaTime * 2f; }
            if (glow != null) glow.intensity = Mathf.Max(0f, glow.intensity - Time.deltaTime * 8f);
            if (gone > 0.6f) { if (model != null) model.gameObject.SetActive(false); enabled = false; }
            return;
        }
        if (model != null)
        {
            model.Rotate(0f, 90f * Time.deltaTime, 0f, Space.World);
            Vector3 p = model.localPosition; p.y = baseY + Mathf.Sin(Time.time * 2f) * 0.08f; model.localPosition = p;
        }
        if (WorldPrompt.PlayerNear(transform.position, 1.4f)) Take();
    }

    void Take()
    {
        gone = 0f;
        if (DungeonQuest.Instance != null) DungeonQuest.Instance.AddKey(id, niceName, color);
        AudioSource.PlayClipAtPoint(SimpleSfx.Chime(), transform.position, 0.9f);
        if (CameraShake.Instance != null) CameraShake.Instance.Shake(0.1f, 0.25f);
    }
}
