using Assets.Scripts;
using System.Collections.Generic;
using System.Globalization;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class BoardManager : NetworkBehaviour
{
    [Header("Dependencies")]
    public Transform gridContainer;
    public GameObject cellPrefab;
    public ColorPalette colorPalette;
    public GameObject starPrefab;
    public GameObject framePrefab;

    private CellData[,] boardData;
    private List<CellView> allCellViews = new List<CellView>();
    private int markedCells = 0;

    // Public property to access the logical board data
    public CellData[,] BoardData
    {
        get { return boardData; }
        set { boardData = value; }
    }

    // ------------------------------------------------------------------
    // ONLINE NOTE:
    // Boards are no longer network-spawned/reparented (that was fragile and
    // caused the "NetworkObject can only be re-parented after being spawned!"
    // error and the invisible host board). Instead, GameManager instantiates
    // boards LOCALLY on each machine and feeds them the layout the server sent,
    // via BuildFromData(...). So OnNetworkSpawn is intentionally left inert.
    // ------------------------------------------------------------------
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
    }

    // Build (or rebuild) this board directly from a ready-made layout.
    // Works without any networking: pure local construction.
    public void BuildFromData(CellData[,] data)
    {
        boardData = data;
        ClearBoard();
        SetupGridLayout();
        SpawnCells();
        LinkLogicToViews();
    }

    // Mark a single cell (identified by its row-major index) as marked, and refresh its
    // visual. Used to apply moves that arrive over the network. Idempotent: marking an
    // already-marked cell does nothing harmful.
    public void MarkCellByIndex(int index)
    {
        if (index < 0 || index >= allCellViews.Count) return;

        CellView cv = allCellViews[index];
        if (cv != null && cv.LogicData != null)
        {
            cv.LogicData.IsMarked = true;
            cv.UpdateMarkedVisual();
        }
    }

    // Show a live "preview" selection on this board: clears all selection highlights,
    // then highlights the given cell indices. Used to mirror the opponent's in-progress
    // picks onto the opponent board (does not mark anything).
    public void SetPreviewSelection(int[] indices)
    {
        for (int i = 0; i < allCellViews.Count; i++)
        {
            if (allCellViews[i] != null) allCellViews[i].SetSelectedVisual(false);
        }

        if (indices == null) return;

        foreach (int idx in indices)
        {
            if (idx >= 0 && idx < allCellViews.Count && allCellViews[idx] != null)
                allCellViews[idx].SetSelectedVisual(true);
        }
    }

    // Make this board read-only (used for the opponent view) or interactive again.
    public void SetInteractable(bool interactable)
    {
        CanvasGroup cg = GetComponent<CanvasGroup>();
        if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();
        cg.interactable = interactable;
        cg.blocksRaycasts = interactable;
    }

    public void GenerateBoard()
    {
        ClearBoard();
        SetupGridLayout();
        SpawnCells();

        // 1. Delegate the heavy mathematical generation to the pure C# class
        BoardGenerator generator = new BoardGenerator();
        boardData = generator.GenerateValidBoard();

        if (boardData != null)
        {
            // 2. Link the logical data generated to the visual representations
            LinkLogicToViews();
        }
        else
        {
            UnityEngine.Debug.LogError("Generation failed for 15x7 board.");
        }
    }

    private void ClearBoard()
    {
        // Use DestroyImmediate to ensure they are gone before we spawn new ones
        for (int i = gridContainer.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(gridContainer.GetChild(i).gameObject);
        }
        allCellViews.Clear();
        markedCells = 0;
    }

    private void SetupGridLayout()
    {
        GridLayoutGroup gridLayout = gridContainer.GetComponent<GridLayoutGroup>();
        if (gridLayout != null)
        {
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = BoardGenerator.COLS;
        }
    }

    private void SpawnCells()
    {
        for (int i = 0; i < BoardGenerator.ROWS * BoardGenerator.COLS; i++)
        {
            GameObject newCell = Instantiate(cellPrefab, gridContainer);
            CellView cv = newCell.GetComponent<CellView>();
            if (cv != null) allCellViews.Add(cv);
        }
    }

    private void LinkLogicToViews()
    {
        // Safety check
        if (allCellViews.Count < (BoardGenerator.ROWS * BoardGenerator.COLS))
        {
            UnityEngine.Debug.LogError($"CRITICAL: Expected {BoardGenerator.ROWS * BoardGenerator.COLS} cells, but only spawned {allCellViews.Count}.");
            return;
        }

        int viewIndex = 0;
        for (int r = 0; r < BoardGenerator.ROWS; r++)
        {
            for (int c = 0; c < BoardGenerator.COLS; c++)
            {
                // Safety: Ensure we don't go out of bounds
                if (viewIndex < allCellViews.Count)
                {
                    CellView cv = allCellViews[viewIndex];
                    CellData data = boardData[r, c];

                    cv.Initialize(data, colorPalette.GetColor(data.Color), this);

                    if (data.HasStar)
                    {
                        // Place the star inside the cell's inner FillingObject (the painted
                        // area), not on the Cell root, so it sits within the margins.
                        // Falls back to the Cell itself if FillingObject isn't present.
                        Transform fillingObject = cv.transform.Find("FillingObject");
                        Transform starParent = fillingObject != null ? fillingObject : cv.transform;
                        GameObject star = Instantiate(starPrefab, starParent);
                        star.transform.SetAsFirstSibling();
                        star.transform.localPosition = Vector3.zero;
                    }

                    // Logic for the middle row frames
                    // Logic for the middle row frames
                    if (r == BoardGenerator.MIDDLE_ROW && framePrefab != null)
                    {
                        //GameObject frame = Instantiate(framePrefab, cv.transform, false);
                        //frame.transform.localPosition = Vector3.zero;

                        RectTransform fillingObject = cv.transform.Find("FillingObject") as RectTransform;
                        if (fillingObject != null)
                        {
                            // Assegura que està en stretch (ancoratges a les 4 cantonades),
                            // que és el que fa que Left/Right/Top/Bottom tinguin sentit.
                            fillingObject.anchorMin = Vector2.zero;   // (0,0)
                            fillingObject.anchorMax = Vector2.one;    // (1,1)

                            fillingObject.offsetMin = new Vector2(6f, 6f);    // Left = 6, Bottom = 6
                            fillingObject.offsetMax = new Vector2(-6f, -6f);  // Right = 6, Top = 6
                        }
                    }

                    viewIndex++;
                }
            }
        }
    }

    public int GetMarkedCellsCount()
    {
        // Recalculate from scratch to avoid infinite accumulation bugs
        markedCells = 0;
        if (boardData != null)
        {
            foreach (var cell in boardData)
            {
                if (cell != null && cell.IsMarked)
                {
                    markedCells++;
                }
            }
        }
        return markedCells;
    }

    internal void CountMarkedCells(int total)
    {
        markedCells += total;
    }

    public CellData[,] GetBoardData() => boardData;

    public void SetBoardData(CellData[,] data)
    {
        // Copy the data
        this.boardData = data;

        // Clean current view and setup the new one
        ClearBoard();
        SetupGridLayout();
        SpawnCells();

        // Link the new data visually
        LinkLogicToViews();
    }

    internal void ShareDataWith(CellData[,] sourceBoardData)
    {
        // 1. Initialize the matrix for the current board with the same dimensions
        boardData = new CellData[BoardGenerator.ROWS, BoardGenerator.COLS];

        // 2. Perform a DEEP COPY to ensure both players have independent logic states.
        for (int r = 0; r < BoardGenerator.ROWS; r++)
        {
            for (int c = 0; c < BoardGenerator.COLS; c++)
            {
                CellData sourceData = sourceBoardData[r, c];

                // Create a completely NEW instance with the static data (Color, Row, Col)
                CellData newData = new CellData(sourceData.Color, r, c);

                // Copy additional structural properties (like the Star)
                newData.HasStar = sourceData.HasStar;

                // Assign the independent instance to this board
                boardData[r, c] = newData;
            }
        }

        // 3. Render the board visually using the newly generated independent logic data
        ClearBoard();
        SpawnCells();
        LinkLogicToViews();
    }
}