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

    void Start()
    {
        //// 1. Setup listeners: Every time a slider moves, we update label AND UI logic
        //sliderRows.onValueChanged.AddListener((val) => {
        //    UpdateLabel(rowLabel, val);
        //    UpdateUI();
        //});

        //sliderCols.onValueChanged.AddListener((val) => {
        //    UpdateLabel(colLabel, val);
        //    UpdateUI();
        //});

        //// 2. Initial state
        //UpdateLabel(rowLabel, sliderRows.value);
        //UpdateLabel(colLabel, sliderCols.value);
        validator = new BoardValidator();

    }

    //private void UpdateLabel(TextMeshProUGUI label, float val)
    //{
    //    if (label != null) label.text = $"{(int)val}";
    //}

    //Called by the button
    // TestingConsole.cs
    // TestingConsole.cs

    // TestingConsole.cs
    public void GenerateAndValidate()
    {
        consoleText.text = "";

        boardManager.GenerateBoard();

        // Obtenim els clusters un cop generat el tauler
        var clusters = boardManager.GetColorClusters();

        // Generem l'informe únic
        bool isValid = validator.ValidateBoard(boardManager.GetBoardData(), clusters, out string report);

        string header = isValid ? "<color=green>VALID BOARD</color>" : "<color=red>INVALID BOARD</color>";
        consoleText.text = $"{header}\n{report}";
    }
}