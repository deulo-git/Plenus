using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Assets.Scripts
{
    public class BoardGenerator
    {
        public const int ROWS = 15;
        public const int COLS = 7;
        public const int MIDDLE_ROW = (ROWS - 1) / 2;
        private const long MAX_EXECUTION_TIME_MS = 500;
        private const int MAX_RETRIES = 100;

        public const int STAR_COLOR = 3; //STARS FOR COLOUR
        private Stopwatch stopwatch = new Stopwatch();
        private GridUtils gridUtils;

        private CellData[,] boardData;

        public BoardGenerator()
        {
            gridUtils = new GridUtils();
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
        public CellData[,] GenerateValidBoard()
        {
            // Initialize the matrix memory
            boardData = new CellData[ROWS, COLS];

            // Start the generation process
            bool isSuccessful = TryGenerateOrganicBoard();

            if (isSuccessful)
            {
                // Note: If your star distribution logic (DistributeStars) 
                // is inside BoardGenerator, call it here before returning.
                var clusters = GetColorClusters();
                DistributeStars(clusters);


                return boardData; // Send the finished matrix back to BoardManager
            }

            UnityEngine.Debug.LogError("Generation failed after maximum retries.");
            return null;
        }

        private bool SolvePartition(int[,] board, Dictionary<int, int> remainingSizes, int currentGroupId, ref int steps)
        {
            // --- CIRCUIT BREAKERS ---
            if (steps++ > 10000) return false; // Iteration limit

            // CADA 50 passes comprovem si hem superat el temps límit
            if (steps++ % 50 == 0 && stopwatch.ElapsedMilliseconds > MAX_EXECUTION_TIME_MS) return false;


            Vector2Int start = gridUtils.FindFirstEmpty(board);
            if (start.x == -1) return true; // Board is full, success!

            var sizesToTry = remainingSizes.Keys.Where(k => remainingSizes[k] > 0).OrderByDescending(x => x).ToList();
            if (sizesToTry.Count == 0) return true;

            int minLeft = sizesToTry.Min();
            int emptySpaceSize = gridUtils.GetConnectedEmptySpaceSize(board, start);

            // Pruning: The current empty hole is smaller than our smallest remaining piece
            if (emptySpaceSize < minLeft) return false;

            foreach (int size in sizesToTry)
            {
                if (emptySpaceSize < size) continue;

                List<List<Vector2Int>> shapes = GenerateAllShapes(board, start, size);
                gridUtils.Shuffle(shapes);

                foreach (var shape in shapes)
                {
                    // Place piece
                    foreach (var c in shape) board[c.x, c.y] = currentGroupId;
                    remainingSizes[size]--;

                    // Check viability
                    int currentMin = remainingSizes.Where(kvp => kvp.Value > 0).Select(kvp => kvp.Key).DefaultIfEmpty(0).Min();
                    if (currentMin == 0 || gridUtils.IsBoardViable(board, currentMin))
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
            // Convertim l'Enum a un array de CellColor
            CellColor[] allColors = (CellColor[])Enum.GetValues(typeof(CellColor));

            // Recorrem l'array fins a l'últim element (Length - 1)
            for (int i = 0; i < allColors.Length - 1; i++)
            {
                CellColor c = allColors[i];
                colorUsage[c] = new HashSet<int>();
            }
            if (SolveColoring(regionList, 0, colorUsage))
            {
                boardData = new CellData[ROWS, COLS];
                for (int r = 0; r < ROWS; r++)
                {
                    for (int c = 0; c < COLS; c++)
                    {
                        boardData[r, c] = new CellData(regions[ownerGrid[r, c]].color, r, c);
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

            // Filtrem el Black aquí mateix
            var allColors = (CellColor[])Enum.GetValues(typeof(CellColor));
            var shuffledColors = allColors.Where(c => c != CellColor.Black).ToList();


            gridUtils.Shuffle(shuffledColors);



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
            var candidates = available.Where(n => gridUtils.IsAdjacent(n, last)).ToList();

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

        // Inside BoardGenerator.cs, where you place the stars:
        private void DistributeStars(Dictionary<CellColor, List<List<Vector2Int>>> colorClusters)
        {
            List<Vector2Int> allStarPositions = new List<Vector2Int>();

            foreach (var kvp in colorClusters)
            {
                CellColor color = kvp.Key;

                // 1. Gather all valid cells for this color that don't already have a star
                List<Vector2Int> availableCells = new List<Vector2Int>();
                foreach (var cluster in kvp.Value)
                {
                    foreach (var cell in cluster)
                    {
                        if (!boardData[cell.x, cell.y].HasStar)
                        {
                            availableCells.Add(cell);
                        }
                    }
                }

                int starsPlaced = 0;

                // 2. Place stars until we hit the limit or run out of valid cells
                while (starsPlaced < STAR_COLOR && availableCells.Count > 0)
                {
                    Vector2Int chosenCell;

                    if (allStarPositions.Count == 0)
                    {
                        // First star on the whole board can be placed anywhere purely at random
                        int randomIndex = UnityEngine.Random.Range(0, availableCells.Count);
                        chosenCell = availableCells[randomIndex];
                    }
                    else
                    {
                        // For subsequent stars, score every available cell based on its distance 
                        // to the CLOSEST existing star. We want to MAXIMIZE this distance.
                        var scoredCells = availableCells.Select(cell =>
                        {
                            // Using Euclidean distance (Vector2.Distance) for true visual distance
                            float minDistanceToAnyStar = allStarPositions.Min(star => Vector2.Distance(cell, star));
                            return new { Cell = cell, Score = minDistanceToAnyStar };
                        })
                        .OrderByDescending(x => x.Score)
                        .ToList();

                        // To maintain organic randomness (so games aren't identically predictable),
                        // we pick randomly from the top 3 furthest candidates.
                        int candidatesToConsider = Mathf.Min(3, scoredCells.Count);
                        int chosenIndex = UnityEngine.Random.Range(0, candidatesToConsider);

                        chosenCell = scoredCells[chosenIndex].Cell;
                    }

                    // 3. Apply the chosen star to the board data
                    boardData[chosenCell.x, chosenCell.y].HasStar = true;
                    allStarPositions.Add(chosenCell);
                    availableCells.Remove(chosenCell); // Remove so we don't pick it again
                    starsPlaced++;
                }
            }
        }

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

        class Region
        {
            public int id;
            public int size;
            public HashSet<int> neighbors = new HashSet<int>();
            public CellColor color = (CellColor)(-1);
        }



    }
}

