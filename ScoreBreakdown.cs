using System.Collections.Generic;

[System.Serializable]
public class ScoreBreakdown
{

    [System.Serializable]
    public class RowScore
    {
        public int rowIndex;
        public CompletionOrder order;
        public int points;
    }


    [System.Serializable]
    public class ColorScore
    {
        public CellColor color;
        public CompletionOrder order;
        public int points;
    }

    public List<RowScore> rows = new();

    public List<ColorScore> colors = new();

    public int unmarkedStars;
    public int unusedWildcards;

    public int Total
    {
        get
        {
            int total = 0;

            foreach (var row in rows)
                total += row.points;

            foreach (var color in colors)
                total += color.points;

            total += unmarkedStars * ScoreConfig.PenaltyPerUnmarkedStar;
            total += unusedWildcards * ScoreConfig.RewardPerUnusedWildcard;

            return total;
        }
    }
}