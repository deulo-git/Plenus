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

    // --- NEW: Variables to store the current turn's rolled values ---
    private int[] rolledNumbers = new int[3];
    private CellColor[] rolledColors = new CellColor[3];

    private int selectedNumber = -1;       // -1 means no number selected yet
    private bool hasSelectedColor = false;
    private CellColor selectedColor;
    private ColorFace selectedColorFace;
    public void RollAllDice()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("DICE OUTPUT\n");

        // 1. Roll numeric dice
        for (int i = 0; i < numericDice.Length; i++)
        {
            int numResult = Random.Range(1, 7);
            
            rolledNumbers[i] = numResult;
            
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

            rolledColors[i] = MapFaceToCellColor(colorResult);

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

    public void SelectNumericDie(int diceIndex)
    {
        if (diceIndex < 0 || diceIndex >= rolledNumbers.Length) return;

        // NOU: Validem que el dau s'hagi tirat (no pot ser 0 en un dau de 6 cares)
        if (rolledNumbers[diceIndex] == 0)
        {
            Debug.LogWarning("<color=yellow>Compte! Estàs intentant seleccionar un dau abans de tirar, o la llista de daus a l'inspector està buida.</color>");
            return;
        }

        selectedNumber = rolledNumbers[diceIndex];
        Debug.Log($"<color=cyan>Selected Number: {selectedNumber}</color>");
        CheckAndSendToSelectionManager();
    }

    public void SelectColorDie(int diceIndex)
    {
        if (diceIndex < 0 || diceIndex >= rolledColors.Length) return;

        // També podem protegir els daus de color, assegurant-nos que s'han tirat
        // (Això assumint que el 0 absolut a l'inici no és vàlid si ho controles amb un flag, 
        // però en aquest cas només cal comprovar que hagis tirat)

        selectedColor = rolledColors[diceIndex];
        hasSelectedColor = true;
        Debug.Log($"<color=cyan>Selected Color: {selectedColor}</color> {diceIndex}");
        Debug.Log($"{rolledColors[0]},{rolledColors[1]},{rolledColors[2]}");
        CheckAndSendToSelectionManager();
    }


    private void CheckAndSendToSelectionManager()
    {
        if (selectedNumber != -1 && hasSelectedColor)
        {
            // Detectem els comodins aquí, abans d'enviar-ho al Manager
            bool isNumWildcard = (selectedNumber == 6); // El 6 és comodí
            bool isColWildcard = (selectedColor == CellColor.Black); // Suposant que tens l'enum ColorFace.Wildcard
            if (SelectionManager.Instance != null)
            {
                // Enviem els flags a la nova versió de SetActiveDice
                SelectionManager.Instance.SetActiveDice(
                    selectedNumber,
                    selectedColor,
                    isNumWildcard,
                    isColWildcard
                );
            }
        }
    }


    // --- Helpers ---

    private Color GetFaceColor(ColorFace face) 
    {
        switch (face)
        {
            case ColorFace.Blue: return palette.blue;
            case ColorFace.Red: return palette.red;
            case ColorFace.Green: return palette.green;
            case ColorFace.Yellow: return palette.yellow;
            case ColorFace.Orange: return palette.orange;
            default: return Color.white; // Wildcard
        }
    }

    // Maps the Dice enum to the Board Logic enum
    // També has de corregir el MapFaceToCellColor perquè no retorni Blue si és comodí
    private CellColor MapFaceToCellColor(ColorFace face)
    {
        switch (face)
        {
            case ColorFace.Blue: return CellColor.Blue;
            case ColorFace.Red: return CellColor.Red;
            case ColorFace.Green: return CellColor.Green;
            case ColorFace.Yellow: return CellColor.Yellow;
            case ColorFace.Orange: return CellColor.Orange;
            default: return CellColor.Black; // Si és Wildcard, retornarà Blue per defecte, però el flag isColWildcard a SelectionManager ignorarà aquest color.
        }
    }
}