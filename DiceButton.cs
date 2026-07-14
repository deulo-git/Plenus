using UnityEngine;
using UnityEngine.UI;

public class DiceButton : MonoBehaviour
{
    public bool isNumeric; // Marcat True si és un dau numèric, False si és de color
    public int diceIndex;  // Índex corresponent a l'array del DiceManager
    

    private void Start()
    {
        // Afegim automàticament el listener al clic
        GetComponent<Button>().onClick.AddListener(OnButtonClick);
    }

    private void OnButtonClick()
    {
        if (isNumeric)
            DiceManager.Instance.SelectNumericDie(diceIndex, GetComponent<Button>());
        else
            DiceManager.Instance.SelectColorDie(diceIndex, GetComponent<Button>());
    }
}