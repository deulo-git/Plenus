using UnityEngine;

[CreateAssetMenu(fileName = "NewColorPalette", menuName = "Plenus/Color Palette")]
public class ColorPalette : ScriptableObject
{
    public Color red = Color.red;
    public Color blue = Color.blue;
    public Color green = Color.green;
    public Color yellow = Color.yellow;
    public Color orange = Color.magenta;
    public Color black = Color.black;

    public Color GetColor(CellColor cellColor)
    {
        switch (cellColor)
        {
            case CellColor.Red: return red;
            case CellColor.Blue: return blue;
            case CellColor.Green: return green;
            case CellColor.Yellow: return yellow;
            case CellColor.Orange: return orange;            
            default: return Color.white;
        }
    }
}