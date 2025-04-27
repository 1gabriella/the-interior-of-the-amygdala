using UnityEngine;
using UnityEngine.UI;

public class MinimapSystem : MonoBehaviour
{
    public Camera minimapCamera;
    public Transform player;
    public RawImage mapImage;
    public RectTransform playerIcon;
    public Button toggleMapButton;
    public Vector2 worldSize = new Vector2(100, 100);
    public float cameraHeight = 50f;

    private bool mapVisible = false;

    void Awake()
    {
        // hide the whole minimap canvas at start
        gameObject.SetActive(false);

        if (toggleMapButton != null)
            toggleMapButton.onClick.AddListener(ToggleMap);
    }

    void LateUpdate()
    {
        if (!mapVisible || player == null) return;

        // pan camera
        var p = player.position;
        minimapCamera.transform.position = new Vector3(p.x, cameraHeight, p.z);
        minimapCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        // move icon
        float nx = Mathf.Clamp01(p.x / worldSize.x + 0.5f);
        float ny = Mathf.Clamp01(p.z / worldSize.y + 0.5f);
        var rt = mapImage.rectTransform;
        playerIcon.anchoredPosition = new Vector2(
            (nx - 0.5f) * rt.sizeDelta.x,
            (ny - 0.5f) * rt.sizeDelta.y
        );
    }

    public void ToggleMap()
    {
        mapVisible = !mapVisible;
        gameObject.SetActive(mapVisible);
    }
}

