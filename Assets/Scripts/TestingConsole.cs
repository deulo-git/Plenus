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
        // 1. Setup listeners: Every time a slider moves, we update label AND UI logic
        sliderRows.onValueChanged.AddListener((val) => {
            UpdateLabel(rowLabel, val);
            UpdateUI();
        });

        sliderCols.onValueChanged.AddListener((val) => {
            UpdateLabel(colLabel, val);
            UpdateUI();
        });

        // 2. Initial state
        UpdateLabel(rowLabel, sliderRows.value);
        UpdateLabel(colLabel, sliderCols.value);
        validator = new BoardValidator();

        UpdateUI();
    }

    private void UpdateLabel(TextMeshProUGUI label, float val)
    {
        if (label != null) label.text = $"{(int)val}";
    }

    private void UpdateUI()
    {
        // Calculate total based on current slider values
        int rows = (int)sliderRows.value;
        int cols = (int)sliderCols.value;
        int total = rows * cols;

        // Define viability rules
        bool isViable = (total % 5 == 0 && total >= 30);

        // Update Cell Count display
        cellCountText.text = $"{total} Cells";
        cellCountText.color = isViable ? Color.green : Color.red;

        // Enable or disable generate button
        if (generateButton != null)
        {
            generateButton.interactable = isViable;
        }
    }

    public void RunValidation()
    {
        consoleText.text = "";
        // Measuring time for performance logging
        System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
        sw.Start();

        int rows = (int)sliderRows.value;
        int cols = (int)sliderCols.value;

        boardManager.GenerateAndLinkBoard(rows, cols);
        bool isValid = validator.ValidateBoard(boardManager.GetBoardData(), out string report);

        sw.Stop();
        report += $"Generated {rows}x{cols} in {sw.ElapsedMilliseconds/1000}s";

        string header = isValid ? "<color=green>VALID BOARD</color>\n\n" : "<color=red>INVALID BOARD</color>\n\n";
        consoleText.text += header + report;

        Debug.Log("Validation completed.\n" + report);

    }
}