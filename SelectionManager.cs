using Assets.Scripts;
using System.Collections.Generic;
using System.Text;
using Unity.Multiplayer.PlayMode;
using UnityEngine;

public class SelectionManager : MonoBehaviour
{
    public BoardManager currentBoard; 
    public static SelectionManager Instance;

    [Header("Debug")]
    [SerializeField] private SelectionDebugUI debugUI;

    [Header("Dice State (Simulation)")]
    [System.NonSerialized]
    public int? activeNumber;
    [System.NonSerialized]
    public CellColor? activeColor;
    private bool areDiceSelected = false;
    public bool HasCellsSelected => currentCellsSelection.Count > 0;
    public int? ActiveNumber => activeNumber;
    public CellColor? ActiveColor => activeColor;

    public List<CellView> currentCellsSelection = new List<CellView>();
    private string lastSystemMessage = "Waiting for player to start selecting...";
    private bool isNumberWildcard = false;
    private bool isColorWildcard = false;
    private int? pendingNumber;
    private CellColor? pendingColor;
    
    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        RefreshDebug();
    }

    // Quan el jugador canvia, el GameManager cridarà a això
    public void SetActiveBoard(BoardManager board)
    {
        currentBoard = board;
    }

    // Afegeix aquest mètode nou per resetejar quan es tiren els daus:
    public void ResetDiceSelection()
    {
        areDiceSelected = false;
        CancelSelection(); // Neteja les caselles a mig seleccionar si n'hi havia
        lastSystemMessage = "Roll the dice and select ONE number and ONE color.";
        RefreshDebug();
    }

    // Call this from DiceManager later when dice are rolled and selected
    public void SetActiveDice(int number, CellColor color, bool isNumberWildcard, bool isColorWildcard)
    {
        activeNumber = number;
        activeColor = color;
        this.isNumberWildcard = isNumberWildcard;
        this.isColorWildcard = isColorWildcard;
        areDiceSelected = true; // Ara ja podem clicar caselles!
        lastSystemMessage = $"Dice selected: {number} & {color}. Start picking cells.";
        RefreshDebug();
    }

    public void AttemptSelectCell(CellView clickedCell)
    {
        CellData data = clickedCell.LogicData;
        BoardManager currentBoard = clickedCell.ParentBoardManager;

        if (!GameManager.Instance.IsCellClickValid(currentBoard))
        {
            return;
        }

        // 0. Basic state checks
        if (!areDiceSelected) return;
        if (data.IsMarked) return;
        if (currentBoard == null) return;

        // --- 1. DESELECTION LOGIC ---
        if (currentCellsSelection.Contains(clickedCell))
        {
            // Remove the cell from our selection list
            currentCellsSelection.Remove(clickedCell);

            // Turn off the visual selection highlight
            clickedCell.SetSelectedVisual(false);

            // ⚠️ IMPORTANT: DO NOT call ResetDiceSelection() here!
            // If the player unpicks the last cell, we just wait for them to pick another one.
            // The chosen dice must remain active.

            if (currentCellsSelection.Count == 0)
            {
                lastSystemMessage = $"Dice selected: {activeNumber} & {activeColor}. Start picking cells.";
            }
            else
            {
                lastSystemMessage = $"Selected {currentCellsSelection.Count} cells. Waiting to confirm or pick more...";
            }

            RefreshDebug();
            return; // We stop here because it was a deselection
        }

        // --- 2. SELECTION LOGIC ---
        // Ensure dice are actually selected before picking a new cell
        if (!activeNumber.HasValue || !activeColor.HasValue)
        {
            lastSystemMessage = "Warning: Select numeric and color dice first!";
            RefreshDebug();
            return;
        }

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
            if (currentCellsSelection.Count == 0)
            {
                // First cell clicked can be ANY color
                isColorMatch = true;
            }
            else
            {
                // Subsequent cells MUST match the color of the very first cell selected
                isColorMatch = (data.Color == currentCellsSelection[0].LogicData.Color);
            }
        }

        if (!isColorMatch)
        {
            lastSystemMessage = "<color=red>Invalid color!</color>";
            DebugBoardStatus(currentBoard);
            RefreshDebug();
            return;
        }

        // 3. Logic for the FIRST cell of the turn (The Anchor)
        if (currentCellsSelection.Count == 0)
        {
            if (!IsValidAnchor(data, currentBoard))
            {
                lastSystemMessage = "<color=red>Invalid start: Must be middle row or adjacent to marked cells!</color>";
                DebugBoardStatus(currentBoard);
                RefreshDebug();
                return;
            }
        }
        // 4. Logic for SUBSEQUENT cells (The Chain)
        else
        {
            if (!IsAdjacentToCurrentSelection(data))
            {
                lastSystemMessage = "<color=red>Invalid path: Must be adjacent to your current selection!</color>";
                DebugBoardStatus(currentBoard);
                RefreshDebug();
                return;
            }
        }

        // 5. Limit check
        if (currentCellsSelection.Count >= activeNumber) return;

        // Everything is valid!
        currentCellsSelection.Add(clickedCell);
        clickedCell.SetSelectedVisual(true);
        RefreshDebug();
    }

    // Helper: Checks if the first click is valid
    private bool IsValidAnchor(CellData data, BoardManager currentBoard)
    {
        if (currentBoard == null) return false;

        bool isFirstTurn = !HasAnyMarkedCells(currentBoard);

        if (isFirstTurn)
        {
            bool isMiddleRow = (data.Row == BoardGenerator.MIDDLE_ROW);
            return isMiddleRow;
        }
        else
        {
            bool isAdjacent = IsAdjacentToMarked(data, currentBoard);
            return isAdjacent;
        }
    }

    // Helper: Checks if a new cell is adjacent to the chain being built
    private bool IsAdjacentToCurrentSelection(CellData data)
    {
        foreach (var selectedView in currentCellsSelection)
        {
            if (AreAdjacent(data, selectedView.LogicData)) return true;
        }
        return false;
    }

    private bool HasAnyMarkedCells(BoardManager board)
    {
        // Seguretat: Si el board és nul, no hi ha cel·les marcades
        if (board == null || board.BoardData == null) return false;

        // Iterem EXCLUSIVAMENT sobre el BoardData d'aquest board concret
        // (Això evita que s'equivoqui amb el tauler de l'altre jugador)
        foreach (var cell in board.BoardData)
        {
            if (cell.IsMarked) return true;
        }

        return false;
    }

    private bool IsAdjacentToMarked(CellData data, BoardManager currentBoard)
    {
        int[] dr = { -1, 1, 0, 0 };
        int[] dc = { 0, 0, -1, 1 };

        for (int i = 0; i < 4; i++)
        {
            int nr = data.Row + dr[i];
            int nc = data.Col + dc[i];

            if (nr >= 0 && nr < BoardGenerator.ROWS && nc >= 0 && nc < BoardGenerator.COLS)
            {
                bool isMarked = currentBoard.BoardData[nr, nc].IsMarked;
                if (isMarked)
                {
                    return true;
                }
            }
        }

        // Si arribem aquí, no hem trobat res. Loguejem les coordenades per comparar
        return false;
    }

    public void DebugBoardStatus(BoardManager board)
    {
        int markedCount = 0;
        for (int r = 0; r < BoardGenerator.ROWS; r++)
        {
            for (int c = 0; c < BoardGenerator.COLS; c++)
            {
                if (board.BoardData[r, c].IsMarked) markedCount++;
            }
        }
    }

    public void ConfirmSelection()
    {
        bool isValid = false;

        // 1. Validem la lògica segons si és comodí o no
        if (isNumberWildcard || isColorWildcard)
        {
            if(GameManager.Instance.CurrentActivePlayer.wildcardsRemaining <= 0)
            {
                lastSystemMessage = "<color=red>No wildcards available! Cannot use wildcard selection.</color>";
                RefreshDebug();
                return;
            }

            // Si és comodí (el 6), permetem seleccionar entre 1 i 6 caselles (ajusta el límit si cal)
            isValid = (currentCellsSelection.Count > 0 && currentCellsSelection.Count < 6);
        }
        else
        {
            // Si no és comodí, ha de ser exactament el número del dau
            isValid = (currentCellsSelection.Count == activeNumber);
        }

        // 2. Executem l'acció segons el resultat
        if (isValid)
        {
            foreach (var cell in currentCellsSelection)
            {
                cell.LogicData.IsMarked = true;
                cell.UpdateMarkedVisual();
                cell.SetSelectedVisual(false);
            }
            
            // 2. GESTIÓ DE COMODINS (WILDCARDS)
            int wildcardsToConsume = 0;

            // Si estem utilitzant el dau de 6, gastem 1 comodí
            if (isNumberWildcard) wildcardsToConsume++;

            // Si estem utilitzant el color negre, gastem 1 comodí
            if (isColorWildcard) wildcardsToConsume++;

            if (wildcardsToConsume > 0)
            {
                // Cridem al mètode que ja tens al GameManager per restar-los
                GameManager.Instance.ConsumeWildcards(wildcardsToConsume);
            }

            GameManager.Instance.ManageMarkedStars(currentCellsSelection, GameManager.Instance.CurrentActivePlayer);
            
            ScoreManager.Instance.ProcessTurnScoring(GameManager.Instance.CurrentActivePlayer, currentBoard);




            RefreshDebug();
            ResetDiceSelectionState();

            currentCellsSelection.Clear();
            lastSystemMessage = "<color=green>Selection Confirmed! Turn ended.</color>";
            GameManager.Instance.EndTurn();

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
        
        
    }



    public void CancelSelection()
    {
        foreach (var cell in currentCellsSelection)
        {
            cell.SetSelectedVisual(false);
        }
        currentCellsSelection.Clear();
        RefreshDebug();
    }

    // Mathematical adjacency check (Orthogonal only)
    private bool AreAdjacent(CellData a, CellData b)
    {
        return Mathf.Abs(a.Row - b.Row) + Mathf.Abs(a.Col - b.Col) == 1;
    }

    private void RefreshDebug()
    {
        debugUI.Refresh(
            activeNumber,
            activeColor,
            currentCellsSelection.Count,
            lastSystemMessage
        );
    }

    public void SetPendingNumber(int number)
    {
        pendingNumber = number;
        TryActivateDice();
    }

    public void SetPendingColor(CellColor color)
    {
        pendingColor = color;
        TryActivateDice();
    }

    private void TryActivateDice()
    {
        if (!pendingNumber.HasValue || !pendingColor.HasValue)
            return;

        SetActiveDice(
            pendingNumber.Value,
            pendingColor.Value,
            pendingNumber.Value == 6,
            pendingColor.Value == CellColor.Black
        );

        //pendingNumber = null;
        //pendingColor = null;
    }

    // Clears the active and pending dice data for the current selection session
    public void ResetDiceSelectionState()
    {
        pendingNumber = null;
        pendingColor = null;
        activeNumber = null;
        activeColor = null;
        areDiceSelected = false;

        // Optional: clear any visual cell pre-selections just in case
        currentCellsSelection.Clear();

        RefreshDebug();
    }

}