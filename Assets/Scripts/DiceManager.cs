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

    // Which dice the local player last selected (used so the network layer can lock the
    // exact same dice on every machine when a selection is confirmed).
    public int SelectedNumericIndex { get; private set; } = -1;
    public int SelectedColorIndex { get; private set; } = -1;

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
        SelectedNumericIndex = -1;
        SelectedColorIndex = -1;
    }

    // The Roll button now asks the HOST to roll (authoritative). The host generates the
    // values and broadcasts them so both machines display the identical roll.
    public void OnRollDiceButtonClicked()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnRollButtonPressed();
        }
    }

    // Called on EVERY machine from the network layer with the host's roll.
    // colorActive is false during the initiative phase (numeric dice only).
    public void ApplyRoll(int[] numericValues, int[] colorFaces, bool colorActive)
    {
        if (numericValues != null)
        {
            for (int i = 0; i < numericDice.Length && i < numericValues.Length; i++)
            {
                rolledNumbers[i] = numericValues[i];
                if (numericDice[i] != null)
                    numericDice[i].Roll(numericValues[i] - 1);
            }
        }

        if (colorActive && colorFaces != null)
        {
            for (int i = 0; i < colorDice.Length && i < colorFaces.Length; i++)
            {
                rolledColors[i] = MapFaceToCellColor((ColorFace)colorFaces[i]);
                if (colorDice[i] != null)
                    colorDice[i].Roll(colorFaces[i]);
            }
        }

        AreDiceRolled = true;

        if (debugUI != null)
            debugUI.Refresh(rolledNumbers, rolledColors);
    }

    /// <summary>
    /// Enables or disables the interactability of all dice buttons.
    /// </summary>
    public void SetDiceInteractable(bool isInteractable)
    {
        if (DiceButtonUI.Instance == null) return;

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
        // Only the player whose turn it is may pick dice.
        if (GameManager.Instance != null && !GameManager.Instance.IsMyTurn) return;

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

        SelectedNumericIndex = diceIndex;
        SelectionManager.Instance.SetPendingNumber(rolledNumbers[diceIndex]);
    }

    public void SelectColorDie(int diceIndex, Button clickedButton)
    {
        // Only the player whose turn it is may pick dice.
        if (GameManager.Instance != null && !GameManager.Instance.IsMyTurn) return;

        if (SelectionManager.Instance != null && SelectionManager.Instance.HasCellsSelected)
        {
            SelectionManager.Instance.CancelSelection();
            SelectionManager.Instance.SetPendingColor(rolledColors[diceIndex]);
        }

        if (diceIndex < 0 || diceIndex >= rolledColors.Length) return;

        buttonUI.SelectColor(clickedButton);

        SelectedColorIndex = diceIndex;
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
