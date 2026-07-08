using UnityEngine;

[System.Serializable]
public struct Reward
{
    public int first;
    public int second;

    public Reward(int first, int second)
    {
        this.first = first;
        this.second = second;
    }
}
public enum CompletionOrder
{
    None,
    First,
    Second
}

public static class ScoreConfig
{
    // Row rewards (index 0 = top, index 14 = bottom). Format: (First Reward, Second Reward)
    // Values adapted to Plenus classic rules (Adjust second rewards as needed)
    public static readonly Reward[] RowRewards = new Reward[]
    {
        new Reward(5, 3), new Reward(3, 2), new Reward(3, 2),
        new Reward(3, 2), new Reward(2, 1), new Reward(2, 1),
        new Reward(2, 1), new Reward(1, 0), new Reward(2, 1),
        new Reward(2, 1), new Reward(2, 1), new Reward(3, 2),
        new Reward(3, 2), new Reward(3, 2), new Reward(5, 3)
    };

    public static readonly Reward ColorReward = new Reward(5, 3);

    public const int PenaltyPerUnmarkedStar = -2;
    public const int RewardPerUnusedWildcard = 1; 
}