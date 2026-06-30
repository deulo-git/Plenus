using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

// ELIMINAT el ": MonoBehaviour". Aquesta classe és lògica pura.
public class BoardValidator
{
    public bool ValidateBoard(CellData[,] board, out string report)
    {
        int rows = board.GetLength(0);
        int cols = board.GetLength(1);
        int colorAmount = Enum.GetValues(typeof(CellColor)).Length;
        int expectedCells = (rows * cols) / colorAmount;

        Dictionary<CellColor, List<int>> colorGroups = new Dictionary<CellColor, List<int>>();
        foreach (CellColor c in Enum.GetValues(typeof(CellColor)))
            colorGroups[c] = new List<int>();

        bool[,] visited = new bool[rows, cols];
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                if (!visited[r, c] && board[r, c] != null)
                {
                    int size = FloodFill(board, visited, r, c, board[r, c].Color, rows, cols);
                    colorGroups[board[r, c].Color].Add(size);
                }
            }
        }

        StringBuilder sb = new StringBuilder();
        bool isValid = true;

        foreach (var entry in colorGroups)
        {
            string colorName = Enum.GetName(typeof(CellColor), entry.Key);
            sb.AppendLine($"--- {colorName.ToUpper()} ---");

            int totalCells = 0;
            entry.Value.ForEach(s => totalCells += s);

            // Requisit estricte basat en la mida de la matriu
            bool distOk = (totalCells == expectedCells);
            sb.AppendLine($"Colour Distribution: {(distOk ? "<color=green>OK</color>" : "<color=red>ERROR</color>")} ({totalCells} celles)");

            for (int s = 6; s >= 2; s--)
            {
                int count = entry.Value.FindAll(x => x == s).Count;
                bool isOk = (count == 1);

                sb.AppendLine($"{s}-cells: {(isOk ? "<color=green>OK</color>" : "<color=red>ERROR</color>")} ({count} trobats)");
                if (!isOk) isValid = false;
            }

            // Exigim exactament 1 cel·la aïllada ja que (6+5+4+3+2) = 20, més 1 aillada = 21 (expectedCells).
            int onesCount = entry.Value.FindAll(x => x == 1).Count;
            bool isolatedOk = (onesCount == 1);
            sb.AppendLine($"Isolated Cells: {(isolatedOk ? "<color=green>OK</color>" : "<color=red>ERROR</color>")} ({onesCount} trobats)");
            if (!isolatedOk) isValid = false;

            int largerGroups = entry.Value.FindAll(x => x > 6).Count;
            if (largerGroups > 0)
            {
                sb.AppendLine($"Groups > 6: <color=red>ERROR</color> ({largerGroups} trobats)");
                isValid = false;
            }

            sb.AppendLine("");
        }

        report = sb.ToString();
        return isValid;
    }

    private int FloodFill(CellData[,] board, bool[,] visited, int r, int c, CellColor color, int rows, int cols)
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
                if (nr >= 0 && nr < rows && nc >= 0 && nc < cols && !visited[nr, nc] && board[nr, nc] != null && board[nr, nc].Color == color)
                {
                    visited[nr, nc] = true;
                    q.Enqueue(new Vector2Int(nr, nc));
                }
            }
        }
        return size;
    }
}