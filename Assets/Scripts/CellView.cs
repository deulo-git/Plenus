using UnityEngine;
using UnityEngine.UI;

public class CellView : MonoBehaviour
{
    private Image baseImage;
    public GameObject crossOverlay;      // La creu definitiva
    public GameObject selectionOverlay;  // NOU: Feedback visual de pre-selecció
    public BoardManager ParentBoardManager { get; private set; }
    public CellData LogicData { get; private set; } // Necessitem llegir la data des del Manager

    private void Awake()
    {
        baseImage = GetComponent<Image>();
        if (crossOverlay != null) crossOverlay.SetActive(false);
        if (selectionOverlay != null) selectionOverlay.SetActive(false);
    }

    // NOU: Afegim 'BoardManager boardManager' als paràmetres
    public void Initialize(CellData data, Color color, BoardManager boardManager)
    {
        LogicData = data;
        baseImage.color = color;
        ParentBoardManager = boardManager; // Guardem la referència

        if (crossOverlay != null) crossOverlay.SetActive(data.IsMarked);
        if (selectionOverlay != null) selectionOverlay.SetActive(false);
    }

    // Aquest mètode està connectat al Button de l'UI
    public void OnCellClicked()
    {
        if (!DiceManager.Instance.AreDiceRolled)
        {
            Debug.Log("Has de tirar els daus primer!");
            return;
        }
        SelectionManager.Instance?.AttemptSelectCell(this);
        SelectionManager.Instance?.SetActiveBoard(ParentBoardManager);

    }

    // Funcions cridades pel SelectionManager
    public void SetSelectedVisual(bool isSelected)
    {
        if (selectionOverlay != null) selectionOverlay.SetActive(isSelected);
    }

    public void UpdateMarkedVisual()
    {
        if (crossOverlay != null) {
            Button cellButton = GetComponent<Button>();
            if (cellButton != null) {
                ColorBlock colors = cellButton.colors;
                colors.normalColor = new Color(colors.normalColor.r, colors.normalColor.g, colors.normalColor.b, 0.8f);
                cellButton.colors = colors;
            }
            cellButton.interactable = !LogicData.IsMarked;
            if (LogicData.HasStar)
            {
                RawImage starImage = crossOverlay.GetComponent<RawImage>();
                Color starColor = starImage.color;
                starColor.a = 0.3f;
            }
            
            crossOverlay.SetActive(LogicData.IsMarked);
        }
    }
}