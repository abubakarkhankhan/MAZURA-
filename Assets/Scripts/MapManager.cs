using TMPro;
using UnityEngine;

/// <summary>
/// Simple singleton that counts collected map pieces (0 to 4)
/// and keeps the HUD text up to date.
/// </summary>
public class MapManager : MonoBehaviour
{
    // Global access point: MapManager.Instance
    public static MapManager Instance { get; private set; }

    [Header("HUD")]
    [Tooltip("The TextMeshPro text that shows 'Map pieces: x/4'.")]
    [SerializeField] private TMP_Text mapPiecesText;

    [Header("Settings")]
    [SerializeField] private int maxMapPieces = 4;

    // Current number of collected pieces.
    public int CollectedPieces { get; private set; }
    public int MaxPieces => maxMapPieces;

    private void Awake()
    {
        // Handle duplicate singletons safely: keep the first, destroy the rest.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        // Show "Map pieces: 0/4" when the scene starts.
        UpdateHUD();
    }

    private void OnDestroy()
    {
        // Clear the static reference if this instance is being destroyed.
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// Adds one map piece. Returns true if it was added,
    /// false if the maximum was already reached.
    /// </summary>
    public bool AddMapPiece()
    {
        if (CollectedPieces >= maxMapPieces) return false;

        CollectedPieces++;
        UpdateHUD();
        return true;
    }

    // Writes the current count to the HUD immediately.
    private void UpdateHUD()
    {
        if (mapPiecesText != null)
            mapPiecesText.text = "Map pieces: " + CollectedPieces + "/" + maxMapPieces;
    }
}
