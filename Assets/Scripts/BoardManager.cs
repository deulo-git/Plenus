using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using static UnityEngine.Rendering.DebugUI.Table;

public class BoardManager : MonoBehaviour
{
    [Header("Dependencies")]
    public Transform gridContainer;
    public GameObject cellPrefab;
    public ColorPalette colorPalette;
    public GameObject starPrefab;
    public GameObject framePrefab;

    // Locked to 15x7
    private const int ROWS = 15;
    private const int COLS = 7;
    private const int MIDDLE_ROW = (ROWS - 1) / 2;
    private CellData[,] boardData;
    private List<CellView> allCellViews = new List<CellView>();

    private Stopwatch stopwatch = new Stopwatch();
    private const long MAX_EXECUTION_TIME_MS = 500;
    private const int MAX_RETRIES = 100;

    public void GenerateBoard()
    {
        ClearBoard();
        SetupGridLayout();
        SpawnCells();

        if (TryGenerateOrganicBoard())
        {
            // 1. Primer marquem les estrelles a les dades lògiques
            var clusters = GetColorClusters();
            DistributeStars(clusters);

            // 2. Després enllacem la lògica amb la vista (això ja té el teu Instantiate)
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
    }

    private void LinkLogicToViews()
    {
        // Safety check
        if (allCellViews.Count < (ROWS * COLS))
        {
            UnityEngine.Debug.LogError($"CRITICAL: Expected {ROWS * COLS} cells, but only spawned {allCellViews.Count}.");
            return;
        }

        int viewIndex = 0;
        for (int r = 0; r < ROWS; r++)
        {
            for (int c = 0; c < COLS; c++)
            {
                // Safety: Ensure we don't go out of bounds
                if (viewIndex < allCellViews.Count)
                {
                    CellView cv = allCellViews[viewIndex];
                    CellData data = boardData[r, c];

                    cv.Initialize(data, colorPalette.GetColor(data.Color));

                    if (boardData[r, c].HasStar)
                    {
                        GameObject star = Instantiate(starPrefab, cv.transform);
                        // Opcional: posa-la al centre o fes-la filla del transform de la cel·la
                        star.transform.localPosition = Vector3.zero;
                    }
                    // 2. Lògica per als marcs (Frame)
                    if (r == MIDDLE_ROW && framePrefab != null)
                    {
                        GameObject frame = Instantiate(framePrefab, cv.transform, false);
                        // Si el marc té una mida diferent, ajusta la posició:
                        frame.transform.localPosition = Vector3.zero;
                    }

                    viewIndex++;
                }
            }
        }
    }


    private void SetupGridLayout()
    {
        GridLayoutGroup gridLayout = gridContainer.GetComponent<GridLayoutGroup>();
        if (gridLayout != null)
        {
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = COLS;
        }
    }

    private void SpawnCells()
    {
        for (int i = 0; i < ROWS * COLS; i++)
        {
            GameObject newCell = Instantiate(cellPrefab, gridContainer);
            CellView cv = newCell.GetComponent<CellView>();
            if (cv != null) allCellViews.Add(cv);
        }
    }

    private bool TryGenerateOrganicBoard()
    {
        for (int attempt = 0; attempt < MAX_RETRIES; attempt++)
        {
            stopwatch.Restart();
            int[,] ownerGrid = new int[ROWS, COLS];

            // Fixed recipe: 6, 5, 4, 3, 2, 1 for each of the 5 colors
            Dictionary<int, int> remainingSizes = new Dictionary<int, int> {
                {6, 5}, {5, 5}, {4, 5}, {3, 5}, {2, 5}, {1, 5}
            };

            int steps = 0;
            if (SolvePartition(ownerGrid, remainingSizes, 1, ref steps))
            {
                return TryColorGraph(ownerGrid);
            }
        }
        return false;
    }

    private bool TryColorGraph(int[,] ownerGrid)
    {
        Dictionary<int, Region> regions = new Dictionary<int, Region>();
        for (int r = 0; r < ROWS; r++)
        {
            for (int c = 0; c < COLS; c++)
            {
                int id = ownerGrid[r, c];
                if (!regions.ContainsKey(id)) regions[id] = new Region { id = id, size = 0 };
                regions[id].size++;

                int[] dx = { -1, 1, 0, 0 }, dy = { 0, 0, -1, 1 };
                for (int i = 0; i < 4; i++)
                {
                    int nx = r + dx[i], ny = c + dy[i];
                    if (nx >= 0 && nx < ROWS && ny >= 0 && ny < COLS)
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
            boardData = new CellData[ROWS, COLS];
            for (int r = 0; r < ROWS; r++)
            {
                for (int c = 0; c < COLS; c++)
                {
                    boardData[r, c] = new CellData(regions[ownerGrid[r, c]].color);
                }
            }
            return true;
        }
        return false;
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

    class Region
    {
        public int id;
        public int size;
        public HashSet<int> neighbors = new HashSet<int>();
        public CellColor color = (CellColor)(-1);
    }


    // =========================================================
    // UTILITY METHODS
    // =========================================================
    private Vector2Int FindFirstEmpty(int[,] board)
    {
        for (int r = 0; r < ROWS; r++)
            for (int c = 0; c < COLS; c++)
                if (board[r, c] == 0) return new Vector2Int(r, c);
        return new Vector2Int(-1, -1);
    }

    private int GetConnectedEmptySpaceSize(int[,] board, Vector2Int start)
    {
        bool[,] visited = new bool[ROWS, COLS];
        return FloodFillEmpty(board, start, visited);
    }

    private bool IsBoardViable(int[,] board, int minSize)
    {
        bool[,] visited = new bool[ROWS, COLS];
        for (int r = 0; r < ROWS; r++)
        {
            for (int c = 0; c < COLS; c++)
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
                if (nx >= 0 && nx < ROWS && ny >= 0 && ny < COLS && board[nx, ny] == 0 && !visited[nx, ny])
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
            if (nx >= 0 && nx < ROWS && ny >= 0 && ny < COLS && board[nx, ny] == 0)
                initialAvailable.Add(new Vector2Int(nx, ny));
        }

        ExpandShape(initialShape, initialAvailable, targetSize, board, seen, result);
        return result;
    }

    private void ExpandShape(List<Vector2Int> shape, HashSet<Vector2Int> available, int targetSize, int[,] board, HashSet<string> seen, List<List<Vector2Int>> result)
    {
        // 1. Base case: If shape reaches target size, store it
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

        // 2. Get current tail to identify growth candidates
        Vector2Int last = shape.Last();
        var candidates = available.Where(n => IsAdjacent(n, last)).ToList();

        // 3. Sort candidates using a "Surface Score" (clumping heuristic)
        // - Higher connectivity is prioritized (score -= connections)
        // - UnityEngine.Random.value introduces necessary organic "noise"
        var sortedCandidates = candidates.OrderBy(n => {
            int connections = 0;
            int[] dx = { -1, 1, 0, 0 }, dy = { 0, 0, -1, 1 };
            for (int i = 0; i < 4; i++)
            {
                if (shape.Contains(n + new Vector2Int(dx[i], dy[i]))) connections++;
            }
            return (connections * -1.0f) + UnityEngine.Random.value;
        }).ToList();

        // 4. Backtracking recursion
        foreach (var nextCell in sortedCandidates)
        {
            shape.Add(nextCell);
            available.Remove(nextCell);

            // Track added neighbors to restore them later
            List<Vector2Int> newlyAdded = new List<Vector2Int>();
            int[] dx = { -1, 1, 0, 0 }, dy = { 0, 0, -1, 1 };

            for (int i = 0; i < 4; i++)
            {
                Vector2Int neighbor = new Vector2Int(nextCell.x + dx[i], nextCell.y + dy[i]);
                if (neighbor.x >= 0 && neighbor.x < 15 && neighbor.y >= 0 && neighbor.y < 7
                    && board[neighbor.x, neighbor.y] == 0 && !shape.Contains(neighbor) && !available.Contains(neighbor))
                {
                    available.Add(neighbor);
                    newlyAdded.Add(neighbor);
                }
            }

            ExpandShape(shape, available, targetSize, board, seen, result);

            // 5. Backtrack: Reset state for next iteration
            shape.RemoveAt(shape.Count - 1);
            available.Add(nextCell);
            foreach (var n in newlyAdded) available.Remove(n);
        }
    }

    // Ensure this helper is present for the adjacency logic
    private bool IsAdjacent(Vector2Int n, Vector2Int last)
    {
        // For even more organic growth, you can relax this to 
        // Math.Abs(n.x - last.x) <= 1 && Math.Abs(n.y - last.y) <= 1
        int dx = Math.Abs(n.x - last.x);
        int dy = Math.Abs(n.y - last.y);
        return (dx + dy == 1);
    }

    // BoardManager.cs
    public void DistributeStars(Dictionary<CellColor, List<List<Vector2Int>>> colorClusters)
    {
        foreach (var colorEntry in colorClusters)
        {
            var clusters = colorEntry.Value;
            if (clusters.Count < 2) continue;

            var shuffled = clusters.OrderBy(x => UnityEngine.Random.value).ToList();

            for (int i = 0; i < 2; i++)
            {
                var targetCluster = shuffled[i];
                Vector2Int randomCellPos = targetCluster[UnityEngine.Random.Range(0, targetCluster.Count)];

                // Just mutate the data, no logs here
                boardData[randomCellPos.x, randomCellPos.y].HasStar = true;
            }
        }
    }

    /// <summary>
    /// Scans the final boardData and returns a dictionary where each color 
    /// maps to a list of clusters (each cluster is a List of coordinates).
    /// </summary>
    public Dictionary<CellColor, List<List<Vector2Int>>> GetColorClusters()
    {
        var colorClusters = new Dictionary<CellColor, List<List<Vector2Int>>>();
        foreach (CellColor color in Enum.GetValues(typeof(CellColor)))
            colorClusters[color] = new List<List<Vector2Int>>();

        bool[,] visited = new bool[ROWS, COLS];

        for (int r = 0; r < ROWS; r++)
        {
            for (int c = 0; c < COLS; c++)
            {
                if (!visited[r, c])
                {
                    CellColor color = boardData[r, c].Color;
                    List<Vector2Int> cluster = new List<Vector2Int>();
                    FloodFill(r, c, color, visited, cluster);
                    colorClusters[color].Add(cluster);
                }
            }
        }
        return colorClusters;
    }

    private void FloodFill(int r, int c, CellColor color, bool[,] visited, List<Vector2Int> cluster)
    {
        if (r < 0 || r >= ROWS || c < 0 || c >= COLS || visited[r, c] || boardData[r, c].Color != color)
            return;

        visited[r, c] = true;
        cluster.Add(new Vector2Int(r, c));

        int[] dr = { -1, 1, 0, 0 }, dc = { 0, 0, -1, 1 };
        for (int i = 0; i < 4; i++)
        {
            FloodFill(r + dr[i], c + dc[i], color, visited, cluster);
        }
    }

    public CellData[,] GetBoardData() => boardData;
}