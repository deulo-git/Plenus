using Assets.Scripts;
using System;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class PlayerData
{
    public int playerID;
    public string playerName;

    // Stats
    public int wildcardsRemaining = 8;
    public int score => scoreBreakdown.Total;
    
    public int totalStarsCollected = 0;

    // Score Tracking (Points earned)

    
    [NonSerialized]public Dictionary<CellColor, CompletionOrder> completedColors = new();

    public CompletionOrder[] completedRows = new CompletionOrder[BoardGenerator.ROWS];

    public ScoreBreakdown scoreBreakdown = new ScoreBreakdown();
    public ScoreUI uiReference;

    public PlayerData(int id, string name)
    {
        playerID = id;
        playerName = name;
        ResetScoreData();
        scoreBreakdown = new ScoreBreakdown();
        totalStarsCollected = 0;
        wildcardsRemaining = 8;
    }

    public void ResetScoreData()
    {
        completedColors.Clear();

        foreach (CellColor color in Enum.GetValues(typeof(CellColor)))
        {
            if (color != CellColor.Black)
            {
                completedColors[color] = CompletionOrder.None;
            }
        }

        completedRows = new CompletionOrder[BoardGenerator.ROWS];
    }

    public int GetCompletedColoursCount()
    {
        int count = 0;

        foreach (var order in completedColors.Values)
        {
            if (order != CompletionOrder.None)
                count++;
        }

        return count;
    }
}