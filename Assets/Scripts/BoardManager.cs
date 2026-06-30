using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class BoardManager : MonoBehaviour
{
    [Header("Dependencies")]
    public Transform gridContainer;
    public GameObject cellPrefab;
    public ColorPalette colorPalette;

    [Header("Board Settings")]
    private int rows = 15;
    private int cols = 7;
    private CellData[,] boardData;
    private List<CellView> allCellViews = new List<CellView>();

    // Circuit breaker variables
    private Stopwatch stopwatch = new Stopwatch();
    private const long MAX_EXECUTION_TIME_MS = 100; // Augmentat a 100ms per més marge
    private const int MAX_RETRIES = 50; // Total attempts before giving up

    void Start()
    {
        // Board generation will be triggered externally via TestingConsole
    }

    /// <summary>
    /// Main entry point to generate a new dynamic board.
    /// </summary>
    public void GenerateAndLinkBoard(int customRows, int customCols)
    {
        this.rows = customRows;
        this.cols = customCols;

        ClearBoard();
        SetupGridLayout();
        SpawnCells();

        if (GenerateValidBoard())
        {
            LinkLogicToViews();
        }
        else
        {
            UnityEngine.Debug.LogError("Generation failed even with dynamic sizing.");
        }
    }

    private void ClearBoard()
    {
        foreach (Transform child in gridContainer)
        {
            Destroy(child.gameObject);
        }
        allCellViews.Clear();
    }

    private void SetupGridLayout()
    {
        GridLayoutGroup gridLayout = gridContainer.GetComponent<GridLayoutGroup>();
        if (gridLayout != null)
        {
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = cols;
        }
    }

    private void SpawnCells()
    {
        int totalCells = rows * cols;
        for (int i = 0; i < totalCells; i++)
        {
            GameObject newCell = Instantiate(cellPrefab, gridContainer);
            CellView cv = newCell.GetComponent<CellView>();
            if (cv != null) allCellViews.Add(cv);
        }
    }

    private bool GenerateValidBoard()
    {
        for (int i = 0; i < MAX_RETRIES; i++)
        {
            if (TryGenerateOrganicBoard()) return true;
        }
        return false;
    }

    // =========================================================
    // CORE LOGIC: Backtracking Generation (Phases A & B)
    // =========================================================
    private bool TryGenerateOrganicBoard()
    {
        stopwatch.Restart();
        int[,] ownerGrid = new int[rows, cols];
        int cellsPerColor = (rows * cols) / 5;

        // Log de inici per depuració
        UnityEngine.Debug.Log($"Attempting generation for {rows}x{cols}...");

        // DYNAMIC RECIPE: This is the fix. We don't force [6,5,4,3,2,1].
        // We generate a valid sum of 21 (or target) dynamically.
        List<int> recipe = GenerateDynamicRecipe(cellsPerColor);

        // Convert recipe to frequency dictionary for the partition algorithm
        Dictionary<int, int> remainingSizes = new Dictionary<int, int>();
        foreach (int size in recipe)
        {
            if (!remainingSizes.ContainsKey(size)) remainingSizes[size] = 0;
            remainingSizes[size] += 5; // 5 colors
        }

        int steps = 0;
        if (SolvePartition(ownerGrid, remainingSizes, 1, ref steps))
        {
            return TryColorGraph(ownerGrid);
        }
        return false;
    }

    private List<int> GenerateDynamicRecipe(int totalCellsPerColor)
    {
        List<int> recipe = new List<int>();
        int remaining = totalCellsPerColor;

        // Classic pieces distribution approach
        while (remaining > 0)
        {
            int size = Mathf.Min(remaining, UnityEngine.Random.Range(2, 7));
            recipe.Add(size);
            remaining -= size;
        }
        return recipe;
    }

    /// <summary>
    /// Calculates the distribution of cluster sizes dynamically based on the 
    /// total cells available per color. 
    /// </summary>
    private Dictionary<int, int> DetermineClusterSizes(int cellsPerColor)
    {
        Dictionary<int, int> distribution = new Dictionary<int, int>();
        int remainingCells = cellsPerColor;

        // Try to fit standard cluster sizes from 6 down to 1
        // We want a mix of shapes for the "organic" look
        int[] availableSizes = { 6, 5, 4, 3, 2, 1 };

        int sizeIndex = 0;
        while (remainingCells > 0 && sizeIndex < availableSizes.Length)
        {
            int size = availableSizes[sizeIndex];

            // If we have enough cells to fit this size, add it
            if (remainingCells >= size)
            {
                if (!distribution.ContainsKey(size)) distribution[size] = 0;
                distribution[size]++;
                remainingCells -= size;
            }
            else
            {
                // If we can't fit this size, move to a smaller size
                sizeIndex++;
            }
        }

        // Multiply the distribution by 5 because we need the same architecture for all 5 colors
        Dictionary<int, int> globalDistribution = new Dictionary<int, int>();
        foreach (var kvp in distribution)
        {
            globalDistribution[kvp.Key] = kvp.Value * 5;
        }

        return globalDistribution;
    }

    private bool SolvePartition(int[,] board, Dictionary<int, int> remainingSizes, int currentGroupId, ref int steps)
    {
        // --- CIRCUIT BREAKERS ---
        if (steps++ > 10000) return false; // Iteration limit

        // CADA 50 passes comprovem si hem superat el temps límit
        if (steps++ % 50 == 0 && stopwatch.ElapsedMilliseconds > MAX_EXECUTION_TIME_MS) return false;
        

        Vector2Int start = FindFirstEmpty(board);
        if (start.x == -1) return true; // Board is full, success!

        var sizesToTry = remainingSizes.Keys.Where(k => remainingSizes[k] > 0).OrderByDescending(x => x).ToList();
        if (sizesToTry.Count == 0) return true;

        int minLeft = sizesToTry.Min();
        int emptySpaceSize = GetConnectedEmptySpaceSize(board, start);

        // Pruning: The current empty hole is smaller than our smallest remaining piece
        if (emptySpaceSize < minLeft) return false;

        foreach (int size in sizesToTry)
        {
            if (emptySpaceSize < size) continue;

            List<List<Vector2Int>> shapes = GenerateAllShapes(board, start, size);
            Shuffle(shapes);

            foreach (var shape in shapes)
            {
                // Place piece
                foreach (var c in shape) board[c.x, c.y] = currentGroupId;
                remainingSizes[size]--;

                // Check viability
                int currentMin = remainingSizes.Where(kvp => kvp.Value > 0).Select(kvp => kvp.Key).DefaultIfEmpty(0).Min();
                if (currentMin == 0 || IsBoardViable(board, currentMin))
                {
                    if (SolvePartition(board, remainingSizes, currentGroupId + 1, ref steps))
                        return true;
                }

                // Backtrack
                foreach (var c in shape) board[c.x, c.y] = 0;
                remainingSizes[size]++;
            }
        }

        return false;
    }

    // =========================================================
    // GRAPH COLORING LOGIC
    // =========================================================
    class Region
    {
        public int id;
        public int size;
        public HashSet<int> neighbors = new HashSet<int>();
        public CellColor color = (CellColor)(-1);
    }

    private bool TryColorGraph(int[,] ownerGrid)
    {
        Dictionary<int, Region> regions = new Dictionary<int, Region>();
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                int id = ownerGrid[r, c];
                if (!regions.ContainsKey(id)) regions[id] = new Region { id = id, size = 0 };
                regions[id].size++;

                int[] dx = { -1, 1, 0, 0 }, dy = { 0, 0, -1, 1 };
                for (int i = 0; i < 4; i++)
                {
                    int nx = r + dx[i], ny = c + dy[i];
                    if (nx >= 0 && nx < rows && ny >= 0 && ny < cols)
                    {
                        int nid = ownerGrid[nx, ny];
                        if (nid != 0 && nid != id) regions[id].neighbors.Add(nid);
                    }
                }
            }
        }

        var regionList = regions.Values.ToList();
        var colorUsage = new Dictionary<CellColor, HashSet<int>>();
        foreach (CellColor c in Enum.GetValues(typeof(CellColor))) colorUsage[c] = new HashSet<int>();

        if (SolveColoring(regionList, 0, colorUsage))
        {
            boardData = new CellData[rows, cols];
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    boardData[r, c] = new CellData(regions[ownerGrid[r, c]].color);
                }
            }
            return true;
        }
        return false;
    }

    private bool SolveColoring(List<Region> regions, int index, Dictionary<CellColor, HashSet<int>> colorUsage)
    {
        if (index >= regions.Count) return true;

        Region reg = regions[index];
        var shuffledColors = ((CellColor[])Enum.GetValues(typeof(CellColor))).ToList();
        Shuffle(shuffledColors);

        foreach (CellColor c in shuffledColors)
        {
            // Do not use the same size twice for the same color
            if (colorUsage[c].Contains(reg.size)) continue;

            bool neighborConflict = false;
            foreach (int nId in reg.neighbors)
            {
                if (regions.Find(x => x.id == nId).color == c)
                {
                    neighborConflict = true;
                    break;
                }
            }

            if (!neighborConflict)
            {
                reg.color = c;
                colorUsage[c].Add(reg.size);

                if (SolveColoring(regions, index + 1, colorUsage)) return true;

                reg.color = (CellColor)(-1);
                colorUsage[c].Remove(reg.size);
            }
        }
        return false;
    }

    // =========================================================
    // UTILITY METHODS
    // =========================================================
    private Vector2Int FindFirstEmpty(int[,] board)
    {
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                if (board[r, c] == 0) return new Vector2Int(r, c);
        return new Vector2Int(-1, -1);
    }

    private int GetConnectedEmptySpaceSize(int[,] board, Vector2Int start)
    {
        bool[,] visited = new bool[rows, cols];
        return FloodFillEmpty(board, start, visited);
    }

    private bool IsBoardViable(int[,] board, int minSize)
    {
        bool[,] visited = new bool[rows, cols];
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                if (board[r, c] == 0 && !visited[r, c])
                {
                    if (FloodFillEmpty(board, new Vector2Int(r, c), visited) < minSize)
                        return false;
                }
            }
        }
        return true;
    }

    private int FloodFillEmpty(int[,] board, Vector2Int start, bool[,] visited)
    {
        Queue<Vector2Int> q = new Queue<Vector2Int>();
        q.Enqueue(start);
        visited[start.x, start.y] = true;
        int count = 0;
        int[] dx = { -1, 1, 0, 0 }, dy = { 0, 0, -1, 1 };

        while (q.Count > 0)
        {
            var curr = q.Dequeue();
            count++;
            for (int i = 0; i < 4; i++)
            {
                int nx = curr.x + dx[i], ny = curr.y + dy[i];
                if (nx >= 0 && nx < rows && ny >= 0 && ny < cols && board[nx, ny] == 0 && !visited[nx, ny])
                {
                    visited[nx, ny] = true;
                    q.Enqueue(new Vector2Int(nx, ny));
                }
            }
        }
        return count;
    }

    private List<List<Vector2Int>> GenerateAllShapes(int[,] board, Vector2Int start, int targetSize)
    {
        List<List<Vector2Int>> result = new List<List<Vector2Int>>();
        HashSet<string> seen = new HashSet<string>();

        List<Vector2Int> initialShape = new List<Vector2Int> { start };
        HashSet<Vector2Int> initialAvailable = new HashSet<Vector2Int>();

        int[] dx = { -1, 1, 0, 0 }, dy = { 0, 0, -1, 1 };
        for (int i = 0; i < 4; i++)
        {
            int nx = start.x + dx[i], ny = start.y + dy[i];
            if (nx >= 0 && nx < rows && ny >= 0 && ny < cols && board[nx, ny] == 0)
                initialAvailable.Add(new Vector2Int(nx, ny));
        }

        ExpandShape(initialShape, initialAvailable, targetSize, board, seen, result);
        return result;
    }

    private void ExpandShape(List<Vector2Int> shape, HashSet<Vector2Int> available, int targetSize, int[,] board, HashSet<string> seen, List<List<Vector2Int>> result)
    {
        if (shape.Count == targetSize)
        {
            var sorted = shape.OrderBy(v => v.x).ThenBy(v => v.y);
            string key = string.Join("|", sorted.Select(v => $"{v.x},{v.y}"));
            if (!seen.Contains(key))
            {
                seen.Add(key);
                result.Add(new List<Vector2Int>(shape));
            }
            return;
        }

        var availList = new List<Vector2Int>(available);
        foreach (var nextCell in availList)
        {
            shape.Add(nextCell);
            var nextAvailable = new HashSet<Vector2Int>(available);
            nextAvailable.Remove(nextCell);

            int[] dx = { -1, 1, 0, 0 }, dy = { 0, 0, -1, 1 };
            for (int i = 0; i < 4; i++)
            {
                int nx = nextCell.x + dx[i], ny = nextCell.y + dy[i];
                if (nx >= 0 && nx < rows && ny >= 0 && ny < cols && board[nx, ny] == 0)
                {
                    Vector2Int n = new Vector2Int(nx, ny);
                    if (!shape.Contains(n)) nextAvailable.Add(n);
                }
            }

            ExpandShape(shape, nextAvailable, targetSize, board, seen, result);
            shape.RemoveAt(shape.Count - 1); // Backtrack
        }
    }

    private void Shuffle<T>(IList<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            T temp = list[i];
            int r = UnityEngine.Random.Range(i, list.Count);
            list[i] = list[r];
            list[r] = temp;
        }
    }

    public CellData[,] GetBoardData() => boardData;

    private void LinkLogicToViews()
    {
        int viewIndex = 0;
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                if (viewIndex < allCellViews.Count && boardData != null && boardData[r, c] != null)
                {
                    allCellViews[viewIndex].LinkLogic(boardData[r, c], colorPalette.GetColor(boardData[r, c].Color));
                    viewIndex++;
                }
            }
        }
    }
}