using Assets.Scripts;
using System;
using System.Collections.Generic;
using UnityEngine;
using static PlayerData;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance;

    [Header("Global Competition State")]
    public bool[] globalCompletedRows = new bool[15];
    [NonSerialized]
    public Dictionary<CellColor, bool> globalCompletedColors = new Dictionary<CellColor, bool>();

    private void Awake()
    {
        Instance = this;
        InitializeGlobalState();
    }

    private void InitializeGlobalState()
    {
        foreach (CellColor color in System.Enum.GetValues(typeof(CellColor)))
        {
            if (color != CellColor.Black) globalCompletedColors[color] = false;
        }
    }

    public void ProcessTurnScoring(PlayerData player, BoardManager board)
    {

        UpdateGameAchievements(player, board);

        CalculateTotalScore(player, board);

    }

    private void UpdateGameAchievements(PlayerData player, BoardManager board)
    {
        CheckRowCompletion(player, board);
        CheckColorCompletion(player, board);
    }

    private void CheckRowCompletion(PlayerData player, BoardManager board)
    {
        for (int r = 0; r < BoardGenerator.ROWS; r++)
        {
            if (player.completedRows[r] != CompletionOrder.None)
                continue;


            bool completed = IsRowFullyMarked(board, r);

         


            if (!completed)
                continue;


            if (globalCompletedRows[r])
            {
                player.completedRows[r] = CompletionOrder.Second;
            }
            else
            {
                player.completedRows[r] = CompletionOrder.First;
                globalCompletedRows[r] = true;
            }
        }
    }


    private void CheckColorCompletion(PlayerData player, BoardManager board)
    {
        if (player == null || board == null)
            return;


        foreach (CellColor color in Enum.GetValues(typeof(CellColor)))
        {
            if (color == CellColor.Black)
                continue;


            if (!player.completedColors.ContainsKey(color))
                player.completedColors[color] = CompletionOrder.None;


            if (player.completedColors[color] != CompletionOrder.None)
                continue;


            if (!IsColorFullyMarked(board, color))
                continue;


            CompletionOrder order;

            if (globalCompletedColors[color])
            {
                order = CompletionOrder.Second;
            }
            else
            {
                order = CompletionOrder.First;
                globalCompletedColors[color] = true;
            }


            player.completedColors[color] = order;
        }


        // Comprovació FINAL fora del foreach
        if (player.GetCompletedColoursCount() >= 2)
        {
            GameManager.Instance.EndGame();
        }
    }

    public int CalculateTotalScore(PlayerData player, BoardManager board)
    {
        ScoreBreakdown bd = player.scoreBreakdown;

        bd.rows.Clear();
        bd.colors.Clear();

        // Rows
        for (int i = 0; i < player.completedRows.Length; i++)
        {
            CompletionOrder order = player.completedRows[i];

            if (order == CompletionOrder.None)
                continue;

            int points = order == CompletionOrder.First
                ? ScoreConfig.RowRewards[i].first
                : ScoreConfig.RowRewards[i].second;


            bd.rows.Add(new ScoreBreakdown.RowScore
            {
                rowIndex = i,
                order = order,
                points = points
            });
        }


        // Colors
        foreach (var colorData in player.completedColors)
        {
            if (colorData.Value == CompletionOrder.None)
                continue;


            int points = colorData.Value == CompletionOrder.First
                ? ScoreConfig.ColorReward.first
                : ScoreConfig.ColorReward.second;


            bd.colors.Add(new ScoreBreakdown.ColorScore
            {
                color = colorData.Key,
                order = colorData.Value,
                points = points
            });
        }

        
        // Stars
        int totalStars = BoardGenerator.STAR_COLOR *
            (System.Enum.GetValues(typeof(CellColor)).Length - 1);

        bd.unmarkedStars = totalStars - player.totalStarsCollected;


        // Wildcards
        bd.unusedWildcards = player.wildcardsRemaining;


        return bd.Total;
    }

    private bool IsRowFullyMarked(BoardManager board, int row)
    {
        for (int c = 0; c < BoardGenerator.COLS; c++)
        {
            if (!board.BoardData[row, c].IsMarked) return false;
        }
        return true;
    }

    private bool IsColorFullyMarked(BoardManager board, CellColor color)
    {
        foreach (var cell in board.BoardData)
        {
            if (cell.Color == color && !cell.IsMarked) return false;
        }
        return true;
    }
}