using UnityEngine;

/// <summary>
/// Plenus is designed as a portrait (1080x1920) mobile game, but during PC
/// testing the Windowed build launches at that exact size regardless of the
/// developer's monitor. Most desktop monitors are landscape and only ~1080px
/// tall, so a 1920px-tall window does not physically fit — part of it ends
/// up off-screen, and because "Resizable Window" is off in Player Settings,
/// there is no way to drag it back into view.
///
/// This runs before the first scene loads (so it applies before any UI is
/// shown) and resizes the window to the largest size that both fits the
/// current display and keeps the exact 1080:1920 portrait aspect ratio.
/// Combined with the Canvas Scaler being set to "Scale With Screen Size"
/// (Match Height, reference 1080x1920), the UI scales down cleanly to fit —
/// nothing gets cut off or oversized, on any monitor.
///
/// Standalone Windows/Mac/Linux only: mobile devices and the Editor already
/// size themselves correctly and must not be touched here.
/// </summary>
public static class StandaloneWindowFitter
{
    private const int DesignWidth = 1080;
    private const int DesignHeight = 1920;

    // Leave room for the OS taskbar and the window's own title bar so the
    // whole window — including its bottom edge — stays fully visible.
    private const float UsableScreenFraction = 0.9f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void FitWindowToScreen()
    {
#if UNITY_STANDALONE && !UNITY_EDITOR
        int screenHeight = Display.main.systemHeight;
        int screenWidth = Display.main.systemWidth;

        int maxHeight = Mathf.FloorToInt(screenHeight * UsableScreenFraction);
        int maxWidthForHeight = Mathf.FloorToInt(maxHeight * DesignWidth / (float)DesignHeight);

        int targetHeight = Mathf.Min(DesignHeight, maxHeight);
        int targetWidth = Mathf.RoundToInt(targetHeight * DesignWidth / (float)DesignHeight);

        // Extra guard in case a monitor is unusually narrow rather than short.
        if (targetWidth > screenWidth)
        {
            targetWidth = Mathf.Min(maxWidthForHeight, screenWidth);
            targetHeight = Mathf.RoundToInt(targetWidth * DesignHeight / (float)DesignWidth);
        }

        Screen.SetResolution(targetWidth, targetHeight, FullScreenMode.Windowed);
        Debug.Log($"[StandaloneWindowFitter] Screen {screenWidth}x{screenHeight} -> window sized to {targetWidth}x{targetHeight} (portrait 1080:1920 preserved).");
#endif
    }
}
