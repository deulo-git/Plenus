using UnityEngine;
using UnityEngine.UI;

public class CellView : MonoBehaviour
{
    // abans:  private Image baseImage;
    [SerializeField] private Image fillImage;     // la Image del FillingObject (la que es pinta)
    [SerializeField] private Button cellButton;   // el Button del FillingObject
    public GameObject crossOverlay;      
    public GameObject selectionOverlay;  
    public BoardManager ParentBoardManager { get; private set; }
    public CellData LogicData { get; private set; } 

    private void Awake()
    {
        if (crossOverlay != null) crossOverlay.SetActive(false);
        if (selectionOverlay != null) selectionOverlay.SetActive(false);
    }

    public void Initialize(CellData data, Color color, BoardManager boardManager)
    {
        LogicData = data;
        fillImage.color = color;

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
        if (crossOverlay != null)
        {
            if (cellButton != null)
            {
                ColorBlock colors = cellButton.colors;
                colors.normalColor = new Color(colors.normalColor.r, colors.normalColor.g, colors.normalColor.b, 0.8f);
                cellButton.colors = colors;
                cellButton.interactable = !LogicData.IsMarked;
            }
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