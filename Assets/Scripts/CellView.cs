using UnityEngine;
using UnityEngine.UI;

public class CellView : MonoBehaviour
{
    private Image baseImage;
    public GameObject crossOverlay;      // La creu definitiva
    public GameObject selectionOverlay;  // NOU: Feedback visual de pre-selecció

    public CellData LogicData { get; private set; } // Necessitem llegir la data des del Manager

    private void Awake()
    {
        baseImage = GetComponent<Image>();
        if (crossOverlay != null) crossOverlay.SetActive(false);
        if (selectionOverlay != null) selectionOverlay.SetActive(false);
    }

    public void Initialize(CellData data, Color color)
    {
        LogicData = data;
        baseImage.color = color;

        if (crossOverlay != null) crossOverlay.SetActive(data.IsMarked);
        if (selectionOverlay != null) selectionOverlay.SetActive(false);
    }

    // Aquest mètode està connectat al Button de l'UI
    public void OnCellClicked()
    {
        // Ja no marquem aquí. Avisem al Manager que intentem seleccionar-la.
        SelectionManager.Instance?.AttemptSelectCell(this);
    }

    // Funcions cridades pel SelectionManager
    public void SetSelectedVisual(bool isSelected)
    {
        if (selectionOverlay != null) selectionOverlay.SetActive(isSelected);
    }

    public void UpdateMarkedVisual()
    {
        if (crossOverlay != null) crossOverlay.SetActive(LogicData.IsMarked);
    }
}