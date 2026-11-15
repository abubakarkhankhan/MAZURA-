using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Sprint stamina. Holding Sprint while moving drains stamina; at zero you are exhausted
/// and cannot sprint again until it refills to 'recoverAt'. Refills after a short pause.
/// Works by overriding StarterAssetsInputs.sprint every frame (before ThirdPersonController runs).
/// </summary>
[DefaultExecutionOrder(-50)]
public class Stamina : MonoBehaviour
{
    public static Stamina Instance { get; private set; }

    public float max = 100f;
    public float drainPerSecond = 20f;   // about 5 seconds of full sprint
    public float regenPerSecond = 16f;
    public float regenDelay = 0.9f;
    public float recoverAt = 25f;

    public float Current { get; private set; }
    public float Normalized { get { return Current / max; } }
    public bool Exhausted { get; private set; }
    public bool Sprinting { get; private set; }

    MonoBehaviour inputs;
    FieldInfo sprintField, moveField;
    InputAction sprintAction;
    float lastUse;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        PlayerInput pi = Object.FindAnyObjectByType<PlayerInput>();
        if (pi != null && pi.GetComponent<Stamina>() == null) pi.gameObject.AddComponent<Stamina>();
    }

    void Awake()
    {
        Instance = this;
        Current = max;
        foreach (MonoBehaviour mb in GetComponents<MonoBehaviour>())
            if (mb.GetType().Name == "StarterAssetsInputs") inputs = mb;
        if (inputs != null)
        {
            sprintField = inputs.GetType().GetField("sprint");
            moveField = inputs.GetType().GetField("move");
        }
        PlayerInput pi = GetComponent<PlayerInput>();
        if (pi != null && pi.actions != null) sprintAction = pi.actions.FindAction("Sprint");
    }

    void Update()
    {
        bool want = sprintAction != null ? sprintAction.IsPressed()
                  : (Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed);
        bool moving = moveField != null && ((Vector2)moveField.GetValue(inputs)).sqrMagnitude > 0.01f;
        if (Cursor.lockState != CursorLockMode.Locked) want = false;   // a UI is open

        if (Exhausted && Current >= recoverAt) Exhausted = false;
        bool can = want && moving && !Exhausted && Current > 0f;
        Sprinting = can;
        if (sprintField != null) sprintField.SetValue(inputs, can);

        if (can)
        {
            Current = Mathf.Max(0f, Current - drainPerSecond * Time.deltaTime);
            lastUse = Time.time;
            if (Current <= 0f) Exhausted = true;
        }
        else if (Time.time - lastUse > regenDelay)
        {
            Current = Mathf.Min(max, Current + regenPerSecond * (moving ? 0.7f : 1f) * Time.deltaTime);
        }
    }
}
