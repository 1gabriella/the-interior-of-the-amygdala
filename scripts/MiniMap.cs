// ===============================================
// Influences & Inspirations:
// - Unity Camera and RenderTexture workflows for creating a secondary view
// - 2D UI mapping techniques (anchored positioning) using RectTransforms
// - Player-tracking minimaps in games like classic RPGs and open-world titles
// - UnityEngine.UI RawImage for displaying camera output on canvas
// - Button-driven UI interactions via UnityEvent callbacks
// - Mathf.Clamp01 for normalizing world coordinates to [0,1] range
// ===============================================

using UnityEngine;
using UnityEngine.UI;

public class MinimapSystem : MonoBehaviour
{
    [Header("Minimap Components")]
    public Camera minimapCamera;      // Camera rendering top-down view of the world
    public Transform player;          // Reference to the player transform
    public RawImage mapImage;         // UI element to show the minimap camera's RenderTexture
    public RectTransform playerIcon;  // UI icon representing the player's position on the map
    public Button toggleMapButton;    // Button to show/hide the minimap

    [Header("Map Settings")]
    public Vector2 worldSize = new Vector2(100, 100); // Size of the world in X and Z axes
    public float cameraHeight = 50f;  // Height at which the minimap camera flies above the player

    private bool mapVisible = false;  // Tracks whether the minimap is currently visible

    void Awake()
    {
        // Initially hide the entire minimap GameObject
        gameObject.SetActive(false);

        // Register the button click event to toggle the map visibility
        if (toggleMapButton != null)
            toggleMapButton.onClick.AddListener(ToggleMap);
    }

    void LateUpdate()
    {
        // Only update camera and icon when the map is visible and player is assigned
        if (!mapVisible || player == null) return;

        // 1) Pan the minimap camera to follow the player's X,Z position
        Vector3 p = player.position;
        minimapCamera.transform.position = new Vector3(p.x, cameraHeight, p.z);
        // Ensure the camera looks straight down
        minimapCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        // 2) Move the player icon on the UI based on normalized world position
        float nx = Mathf.Clamp01(p.x / worldSize.x + 0.5f);
        float ny = Mathf.Clamp01(p.z / worldSize.y + 0.5f);
        RectTransform rt = mapImage.rectTransform;
        // Convert normalized coords into UI local position
        playerIcon.anchoredPosition = new Vector2(
            (nx - 0.5f) * rt.sizeDelta.x,
            (ny - 0.5f) * rt.sizeDelta.y
        );
    }

    /// <summary>
    /// Toggle the minimap's visibility on or off.
    /// Called via the UI button's OnClick event.
    /// </summary>
    public void ToggleMap()
    {
        mapVisible = !mapVisible;
        gameObject.SetActive(mapVisible);
    }
}


