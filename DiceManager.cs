using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class DiceManager : MonoBehaviour
{
    public static DiceManager Instance;

    private void Awake()
    {
        Instance = this;
    }

    public bool AreDiceRolled { get; private set; } = false;


    [Header("Debug")]
    [SerializeField] private DiceDebugUI debugUI;

    [Header("3D References")]
    [SerializeField] private DiceRoller[] numericDice;
    [SerializeField] private DiceRoller[] colorDice;

    [Header("Dependencies")]
    [SerializeField] private ColorPalette palette;

    [SerializeField] private DiceButtonUI buttonUI;
    private enum ColorFace { Blue, Red, Green, Yellow, Orange, Wildcard }
    private enum NumericFace { One = 1, Two = 2, Three = 3, Four = 4, Five = 5, Wildcard = 6 }

    private int[] rolledNumbers = new int[3];
    private CellColor[] rolledColors = new CellColor[3];



    public void ResetDiceState()
    {
        AreDiceRolled = false;

    }

    // 1. THe button click now delegates the decision to the GameManager
    public void OnRollDiceButtonClicked()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ProcessRollAction();
        }
    }

    // 2. Simply rolls 3 numbers, animates them, and returns the total score
    public int RollNumericDiceOnly()
    {
        int total = 0;
        for (int i = 0; i < numericDice.Length; i++)
        {
            int roll = Random.Range(1, 7);
            rolledNumbers[i] = roll;
            total += roll;

            if (numericDice[i] != null)
                numericDice[i].Roll(roll - 1);
        }
        AreDiceRolled = true;
        return total;
    }

    // 3. Rolls all 6 dice and updates the console
    public void RollAllDice()
    {
        SetDiceInteractable(true);
        // 1. Safety check for Numeric Dice
        if (numericDice != null)
        {
            for (int i = 0; i < numericDice.Length; i++)
            {
                if (numericDice[i] != null)
                {
                    // Trigger the roll animation/logic
                    // numericDice[i].Roll(); 
                }
                else
                {
                    Debug.LogWarning($"[DiceManager] Numeric Dice at index {i} is not assigned in the Inspector.");
                }
            }
        }

        // 2. Safety check for Color Dice (prevents crashing while Blender models are WIP)
        if (colorDice != null)
        {
            for (int i = 0; i < colorDice.Length; i++)
            {
                if (colorDice[i] != null)
                {
                    // Trigger the roll animation/logic
                    // colorDice[i].Roll();
                }
                else
                {
                    // Game won't crash anymore, it will just notify you in yellow
                    Debug.LogWarning($"[DiceManager] Color Dice at index {i} is missing. Waiting for 3D models.");
                }
            }
        }


        // Roll numeric dice
        for (int i = 0; i < numericDice.Length; i++)
        {
            int numResult = Random.Range(1, 7);
            rolledNumbers[i] = numResult;

            if (numericDice[i] != null)
                numericDice[i].Roll(numResult - 1);           
        }


        // Roll colour dice
        int colorDiceCount = Mathf.Max(colorDice.Length, colorDice != null ? colorDice.Length : 0);
        for (int i = 0; i < colorDiceCount; i++)
        {
            ColorFace colorResult = (ColorFace)Random.Range(0, 6);
            rolledColors[i] = MapFaceToCellColor(colorResult);

            if (colorDice != null && i < colorDice.Length && colorDice[i] != null)
            {
                colorDice[i].Roll((int)colorResult);
            }

            char diceLetter = (char)('A' + i);
        }
        debugUI.Refresh(rolledNumbers, rolledColors);

        AreDiceRolled = true;
    }

    /// <summary>
    /// Enables or disables the interactability of all dice buttons.
    /// </summary>
    public void SetDiceInteractable(bool isInteractable)
    {
        foreach (Button btn in DiceButtonUI.Instance.numericButtons)
        {
            if (btn != null) btn.interactable = isInteractable;
        }

        foreach (Button btn in DiceButtonUI.Instance.colorButtons)
        {
            if (btn != null) btn.interactable = isInteractable;
        }
    }

    public void SelectNumericDie(int diceIndex, Button clickedButton)
    {
        if (SelectionManager.Instance != null && SelectionManager.Instance.HasCellsSelected)
        {
            SelectionManager.Instance.CancelSelection();
            SelectionManager.Instance.SetPendingNumber(rolledNumbers[diceIndex]);
        }

        buttonUI.SelectNumeric(clickedButton);

        if (diceIndex < 0 || diceIndex >= rolledNumbers.Length) return;

        if (rolledNumbers[diceIndex] == 0)
        {
            Debug.LogWarning("<color=yellow>Warning: Attempting to select a die before rolling.</color>");
            return;
        }

        SelectionManager.Instance.SetPendingNumber(rolledNumbers[diceIndex]);

    }

    public void SelectColorDie(int diceIndex, Button clickedButton)
    {
        if (SelectionManager.Instance != null && SelectionManager.Instance.HasCellsSelected)
        {
            SelectionManager.Instance.CancelSelection();
            SelectionManager.Instance.SetPendingColor(rolledColors[diceIndex]);            
        }

        if (diceIndex < 0 || diceIndex >= rolledColors.Length) return;

        buttonUI.SelectColor(clickedButton);

        SelectionManager.Instance.SetPendingColor(rolledColors[diceIndex]);
    }

    public void LockUsedDice()
    {
        buttonUI.LockSelected();
    }

    private CellColor MapFaceToCellColor(ColorFace face)
    {
        switch (face)
        {
            case ColorFace.Blue: return CellColor.Blue;
            case ColorFace.Red: return CellColor.Red;
            case ColorFace.Green: return CellColor.Green;
            case ColorFace.Yellow: return CellColor.Yellow;
            case ColorFace.Orange: return CellColor.Orange;
            default: return CellColor.Black;
        }
    }

    internal void ResetAllDice()
    {
        buttonUI.ResetAll();
    }

    internal void SetColorDiceActive(bool active)
    {
        buttonUI.SetColorButtonsActive(active);
    }
}