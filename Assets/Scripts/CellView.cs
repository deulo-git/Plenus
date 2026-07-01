using UnityEngine;
using UnityEngine.UI;

public class CellView : MonoBehaviour
{
    private Image baseImage;
    public GameObject crossOverlay;
    private CellData logicData;

    private void Awake()
    {
        baseImage = GetComponent<Image>();
        if (crossOverlay != null) crossOverlay.SetActive(false);
    }

    // Update this to match what BoardManager expects
    public void Initialize(CellData data, Color color)
    {
        logicData = data;
        baseImage.color = color;

        // Ensure the cross state is reset if reusing objects
        if (crossOverlay != null) crossOverlay.SetActive(data.IsMarked);
    }

    public void OnCellClicked()
    {
        if (logicData == null) return;
        logicData.IsMarked = true;
        if (crossOverlay != null) crossOverlay.SetActive(true);
    }
}