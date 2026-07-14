using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Assets.Scripts
{
    internal class GridUtils
    {
        // Ensure this helper is present for the adjacency logic
        public bool IsAdjacent(Vector2Int n, Vector2Int last)
        {
            // For even more organic growth, you can relax this to
            // Math.Abs(n.x - last.x) <= 1 && Math.Abs(n.y - last.y) <= 1
            int dx = Math.Abs(n.x - last.x);
            int dy = Math.Abs(n.y - last.y);
            return (dx + dy == 1);
        }
        public int FloodFillEmpty(int[,] board, Vector2Int start, bool[,] visited)
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
                    if (nx >= 0 && nx < BoardGenerator.ROWS && ny >= 0 && ny < BoardGenerator.COLS && board[nx, ny] == 0 && !visited[nx, ny])
                    {
                        visited[nx, ny] = true;
                        q.Enqueue(new Vector2Int(nx, ny));
                    }
                }
            }
            return count;
        }

        public Vector2Int FindFirstEmpty(int[,] board)
        {
            for (int r = 0; r < BoardGenerator.ROWS; r++)
                for (int c = 0; c < BoardGenerator.COLS; c++)
                    if (board[r, c] == 0) return new Vector2Int(r, c);
            return new Vector2Int(-1, -1);
        }

        public int GetConnectedEmptySpaceSize(int[,] board, Vector2Int start)
        {
            bool[,] visited = new bool[BoardGenerator.ROWS, BoardGenerator.COLS];
            return FloodFillEmpty(board, start, visited);
        }

        public bool IsBoardViable(int[,] board, int minSize)
        {
            bool[,] visited = new bool[BoardGenerator.ROWS, BoardGenerator.COLS];
            for (int r = 0; r < BoardGenerator.ROWS; r++)
            {
                for (int c = 0; c < BoardGenerator.COLS; c++)
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

        // NOTE (online): the shuffle now takes a System.Random so board generation is
        // fully deterministic for a given seed. Passing the SAME seed on the host and on
        // every client guarantees they all build the IDENTICAL board.
        public void Shuffle<T>(IList<T> list, System.Random rng)
        {
            for (int i = 0; i < list.Count; i++)
            {
                T temp = list[i];
                int r = rng.Next(i, list.Count);
                list[i] = list[r];
                list[r] = temp;
            }
        }
    }
}
