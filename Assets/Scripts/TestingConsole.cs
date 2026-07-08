using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TestingConsole : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Slider sliderRows;
    [SerializeField] private Slider sliderCols;
    [SerializeField] private TextMeshProUGUI rowLabel;
    [SerializeField] private TextMeshProUGUI colLabel;
    [SerializeField] private TextMeshProUGUI cellCountText;
    [SerializeField] private Button generateButton;
    [SerializeField] private TextMeshProUGUI consoleText;

    [Header("Dependencies")]
    [SerializeField] private BoardManager boardManager;
    private BoardValidator validator;
    private SelectionManager selectionManager;

    void Start()
    {
        validator = new BoardValidator();
        selectionManager = SelectionManager.Instance;
    }

    //Called by the Generate & Validate button
    //public void GenerateAndValidate()
    //{
    //    consoleText.text = "";

    //    boardManager.GenerateBoard();
    //    selectionManager.ResetDiceSelection(); // Reset selection state when generating a new board

    //    // Obtenim els clusters un cop generat el tauler
    //    var clusters = boardManager.GetColorClusters();

    //    // Generem l'informe únic
    //    bool isValid = validator.ValidateBoard(boardManager.GetBoardData(), clusters, out string report);

    //    string header = isValid ? "<color=green>VALID BOARD</color>" : "<color=red>INVALID BOARD</color>";
    //    consoleText.text = $"{header}\n{report}";
    //}
}