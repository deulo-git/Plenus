using UnityEngine;
using UnityEngine.UI;

public class DiceButtonUI : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] public Button[] numericButtons;
    [SerializeField] public Button[] colorButtons;

    [Header("Colors")]
    [SerializeField] private Color defaultColor = Color.white;
    [SerializeField] private Color selectedColor = new(1f, 0.6f, 0f);
    [SerializeField] private Color lockedColor = Color.red;

    private Button selectedNumericButton;
    private Button selectedColorButton;

    public static DiceButtonUI Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    public void SelectNumeric(Button button)
    {
        if (selectedNumericButton != null)
            selectedNumericButton.image.color = defaultColor;

        selectedNumericButton = button;
        selectedNumericButton.image.color = selectedColor;
    }

    public void SelectColor(Button button)
    {
        if (selectedColorButton != null)
            selectedColorButton.image.color = defaultColor;

        selectedColorButton = button;
        selectedColorButton.image.color = selectedColor;
    }

    public void LockSelected()
    {
        if (selectedNumericButton != null)
        {
            selectedNumericButton.image.color = lockedColor;
            selectedNumericButton.interactable = false;
            selectedNumericButton = null;
        }

        if (selectedColorButton != null)
        {
            selectedColorButton.image.color = lockedColor;
            selectedColorButton.interactable = false;
            selectedColorButton = null;
        }
    }

    // Lock specific dice by index. Used by the network layer so that when one player
    // "uses up" a numeric die and a color die, those exact dice are locked on EVERY
    // machine (leaving the leftovers for the passive player).
    public void LockByIndex(int numIndex, int colorIndex)
    {
        if (numericButtons != null && numIndex >= 0 && numIndex < numericButtons.Length && numericButtons[numIndex] != null)
        {
            numericButtons[numIndex].image.color = lockedColor;
            numericButtons[numIndex].interactable = false;
        }

        if (colorButtons != null && colorIndex >= 0 && colorIndex < colorButtons.Length && colorButtons[colorIndex] != null)
        {
            colorButtons[colorIndex].image.color = lockedColor;
            colorButtons[colorIndex].interactable = false;
        }
    }

    public void ResetAll()
    {
        foreach (Button button in numericButtons)
        {
            button.image.color = defaultColor;
            button.interactable = true;
        }

        foreach (Button button in colorButtons)
        {
            button.image.color = defaultColor;
            button.interactable = true;
        }

        selectedNumericButton = null;
        selectedColorButton = null;
    }

    public void SetColorButtonsActive(bool active)
    {
        foreach (Button button in colorButtons)
        {
            button.image.color = active ? defaultColor : lockedColor;
            button.interactable = active;
        }
    }
}
