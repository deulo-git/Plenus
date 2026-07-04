public enum CellColor { Blue, Red, Green, Yellow, Orange, Black}

public class CellData
{
    public CellColor Color { get; set; }
    public bool IsMarked { get; set; }
    public bool HasStar { get; set; }
    public int Row { get; set; }
    public int Col { get; set; }

    public CellData(CellColor color, int row, int col)
    {
        Color = color;
        Row = row;
        Col = col;
        IsMarked = false;
        HasStar = false;
    }
}