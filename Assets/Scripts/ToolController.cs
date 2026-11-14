using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>The three tools the player can hold.</summary>
public enum ToolType { Axe, Pickaxe, Weapon, None }

/// <summary>
/// Lives on AdventurePlayer.
/// Keys 1/2/3 select the tool, LEFT MOUSE swings it.
/// The swing animation plays at once; the actual hit is reported ~0.4 s later
/// (when the tool reaches its target) through the static OnToolHit event.
/// Resource nodes (Task 3) listen to that event.
/// </summary>
public class ToolController : MonoBehaviour
{
    public static ToolController Instance { get; private set; }

    /// <summary>Raised at the moment the swing lands.</summary>
    public static event Action<ToolType> OnToolHit;

    /// <summary>Raised the moment a swing starts (for the whoosh sound).</summary>
    public static event Action<ToolType> OnSwingStart;

    [Header("References (assign in Inspector)")]
    [Tooltip("Animator on AdventurerModel. Auto-found if empty.")]
    public Animator animator;
    [Tooltip("Tool models parented to the right hand (Wrist.R). Only the selected one is visible.")]
    public GameObject axeModel;
    public GameObject pickaxeModel;
    public GameObject weaponModel;
    [Tooltip("HUD text that shows the selected tool.")]
    public TMP_Text toolText;

    [Header("Timing")]
    [Tooltip("Seconds between swings. Swing clips are 0.7 s long.")]
    public float cooldown = 0.8f;
    [Tooltip("Seconds from click to the moment the hit lands.")]
    public float hitDelay = 0.4f;

    public ToolType CurrentTool { get; private set; } = ToolType.Axe;

    static readonly int ChopHash = Animator.StringToHash("Chop");
    static readonly int MineHash = Animator.StringToHash("Mine");
    static readonly int AttackHash = Animator.StringToHash("Attack");

    float nextSwingTime;

    void Awake()
    {
        Instance = this;
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    void Start()
    {
        Equip(ToolType.Axe);
    }

    void Update()
    {
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        if (kb == null || mouse == null) return;

        // A menu is open (cursor free): ignore tool input completely.
        if (Cursor.lockState != CursorLockMode.Locked) return;

        if (kb.digit1Key.wasPressedThisFrame) Equip(ToolType.Axe);
        else if (kb.digit2Key.wasPressedThisFrame) Equip(ToolType.Pickaxe);
        else if (kb.digit3Key.wasPressedThisFrame) Equip(ToolType.Weapon);
        else if (kb.digit4Key.wasPressedThisFrame) Equip(ToolType.None);

        if (mouse.leftButton.wasPressedThisFrame && Time.time >= nextSwingTime && !ShieldController.IsBlocking)
            Swing();
    }

    /// <summary>Select a tool, show its model and update the HUD.</summary>
    public void Equip(ToolType tool)
    {
        CurrentTool = tool;
        if (animator != null) animator.SetBool("Armed", tool == ToolType.Weapon);   // sword stance for walk/run
        if (axeModel != null) axeModel.SetActive(tool == ToolType.Axe);
        if (pickaxeModel != null) pickaxeModel.SetActive(tool == ToolType.Pickaxe);
        if (weaponModel != null) weaponModel.SetActive(tool == ToolType.Weapon);

        if (toolText != null)
        {
            int key = (int)tool + 1;
            toolText.text = "Tool [" + key + "]: " + tool;
        }
    }

    void Swing()
    {
        // Key 4 = empty hands: nothing to swing.
        if (CurrentTool == ToolType.None) return;

        nextSwingTime = Time.time + cooldown;
        OnSwingStart?.Invoke(CurrentTool);

        if (animator != null)
        {
            switch (CurrentTool)
            {
                case ToolType.Axe: animator.SetTrigger(ChopHash); break;
                case ToolType.Pickaxe: animator.SetTrigger(MineHash); break;
                default: animator.SetTrigger(AttackHash); break;
            }
        }

        StartCoroutine(HitAfterDelay(CurrentTool));
    }

    IEnumerator HitAfterDelay(ToolType tool)
    {
        yield return new WaitForSeconds(hitDelay);
        OnToolHit?.Invoke(tool);
    }
}
