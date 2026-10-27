using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// A chest the player can open once by pressing E while standing inside
/// its trigger area. Opening gives one map piece through MapManager.
/// Needs: a trigger collider on this object, and the player tagged "Player"
/// (the Starter Assets player uses a CharacterController, which triggers
/// OnTriggerEnter/Exit on trigger colliders).
/// </summary>
[RequireComponent(typeof(Collider))]
public class MapChest : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The pivot that rotates to open the lid (placed at the lid's hinge).")]
    [SerializeField] private Transform lidPivot;

    [Header("Settings")]
    [SerializeField] private float openAngle = -110f;   // Degrees around the pivot's X axis
    [SerializeField] private float openDuration = 0.5f; // Seconds

    private bool playerInRange;   // Is the player inside the trigger?
    private bool isOpened;        // Once true, the chest can never give a piece again

    private void Reset()
    {
        // Make sure the collider on this object acts as a trigger.
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player")) playerInRange = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player")) playerInRange = false;
    }

    private void Update()
    {
        if (isOpened || !playerInRange) return;

        // Uses the Input System already installed with Starter Assets.
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.eKey.wasPressedThisFrame)
            Open();
    }

    private void Open()
    {
        // Lock immediately so a second press can never count twice.
        isOpened = true;

        if (MapManager.Instance != null)
            MapManager.Instance.AddMapPiece();
        else
            Debug.LogWarning("MapChest: no MapManager in the scene.", this);

        if (lidPivot != null)
            StartCoroutine(OpenLid());
    }

    // Smoothly rotates the lid open.
    private IEnumerator OpenLid()
    {
        Quaternion start = lidPivot.localRotation;
        Quaternion end = Quaternion.Euler(openAngle, 0f, 0f) * start;
        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.01f, openDuration);
            lidPivot.localRotation = Quaternion.Slerp(start, end, t);
            yield return null;
        }
        lidPivot.localRotation = end;
    }
}
