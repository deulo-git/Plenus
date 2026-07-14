using UnityEngine;

/// <summary>
/// Attach to a RectTransform that sits directly under the Canvas and wraps
/// all UI content (score panels, dice, buttons, board container, etc.).
/// Shrinks that panel to the device's safe area so nothing is hidden
/// behind a notch, punch-hole camera, rounded corners, or the home
/// indicator bar on portrait phones. Also reacts to runtime changes
/// (e.g. folding phones, or the safe area being reported late on some
/// Android devices).
/// </summary>
[RequireComponent(typeof(RectTransform))]
[DisallowMultipleComponent]
public class SafeAreaFitter : MonoBehaviour
{
    [Tooltip("If true, logs the applied safe area to the console once on start. Useful when testing on a new device.")]
    [SerializeField] private bool logOnApply = false;

    private RectTransform rectTransform;
    private Rect lastSafeArea = new Rect(0, 0, 0, 0);
    private Vector2Int lastScreenSize = new Vector2Int(0, 0);
    private ScreenOrientation lastOrientation = ScreenOrientation.Portrait;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        Apply();
    }

    private void Update()
    {
        // Screen.safeArea can change at runtime: device rotation, split-screen,
        // foldables unfolding, or (on some Android skins) being reported one
        // frame late after app resume. Cheap to check every frame.
        if (Screen.safeArea != lastSafeArea
            || Screen.width != lastScreenSize.x
            || Screen.height != lastScreenSize.y
            || Screen.orientation != lastOrientation)
        {
            Apply();
        }
    }

    private void Apply()
    {
        Rect safeArea = Screen.safeArea;
        lastSafeArea = safeArea;
        lastScreenSize = new Vector2Int(Screen.width, Screen.height);
        lastOrientation = Screen.orientation;

        if (Screen.width <= 0 || Screen.height <= 0)
            return;

        Vector2 anchorMin = safeArea.position;
        Vector2 anchorMax = safeArea.position + safeArea.size;

        anchorMin.x /= Screen.width;
        anchorMin.y /= Screen.height;
        anchorMax.x /= Screen.width;
        anchorMax.y /= Screen.height;

        // Clamp defensively — a bad safeArea read (0-size, inverted) should
        // never collapse the whole UI to nothing.
        anchorMin.x = Mathf.Clamp01(anchorMin.x);
        anchorMin.y = Mathf.Clamp01(anchorMin.y);
        anchorMax.x = Mathf.Clamp01(anchorMax.x);
        anchorMax.y = Mathf.Clamp01(anchorMax.y);

        if (anchorMax.x <= anchorMin.x || anchorMax.y <= anchorMin.y)
            return;

        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;

        if (logOnApply)
            Debug.Log($"[SafeAreaFitter] Applied safe area {safeArea} on screen {Screen.width}x{Screen.height} -> anchors min={anchorMin} max={anchorMax}");
    }
}
