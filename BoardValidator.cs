using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

// ELIMINAT el ": MonoBehaviour". Aquesta classe és lògica pura.
public class BoardValidator
{
    // BoardValidator.cs
    public bool ValidateBoard(CellData[,] board, Dictionary<CellColor, List<List<Vector2Int>>> clusters, out string report)
    {
        StringBuilder sb = new StringBuilder();
        bool isValid = true;

        foreach (CellColor color in Enum.GetValues(typeof(CellColor)))
        {
            // Skip the Wildcard/Black color as it is not a color present in the cluster data
            if (color == CellColor.Black) continue;

            var colorClusters = clusters[color];
            string colorName = Enum.GetName(typeof(CellColor), color);
            sb.AppendLine($"--- {colorName.ToUpper()} ---");

            // 1. Colour Distribution calculation
            int totalCells = colorClusters.Sum(c => c.Count);
            sb.AppendLine($"\tColour Distribution: <color=green>OK</color> ({totalCells} Cells)");

            // 2. Groups (6, 5, 4, 3, 2, 1)
            for (int size = 6; size >= 1; size--)
            {
                var groupsOfSize = colorClusters.FindAll(c => c.Count == size);
                int count = groupsOfSize.Count;
                bool isOk = (count == 1);

                string label = $"{size}-cells";
                sb.Append($"\t{label}: {(isOk ? "<color=green>OK</color>" : "<color=red>ERROR</color>")} ({count} Found)");

                // Check for stars in these groups
                if (count > 0)
                {
                    bool hasStar = groupsOfSize[0].Any(pos => board[pos.x, pos.y].HasStar);
                    if (hasStar) sb.Append(" {Star added}");
                }
                sb.AppendLine("");
            }
            sb.AppendLine("");
        }

        report = sb.ToString();
        return isValid;
    }

    public string GenerateStarReport(CellData[,] board, Dictionary<CellColor, List<List<Vector2Int>>> clusters)
    {
        StringBuilder sb = new StringBuilder();
        int totalGlobalStars = 0;


        foreach (var colorEntry in clusters)
        {
            CellColor color = colorEntry.Key;
            var clusterList = colorEntry.Value;

            // Preparem un buffer per a les línies d'aquest color
            StringBuilder colorLog = new StringBuilder();
            int starsForThisColor = 0;

            foreach (var cluster in clusterList)
            {
                // Comprovem si alguna cel·la d'aquest clúster té estrella
                bool hasStar = false;
                foreach (var pos in cluster)
                {
                    if (board[pos.x, pos.y].HasStar)
                    {
                        hasStar = true;
                        break;
                    }
                }

                if (hasStar)
                {
                    starsForThisColor++;
                    colorLog.AppendLine($"Added 1 star into cluster ({cluster.Count} cells)");
                }
            }

            // Si hem trobat estrelles per a aquest color, afegim la capçalera
            if (starsForThisColor > 0)
            {
                sb.AppendLine($"{color.ToString().ToUpper()}:");
                sb.Append(colorLog.ToString());
                totalGlobalStars += starsForThisColor;
            }
        }

        sb.AppendLine($"Total stars match the correct value --> {totalGlobalStars}");
        return sb.ToString();
    }
    private int FloodFill(CellData[,] board, bool[,] visited, int r, int c, CellColor color, int ROWS, int COLS)
    {
        int size = 0;
        Queue<Vector2Int> q = new Queue<Vector2Int>();
        q.Enqueue(new Vector2Int(r, c));
        visited[r, c] = true;
        int[] dr = { -1, 1, 0, 0 }, dc = { 0, 0, -1, 1 };

        while (q.Count > 0)
        {
            Vector2Int curr = q.Dequeue();
            size++;
            for (int i = 0; i < 4; i++)
            {
                int nr = curr.x + dr[i], nc = curr.y + dc[i];
                if (nr >= 0 && nr < ROWS && nc >= 0 && nc < COLS && !visited[nr, nc] && board[nr, nc] != null && board[nr, nc].Color == color)
                {
                    visited[nr, nc] = true;
                    q.Enqueue(new Vector2Int(nr, nc));
                }
            }
        }
        return size;
    }
}