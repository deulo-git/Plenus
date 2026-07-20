using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Un sol botó que fa de "Roll" o de "Confirm" segons l'estat de la partida.
public class RollConfirmButton : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private TextMeshProUGUI label;

    [Header("Textos")]
    [SerializeField] private string rollText = "Roll";
    [SerializeField] private string confirmText = "Confirm";

    [Header("Colors")]
    [SerializeField] private Color rollColor = new Color(0.30f, 0.55f, 1f);  // blau
    [SerializeField] private Color confirmColor = new Color(1f, 0.60f, 0f);  // taronja

    private enum Mode { None, Roll, Confirm }
    private Mode currentMode = Mode.None;

    private void Awake()
    {
        if (button == null) button = GetComponent<Button>();
        button.onClick.AddListener(OnClicked);
    }

    private void Start() => ApplyMode(ComputeMode());   // estat inicial correcte

    private void Update()
    {
        Mode desired = ComputeMode();
        if (desired != currentMode) ApplyMode(desired);

        // Interactability is refreshed every frame (not only on mode changes),
        // because in Confirm mode it also depends on the current dice selection.
        UpdateInteractable();
    }

    // Roll: always clickable when it's your turn. Confirm: clickable ONLY once you
    // have selected BOTH dice (one number + one colour). None: never clickable.
    private void UpdateInteractable()
    {
        if (button == null) return;

        bool on;
        switch (currentMode)
        {
            case Mode.Roll: on = true; break;
            case Mode.Confirm: on = BothDiceSelected(); break;
            default: on = false; break;
        }

        button.interactable = on;
    }

    private static bool BothDiceSelected()
    {
        var sm = SelectionManager.Instance;
        return sm != null && sm.ActiveNumber.HasValue && sm.ActiveColor.HasValue;
    }

    private Mode ComputeMode()
    {
        var gm = GameManager.Instance;
        if (gm == null || !gm.IsMyTurn) return Mode.None;

        bool rolled = DiceManager.Instance != null && DiceManager.Instance.AreDiceRolled;
        var s = gm.CurrentState;

        if (s == GameManager.GameState.ActivePlayerTurn && !rolled) return Mode.Roll;
        if (s == GameManager.GameState.Initiative) return Mode.Roll;   // per si el fas servir a Main
        if (s == GameManager.GameState.ActivePlayerTurn || s == GameManager.GameState.PassivePlayerTurn)
            return Mode.Confirm;

        return Mode.None;
    }

    private void ApplyMode(Mode mode)
    {
        currentMode = mode;

        // Interactability is handled by UpdateInteractable(); ApplyMode only drives
        // the visuals (label + colours).
        if (mode == Mode.None) return;

        Color c = (mode == Mode.Roll) ? rollColor : confirmColor;
        if (label != null) label.text = (mode == Mode.Roll) ? rollText : confirmText;

        if (button != null)
        {
            ColorBlock cb = button.colors;
            cb.normalColor = c;
            cb.highlightedColor = Color.Lerp(c, Color.white, 0.15f);
            cb.pressedColor = Color.Lerp(c, Color.black, 0.15f);
            cb.selectedColor = c;
            button.colors = cb;
        }
    }

    private void OnClicked()
    {
        if (currentMode == Mode.Roll)
            GameManager.Instance?.OnRollButtonPressed();
        else if (currentMode == Mode.Confirm)
            SelectionManager.Instance?.ConfirmSelection();
    }
}