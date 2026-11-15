using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// The gate of the Old Dungeon in the Dark Forest. Sealed until the 4 map pieces are found,
/// then the doors swing open and [E] takes the player down into the dungeon.
/// </summary>
public class DungeonGate : MonoBehaviour
{
    public Transform leftDoor, rightDoor;
    [Tooltip("Where the player stands when coming back up from the dungeon.")]
    public Transform outsidePoint;
    public Light glow;
    public float openAngle = 95f;
    public float useRange = 3.4f;

    bool open;
    float t;
    Quaternion l0, r0;
    WorldPrompt prompt;
    AudioSource sfx;

    void Start()
    {
        if (leftDoor != null) l0 = leftDoor.localRotation;
        if (rightDoor != null) r0 = rightDoor.localRotation;
        prompt = new WorldPrompt(transform, 3.6f, "");
        sfx = gameObject.AddComponent<AudioSource>();
        sfx.spatialBlend = 1f; sfx.maxDistance = 30f; sfx.playOnAwake = false;
        if (glow != null) glow.intensity = 0f;
    }

    public void Open()
    {
        if (open) return;
        open = true;
        sfx.pitch = 0.6f; sfx.PlayOneShot(SimpleSfx.Rumble(), 1f);
        if (CameraShake.Instance != null) CameraShake.Instance.Shake(0.35f, 0.8f);
    }

    Vector3 Front { get { return outsidePoint != null ? outsidePoint.position : transform.position + transform.right * 2f; } }

    void Update()
    {
        t = Mathf.MoveTowards(t, open ? 1f : 0f, Time.deltaTime * 0.45f);
        float e = Mathf.SmoothStep(0f, 1f, t);
        if (leftDoor != null) leftDoor.localRotation = l0 * Quaternion.Euler(0f, openAngle * e, 0f);
        if (rightDoor != null) rightDoor.localRotation = r0 * Quaternion.Euler(0f, -openAngle * e, 0f);
        if (glow != null) glow.intensity = Mathf.Lerp(0f, 3f, e) * (0.85f + 0.15f * Mathf.Sin(Time.time * 3f));

        bool near = WorldPrompt.PlayerNear(Front, useRange);
        int maps = MapManager.Instance != null ? MapManager.Instance.CollectedPieces : 0;
        prompt.Text = open ? WorldPrompt.Key("Enter the Old Dungeon") : "<color=#C9B48A>Sealed  -  map pieces " + maps + "/4</color>";
        prompt.Tick(near);
        if (near && open && t > 0.6f && WorldPrompt.CanAct() && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            if (DungeonQuest.Instance != null) DungeonQuest.Instance.EnterDungeon();
    }
}
