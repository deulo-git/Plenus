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

    public override void OnNetworkSpawn()
    {
        Debug.Log(
            $"{name} parent = {(transform.parent != null ? transform.parent.name : "NULL")}"
        );

        RectTransform rt = GetComponent<RectTransform>();

        Debug.Log(
            $"{name} anchoredPos={rt.anchoredPosition} size={rt.sizeDelta}"
        );

        // Aquí pots afegir la lògica per saber si aquest és EL TEU tauler o el de l'oponent
        if (IsOwner)
        {
            Debug.Log($"[Network] This is my board! (Client ID: {OwnerClientId})");
            // Pots cridar el GenerateBoard() aquí o des del GameManager
        }

        base.OnNetworkSpawn();

        // When this board spawns on ANY computer (Host or Client), 
        // it tells the GameManager who it belongs to.
        if (GameManager.Instance != null)
        {
            GameManager.Instance.RegisterBoard(OwnerClientId, this);
        }
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
                        GameObject star = Instantiate(starPrefab, cv.transform);
                        star.transform.SetAsFirstSibling();
                        star.transform.localPosition = Vector3.zero;
                    }

                    // Logic for the middle row frames
                    if (r == BoardGenerator.MIDDLE_ROW && framePrefab != null)
                    {
                        GameObject frame = Instantiate(framePrefab, cv.transform, false);
                        frame.transform.localPosition = Vector3.zero;
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