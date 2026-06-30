public enum CellColor { Blue, Red, Green, Yellow, Orange }

public class CellData
{
    public CellColor Color { get; set; }
    public bool IsMarked { get; set; }
    public bool HasStar { get; set; }

    public CellData(CellColor color)
    {
        Color = color;
        IsMarked = false;
        HasStar = false;
    }
}