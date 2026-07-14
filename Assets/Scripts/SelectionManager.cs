using Assets.Scripts;
using System.Collections.Generic;
using System.Text;
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

    public void SetActiveBoard(BoardManager board)
    {
        currentBoard = board;
    }

    public void ResetDiceSelection()
    {
        areDiceSelected = false;
        CancelSelection();
        lastSystemMessage = "Roll the dice and select ONE number and ONE color.";
        RefreshDebug();
    }

    public void SetActiveDice(int number, CellColor color, bool isNumberWildcard, bool isColorWildcard)
    {
        activeNumber = number;
        activeColor = color;
        this.isNumberWildcard = isNumberWildcard;
        this.isColorWildcard = isColorWildcard;
        areDiceSelected = true;
        lastSystemMessage = $"Dice selected: {number} & {color}. Start picking cells.";
        RefreshDebug();
        NotifyPreview();
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
            currentCellsSelection.Remove(clickedCell);
            clickedCell.SetSelectedVisual(false);

            if (currentCellsSelection.Count == 0)
            {
                lastSystemMessage = $"Dice selected: {activeNumber} & {activeColor}. Start picking cells.";
            }
            else
            {
                lastSystemMessage = $"Selected {currentCellsSelection.Count} cells. Waiting to confirm or pick more...";
            }

            RefreshDebug();
            NotifyPreview();
            return;
        }

        // --- 2. SELECTION LOGIC ---
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
            isColorMatch = (data.Color == activeColor);
        }
        else
        {
            if (currentCellsSelection.Count == 0)
            {
                isColorMatch = true;
            }
            else
            {
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
        NotifyPreview();
    }

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
        if (board == null || board.BoardData == null) return false;

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

    // ------------------------------------------------------------------
    // ONLINE: confirming no longer marks/scores locally. It validates the
    // selection, then sends it to the HOST, which applies the marks, scores,
    // and advances the turn authoritatively. The marks then come back to every
    // machine (including this one) via the network.
    // ------------------------------------------------------------------
    public void ConfirmSelection()
    {
        // Only the player whose turn it is may confirm.
        if (GameManager.Instance != null && !GameManager.Instance.IsMyTurn)
        {
            lastSystemMessage = "<color=orange>It's not your turn.</color>";
            RefreshDebug();
            return;
        }

        bool isValid;

        if (isNumberWildcard || isColorWildcard)
        {
            if (GameManager.Instance.CurrentActivePlayer.wildcardsRemaining <= 0)
            {
                lastSystemMessage = "<color=red>No wildcards available! Cannot use wildcard selection.</color>";
                RefreshDebug();
                return;
            }

            isValid = (currentCellsSelection.Count > 0 && currentCellsSelection.Count < 6);
        }
        else
        {
            isValid = (currentCellsSelection.Count == activeNumber);
        }

        if (!isValid)
        {
            if (isNumberWildcard)
                lastSystemMessage = "<color=orange>Warning: Wildcard (6) requires selecting 1-6 cells.</color>";
            else
                lastSystemMessage = $"<color=orange>Warning: You must select EXACTLY {activeNumber} cells.</color>";

            RefreshDebug();
            return;
        }

        // Gather the selected cell indices (row-major) to send to the host.
        int[] cellIndices = new int[currentCellsSelection.Count];
        for (int i = 0; i < currentCellsSelection.Count; i++)
        {
            CellData d = currentCellsSelection[i].LogicData;
            cellIndices[i] = d.Row * BoardGenerator.COLS + d.Col;
        }

        int number = activeNumber ?? 0;
        int colorInt = (int)(activeColor ?? CellColor.Black);
        int numIndex = DiceManager.Instance != null ? DiceManager.Instance.SelectedNumericIndex : -1;
        int colorIndex = DiceManager.Instance != null ? DiceManager.Instance.SelectedColorIndex : -1;

        // Clear the on-screen selection highlight now; the actual marks (crosses) will
        // arrive from the host and be applied on every machine.
        foreach (var cell in currentCellsSelection)
        {
            cell.SetSelectedVisual(false);
        }

        GameManager.Instance.CommitSelection(cellIndices, number, colorInt, isNumberWildcard, isColorWildcard, numIndex, colorIndex);

        ResetDiceSelectionState();
        currentCellsSelection.Clear();
        lastSystemMessage = "<color=green>Selection sent!</color>";
        RefreshDebug();
    }

    public void CancelSelection()
    {
        foreach (var cell in currentCellsSelection)
        {
            cell.SetSelectedVisual(false);
        }
        currentCellsSelection.Clear();
        RefreshDebug();
        NotifyPreview();
    }

    private bool AreAdjacent(CellData a, CellData b)
    {
        return Mathf.Abs(a.Row - b.Row) + Mathf.Abs(a.Col - b.Col) == 1;
    }

    private void RefreshDebug()
    {
        if (debugUI != null)
        {
            debugUI.Refresh(
                activeNumber,
                activeColor,
                currentCellsSelection.Count,
                lastSystemMessage
            );
        }
    }

    // Broadcast the acting player's in-progress picks (chosen dice + highlighted cells)
    // so the opponent can see them live. Guarded to the player whose turn it is.
    private void NotifyPreview()
    {
        if (GameManager.Instance == null) return;
        if (!GameManager.Instance.IsMyTurn) return;

        int[] cells = new int[currentCellsSelection.Count];
        for (int i = 0; i < currentCellsSelection.Count; i++)
        {
            CellData d = currentCellsSelection[i].LogicData;
            cells[i] = d.Row * BoardGenerator.COLS + d.Col;
        }

        int number = activeNumber ?? 0;
        int colorInt = activeColor.HasValue ? (int)activeColor.Value : -1;
        GameManager.Instance.BroadcastPreview(cells, number, colorInt);
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
    }

    public void ResetDiceSelectionState()
    {
        pendingNumber = null;
        pendingColor = null;
        activeNumber = null;
        activeColor = null;
        areDiceSelected = false;

        currentCellsSelection.Clear();

        RefreshDebug();
        NotifyPreview();
    }
}
