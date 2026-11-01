using StarterAssets;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Player health. Put it on the player (AdventurePlayer).
/// - 100 HP by default.
/// - Slow regeneration starts when no damage was taken for 'regenDelay' seconds.
/// - Other scripts (enemies, traps...) call TakeDamage(amount).
/// - At 0 HP the player stops moving and "You Died" is shown.
///
/// Example from an enemy:
///   PlayerRef.Transform.GetComponent&lt;HealthManager&gt;().TakeDamage(10f);
/// </summary>
public class HealthManager : MonoBehaviour
{
    [Header("Health")]
    public float maxHealth = 100f;

    [Header("Regeneration")]
    [Tooltip("Seconds without taking damage before regeneration starts.")]
    public float regenDelay = 5f;
    [Tooltip("Health points regained per second while regenerating (slow).")]
    public float regenPerSecond = 2f;

    [Header("HUD")]
    [Tooltip("Text that shows 'HP: 100/100'.")]
    [SerializeField] private TMP_Text healthText;
    [Tooltip("Object with the big 'You Died' text. It is hidden until the player dies.")]
    [SerializeField] private GameObject deathMessage;

    [Header("Events")]
    public UnityEvent OnDied = new UnityEvent();

    public float CurrentHealth { get; private set; }
    public bool IsDead { get; private set; }

    private float timeSinceDamage;
    private int shownHealth = -1;

    private void Awake()
    {
        CurrentHealth = maxHealth;
    }

    private void Start()
    {
        if (deathMessage != null) deathMessage.SetActive(false);
        UpdateHud();
    }

    private void Update()
    {
        if (IsDead) return;

        // Slow regeneration when no damage was taken recently.
        timeSinceDamage += Time.deltaTime;
        if (timeSinceDamage >= regenDelay && CurrentHealth < maxHealth)
        {
            CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + regenPerSecond * Time.deltaTime);
            UpdateHud();
        }
    }

    /// <summary>Reduces health. Safe to call from any script.</summary>
    public void TakeDamage(float amount)
    {
        if (IsDead || amount <= 0f) return;

        CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
        timeSinceDamage = 0f; // restart the regeneration delay
        UpdateHud();

        if (CurrentHealth <= 0f) Die();
    }

    // Quick test from the Inspector: right-click the component > "Test: Take 20 Damage".
    [ContextMenu("Test: Take 20 Damage")]
    private void TestDamage()
    {
        TakeDamage(20f);
    }

    private void Die()
    {
        IsDead = true;

        // Stop all player movement and camera control.
        ThirdPersonController controller = GetComponent<ThirdPersonController>();
        if (controller != null) controller.enabled = false;

        StarterAssetsInputs input = GetComponent<StarterAssetsInputs>();
        if (input != null)
        {
            input.move = Vector2.zero;
            input.look = Vector2.zero;
            input.jump = false;
            input.sprint = false;
        }

        // The controller no longer updates the animator, so reset it to Idle by hand.
        Animator animator = GetComponentInChildren<Animator>();
        if (animator != null)
        {
            animator.SetFloat("Speed", 0f);
            animator.SetFloat("MotionSpeed", 1f);
        }

        if (deathMessage != null) deathMessage.SetActive(true);
        OnDied.Invoke();
    }

    private void UpdateHud()
    {
        if (healthText == null) return;

        // Only rebuild the string when the shown number changes.
        int shown = Mathf.CeilToInt(CurrentHealth);
        if (shown == shownHealth) return;
        shownHealth = shown;

        healthText.text = "HP: " + shown + "/" + Mathf.CeilToInt(maxHealth);
    }
}
