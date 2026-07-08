using System.Text;
using TMPro;
using UnityEngine;

public class SelectionDebugUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI debugConsole;

    public void Refresh(
        int? activeNumber,
        CellColor? activeColor,
        int selectedCells,
        string systemMessage)
    {
        if (debugConsole == null) return;

        StringBuilder sb = new();

        sb.AppendLine("--- <b>SELECTION DEBUG PANEL</b> ---");

        sb.AppendLine($"<b>Active Dice:</b> {activeNumber} | {activeColor}");

        sb.AppendLine();
        sb.AppendLine("--- <b>CURRENT STATUS</b> ---");

        string color =
            activeNumber.HasValue && selectedCells == activeNumber.Value
            ? "green"
            : "white";

        sb.AppendLine($"<b>Cells Picked:</b> <color={color}>{selectedCells} / {activeNumber}</color>");

        sb.AppendLine();
        sb.AppendLine("--- <b>SYSTEM MESSAGE</b> ---");
        sb.AppendLine(systemMessage);

        debugConsole.text = sb.ToString();
    }

    public void Clear()
    {
        if (debugConsole != null)
            debugConsole.text = "";
    }
}