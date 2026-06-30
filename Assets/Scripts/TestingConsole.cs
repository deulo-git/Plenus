using UnityEngine;
using TMPro;

public class TestingConsole : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] BoardManager boardManager;
    [SerializeField] TextMeshProUGUI consoleText;

    private BoardValidator validator;

    void Start()
    {
        // Inicialitzem el validador un sol cop
        validator = new BoardValidator();
    }

    public void RunValidation()
    {
        // 1. Demanem al BoardManager que generi un tauler completament nou
        boardManager.GenerateAndLinkBoard();

        // 2. Validem el tauler que s'acaba de generar
        bool isValid = validator.ValidateBoard(boardManager.GetBoardData(), out string report);

        // 3. Escrivim a la consola (Sense emojis, només Rich Text)
        string header = isValid ? "<color=green>TAULER VÀLID</color>\n\n" : "<color=red>TAULER INVÀLID</color>\n\n";
        consoleText.text = header + report;

        Debug.Log("Validacio completada.\n" + report);
    }
}