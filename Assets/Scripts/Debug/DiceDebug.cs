using System.Text;
using TMPro;
using UnityEngine;

public class DiceDebugUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI consoleText;

    public void Refresh(int[] rolledNumbers, CellColor[] rolledColors)
    {
        if (consoleText == null) return;

        StringBuilder sb = new();

        sb.AppendLine("DICE OUTPUT");
        sb.AppendLine();

        for (int i = 0; i < rolledNumbers.Length; i++)
        {
            char letter = (char)('A' + i);

            string value = rolledNumbers[i] == 6
                ? "Wildcard (?)"
                : rolledNumbers[i].ToString();

            sb.AppendLine($"Numeric Dice {letter} --> {value}");
        }

        sb.AppendLine();
        sb.AppendLine("----------------------");
        sb.AppendLine();

        for (int i = 0; i < rolledColors.Length; i++)
        {
            char letter = (char)('A' + i);
            sb.AppendLine($"Colour Dice {letter} --> {rolledColors[i]}");
        }

        consoleText.text = sb.ToString();
    }

    public void Clear()
    {
        if (consoleText != null)
            consoleText.text = "";
    }
}