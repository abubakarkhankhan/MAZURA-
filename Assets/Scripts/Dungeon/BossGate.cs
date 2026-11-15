using UnityEngine;

/// <summary>The sealed door inside the dungeon. Opens when the 3 keys are found.</summary>
public class BossGate : MonoBehaviour
{
    public Transform leftDoor, rightDoor;
    public Collider blocker;
    public Light[] keyLights;
    public float openAngle = 95f;

    bool open;
    float t;
    Quaternion l0, r0;
    WorldPrompt prompt;
    AudioSource sfx;

    void Start()
    {
        if (leftDoor != null) l0 = leftDoor.localRotation;
        if (rightDoor != null) r0 = rightDoor.localRotation;
        prompt = new WorldPrompt(transform, 2.6f, "");
        sfx = gameObject.AddComponent<AudioSource>();
        sfx.spatialBlend = 1f; sfx.maxDistance = 30f; sfx.playOnAwake = false;
    }

    public void Open()
    {
        if (open) return;
        open = true;
        if (blocker != null) blocker.enabled = false;
        sfx.pitch = 0.5f; sfx.PlayOneShot(SimpleSfx.Rumble(), 1f);
        if (CameraShake.Instance != null) CameraShake.Instance.Shake(0.3f, 0.7f);
    }

    void Update()
    {
        t = Mathf.MoveTowards(t, open ? 1f : 0f, Time.deltaTime * 0.5f);
        float e = Mathf.SmoothStep(0f, 1f, t);
        if (leftDoor != null) leftDoor.localRotation = l0 * Quaternion.Euler(0f, openAngle * e, 0f);
        if (rightDoor != null) rightDoor.localRotation = r0 * Quaternion.Euler(0f, -openAngle * e, 0f);

        DungeonQuest q = DungeonQuest.Instance;
        int k = q != null ? q.KeyCount : 0;
        if (keyLights != null)
            for (int i = 0; i < keyLights.Length; i++)
                if (keyLights[i] != null) keyLights[i].enabled = i < k;
        prompt.Text = "<color=#C9B48A>Sealed  -  keys " + k + "/3</color>";
        prompt.Tick(!open && WorldPrompt.PlayerNear(transform.position, 3.5f));
    }
}
