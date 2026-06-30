using System;
using System.Collections.Generic;
using UnityEngine;

public class BoardManager : MonoBehaviour
{
    public Transform gridContainer;
    public ColorPalette colorPalette;

    private int rows = 15;
    private int cols = 7;
    private CellData[,] boardData;
    private List<CellView> allCellViews;

    void Start()
    {
        FindExistingCells();
        GenerateAndLinkBoard();
    }

    public void GenerateAndLinkBoard()
    {
        GenerateValidBoard();
        LinkLogicToViews();
    }

    private void FindExistingCells()
    {
        allCellViews = new List<CellView>();
        foreach (Transform child in gridContainer)
        {
            CellView cv = child.GetComponent<CellView>();
            if (cv != null) allCellViews.Add(cv);
        }
    }

    private void GenerateValidBoard()
    {
        int maxRetries = 200;
        for (int i = 0; i < maxRetries; i++)
        {
            if (TryGenerateOrganicBoard()) return;
        }
        Debug.LogError("No s'ha pogut generar un tauler amb formes orgàniques.");
    }

    private bool TryGenerateOrganicBoard()
    {
        boardData = new CellData[rows, cols];
        int[,] ownerGrid = new int[rows, cols];
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++) ownerGrid[r, c] = -1;

        List<int> sizes = new List<int>();
        for (int s = 1; s <= 6; s++) for (int i = 0; i < 5; i++) sizes.Add(s);
        Shuffle(sizes);

        List<Chunk> chunks = new List<Chunk>();
        for (int i = 0; i < sizes.Count; i++)
        {
            Chunk chunk = new Chunk { ID = i, Size = sizes[i] };

            // Trobar un punt inicial lliure
            Vector2Int startPos = FindRandomEmpty(ownerGrid);
            if (startPos.x == -1) return false;

            chunk.Cells.Add(startPos);
            ownerGrid[startPos.x, startPos.y] = i;

            // Creixement orgànic: afegir veïns aleatoris
            while (chunk.Cells.Count < chunk.Size)
            {
                List<Vector2Int> candidates = GetAvailableNeighbors(chunk.Cells, ownerGrid);
                if (candidates.Count == 0) break;
                Vector2Int next = candidates[UnityEngine.Random.Range(0, candidates.Count)];
                chunk.Cells.Add(next);
                ownerGrid[next.x, next.y] = i;
            }
            chunks.Add(chunk);
        }

        // Coloració (Constraint Satisfaction)
        if (ApplyColors(chunks, ownerGrid))
        {
            foreach (var ch in chunks)
            {
                foreach (var pos in ch.Cells) boardData[pos.x, pos.y] = new CellData((CellColor)ch.ColorIndex);
            }
            return true;
        }
        return false;
    }

    private Vector2Int FindRandomEmpty(int[,] grid)
    {
        List<Vector2Int> empties = new List<Vector2Int>();
        for (int r = 0; r < rows; r++) for (int c = 0; c < cols; c++) if (grid[r, c] == -1) empties.Add(new Vector2Int(r, c));
        return empties.Count > 0 ? empties[UnityEngine.Random.Range(0, empties.Count)] : new Vector2Int(-1, -1);
    }

    private List<Vector2Int> GetAvailableNeighbors(List<Vector2Int> currentCells, int[,] grid)
    {
        List<Vector2Int> neighbors = new List<Vector2Int>();
        int[] dr = { -1, 1, 0, 0 }, dc = { 0, 0, -1, 1 };
        foreach (var cell in currentCells)
        {
            for (int i = 0; i < 4; i++)
            {
                int nr = cell.x + dr[i], nc = cell.y + dc[i];
                if (nr >= 0 && nr < rows && nc >= 0 && nc < cols && grid[nr, nc] == -1)
                    if (!neighbors.Contains(new Vector2Int(nr, nc))) neighbors.Add(new Vector2Int(nr, nc));
            }
        }
        return neighbors;
    }

    private bool ApplyColors(List<Chunk> chunks, int[,] ownerGrid)
    {
        bool[,] used = new bool[5, 7];
        foreach (var ch in chunks)
        {
            List<int> validColors = new List<int> { 0, 1, 2, 3, 4 };
            Shuffle(validColors);
            bool placed = false;
            foreach (var col in validColors)
            {
                if (used[col, ch.Size]) continue;
                bool conflict = false;
                foreach (var cell in ch.Cells)
                {
                    int[] dr = { -1, 1, 0, 0 }, dc = { 0, 0, -1, 1 };
                    for (int i = 0; i < 4; i++)
                    {
                        int nr = cell.x + dr[i], nc = cell.y + dc[i];
                        if (nr >= 0 && nr < rows && nc >= 0 && nc < cols)
                        {
                            int neighborID = ownerGrid[nr, nc];
                            if (neighborID != -1 && neighborID != ch.ID && chunks[neighborID].ColorIndex == col) conflict = true;
                        }
                    }
                }
                if (!conflict)
                {
                    ch.ColorIndex = col;
                    used[col, ch.Size] = true;
                    placed = true;
                    break;
                }
            }
            if (!placed) return false;
        }
        return true;
    }

    private void Shuffle<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            T temp = list[i];
            int r = UnityEngine.Random.Range(i, list.Count);
            list[i] = list[r]; list[r] = temp;
        }
    }

    public CellData[,] GetBoardData() => boardData;

    private void LinkLogicToViews()
    {
        int viewIndex = 0;
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                if (viewIndex < allCellViews.Count && boardData[r, c] != null)
                {
                    allCellViews[viewIndex].LinkLogic(boardData[r, c], colorPalette.GetColor(boardData[r, c].Color));
                    viewIndex++;
                }
            }
    }

    private class Chunk
    {
        public int ID;
        public int Size;
        public int ColorIndex = -1;
        public List<Vector2Int> Cells = new List<Vector2Int>();
    }
}