using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DiceManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI consoleText;

    [Header("3D References")]
    [SerializeField] private DiceRoller[] numericDice;
    [SerializeField] private DiceRoller[] colorDice;

    [Header("Dependencies")]
    [SerializeField] private ColorPalette palette;

    private enum ColorFace { Blue, Red, Green, Yellow, Orange, Wildcard }
    private enum NumericFace { One = 1, Two = 2, Three = 3, Four = 4, Five = 5, Wildcard = 6 }

    public void RollAllDice()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("DICE OUTPUT\n");

        // 1. Roll numeric dice
        for (int i = 0; i < numericDice.Length; i++)
        {
            int numResult = Random.Range(1, 7); 

            if (numericDice[i] != null)
            {
                // Convert the result (1-6) into a face index (0-5)
                numericDice[i].Roll(numResult - 1);
            }

            string faceLabel = numResult == 6 ? "Wildcard (?)" : numResult.ToString();
            char diceLetter = (char)('A' + i);
            sb.AppendLine($"Numeric Dice {diceLetter} --> {faceLabel}");
        }

        sb.AppendLine("\n----------------------\n");

        // 2. Roll colour dice
        // Use the larger count between assigned 3D dice and 2D placeholders
        int colorDiceCount = Mathf.Max(colorDice.Length, colorDice != null ? colorDice.Length : 0);

        for (int i = 0; i < colorDiceCount; i++)
        {
            ColorFace colorResult = (ColorFace)Random.Range(0, 6); // Resultat Enum: 0 al 5

            // Trigger 3D roll animation
            if (colorDice != null && i < colorDice.Length && colorDice[i] != null)
            {
                // Enum values already match the face indices (0-5)
                colorDice[i].Roll((int)colorResult);
            }

            char diceLetter = (char)('A' + i);
            sb.AppendLine($"Colour Dice {diceLetter} --> {colorResult.ToString()}");
        }

        // 3. Print Console
        if (consoleText != null)
        {
            consoleText.text = sb.ToString();
        }
    }    
}