using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using static UnityEngine.Rendering.DebugUI.Table;

public class SelectionManager : MonoBehaviour
{
    public static SelectionManager Instance;

    [Header("UI Debug References")]
    public TextMeshProUGUI debugConsole; // Link your TextMeshPro element here

    [Header("Dice State (Simulation)")]
    public int activeNumber;
    public CellColor activeColor;
    private bool areDiceSelected = false;

    private List<CellView> currentSelection = new List<CellView>();
    private string lastSystemMessage = "Waiting for player to start selecting...";
    private bool isNumberWildcard = false;
    private bool isColorWildcard = false;

    [SerializeField] private BoardManager boardManager;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        UpdateDebugConsole();
    }

    // Afegeix aquest mètode nou per resetejar quan es tiren els daus:
    public void ResetDiceSelection()
    {
        areDiceSelected = false;
        CancelSelection(); // Neteja les caselles a mig seleccionar si n'hi havia
        lastSystemMessage = "Roll the dice and select ONE number and ONE color.";
        UpdateDebugConsole();
    }

    // Call this from DiceManager later when dice are rolled and selected
    public void SetActiveDice(int number, CellColor color, bool isNumberWildcard, bool isColorWildcard)
    {
        activeNumber = number;
        activeColor = color;
        this.isNumberWildcard = isNumberWildcard;
        this.isColorWildcard = isColorWildcard;
        Debug.Log("Al tanto, tenim wildcard" + isNumberWildcard + " , i color " + isColorWildcard);
        areDiceSelected = true; // Ara ja podem clicar caselles!
        lastSystemMessage = $"Dice selected: {number} & {color}. Start picking cells.";
        UpdateDebugConsole();
    }



    public void AttemptSelectCell(CellView clickedCell)
    {
        CellData data = clickedCell.LogicData;

        // 0. Basic state checks
        if (!areDiceSelected) return;
        if (data.IsMarked) return;

        // --- DYNAMIC COLOR LOCKING ---
        bool isColorMatch = false;

        if (!isColorWildcard)
        {
            // Normal color dice: Must match the selected color exactly
            isColorMatch = (data.Color == activeColor);
        }
        else
        {
            // Wildcard color dice (Black):
            if (currentSelection.Count == 0)
            {
                // First cell clicked can be ANY color
                isColorMatch = true;
            }
            else
            {
                // Subsequent cells MUST match the color of the very first cell selected
                isColorMatch = (data.Color == currentSelection[0].LogicData.Color);
            }
        }
        // -----------------------------
        if (!isColorMatch)
        {
            lastSystemMessage = "<color=red>Invalid color!</color>";
            UpdateDebugConsole();
            return;
        }

        // 1. Toggle logic (if already selected, remove it)
        if (currentSelection.Contains(clickedCell))
        {
            currentSelection.Remove(clickedCell);
            clickedCell.SetSelectedVisual(false);
            return;
        }

        // 2. Logic for the FIRST cell of the turn (The Anchor)
        if (currentSelection.Count == 0)
        {
            if (!IsValidAnchor(data))
            {
                lastSystemMessage = "<color=red>Invalid start: Must be middle row or adjacent to marked cells!</color>";
                UpdateDebugConsole();
                return;
            }
        }
        // 3. Logic for SUBSEQUENT cells (The Chain)
        else
        {
            if (!IsAdjacentToCurrentSelection(data))
            {
                lastSystemMessage = "<color=red>Invalid path: Must be adjacent to your current selection!</color>";
                UpdateDebugConsole();
                return;
            }
        }

        // 4. Limit check
        if (currentSelection.Count >= activeNumber) return;

        // Everything is valid!
        currentSelection.Add(clickedCell);
        clickedCell.SetSelectedVisual(true);
    }

    // Helper: Checks if the first click is valid
    private bool IsValidAnchor(CellData data)
    {
        bool isFirstTurn = !HasAnyMarkedCells();

        if (isFirstTurn)
        {
            // Rule: Must be middle row
            return data.Row == BoardManager.MIDDLE_ROW;
        }
        else
        {
            // Rule: Must be adjacent to existing marked cells
            return IsAdjacentToMarked(data);
        }
    }

    // Helper: Checks if a new cell is adjacent to the chain being built
    private bool IsAdjacentToCurrentSelection(CellData data)
    {
        foreach (var selectedView in currentSelection)
        {
            if (AreAdjacent(data, selectedView.LogicData)) return true;
        }
        return false;
    }
 
    private bool HasAnyMarkedCells()
    {
        // Accedim a boardManager.BoardData
        for (int r = 0; r < BoardManager.ROWS; r++)
            for (int c = 0; c < BoardManager.COLS; c++)
                if (boardManager.BoardData[r, c].IsMarked) return true;
        return false;
    }

    private bool IsAdjacentToMarked(CellData data)
    {
        int[] dr = { -1, 1, 0, 0 };
        int[] dc = { 0, 0, -1, 1 };

        for (int i = 0; i < 4; i++)
        {
            int nr = data.Row + dr[i];
            int nc = data.Col + dc[i];

            if (nr >= 0 && nr < BoardManager.ROWS && nc >= 0 && nc < BoardManager.COLS)
            {
                if (boardManager.BoardData[nr, nc].IsMarked) return true;
            }
        }
        return false;
    }

    public void ConfirmSelection()
    {
        bool isValid = false;

        // 1. Validem la lògica segons si és comodí o no
        if (isNumberWildcard)
        {
            // Si és comodí (el 6), permetem seleccionar entre 1 i 6 caselles (ajusta el límit si cal)
            isValid = (currentSelection.Count > 0 && currentSelection.Count < 6);
        }
        else
        {
            // Si no és comodí, ha de ser exactament el número del dau
            isValid = (currentSelection.Count == activeNumber);
        }

        // 2. Executem l'acció segons el resultat
        if (isValid)
        {
            foreach (var cell in currentSelection)
            {
                cell.LogicData.IsMarked = true;
                cell.UpdateMarkedVisual();
                cell.SetSelectedVisual(false);
            }
            currentSelection.Clear();
            lastSystemMessage = "<color=yellow>Selection Confirmed! Turn ended.</color>";
        }
        else
        {
            // 3. Feedback d'error
            if (isNumberWildcard)
            {
                lastSystemMessage = "<color=orange>Warning: Wildcard (6) requires selecting 1-6 cells.</color>";
            }
            else
            {
                lastSystemMessage = $"<color=orange>Warning: You must select EXACTLY {activeNumber} cells.</color>";
            }
        }

        UpdateDebugConsole();
    }

    public void CancelSelection()
    {
        foreach (var cell in currentSelection)
        {
            cell.SetSelectedVisual(false);
        }
        currentSelection.Clear();
        lastSystemMessage = "Selection canceled. Try again.";
        UpdateDebugConsole();
    }

    // Mathematical adjacency check (Orthogonal only)
    private bool AreAdjacent(CellData a, CellData b)
    {
        return Mathf.Abs(a.Row - b.Row) + Mathf.Abs(a.Col - b.Col) == 1;
    }

    // Constructs the UI text panel
    private void UpdateDebugConsole()
    {
        if (debugConsole == null) return;

        StringBuilder sb = new StringBuilder();

        sb.AppendLine("--- <b>SELECTION DEBUG PANEL</b> ---");
        sb.AppendLine($"<b>Active Dice:</b> {activeNumber} | {activeColor}");

        sb.AppendLine("\n--- <b>CURRENT STATUS</b> ---");

        // Paint the text green if we have the exact amount of cells ready to confirm
        string countColor = (currentSelection.Count == activeNumber) ? "green" : "white";
        sb.AppendLine($"<b>Cells Picked:</b> <color={countColor}>{currentSelection.Count} / {activeNumber}</color>");

        sb.AppendLine("\n--- <b>SYSTEM MESSAGE</b> ---");
        sb.AppendLine(lastSystemMessage);

        debugConsole.text = sb.ToString();
    }
}