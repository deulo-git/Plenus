using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Username login for the Login scene.
///
/// Setup:
///  1. Add this component to an empty GameObject (e.g. "LoginController").
///  2. Drag the username TMP_InputField into usernameInput.
///  3. Drag the login Button into loginButton.
///  4. (Optional) Drag a TextMeshProUGUI into statusText for error messages.
///  5. Set nextSceneName to the scene to load after login (default "Menu").
///
/// Behaviour: validates the name, resolves it to a players.player_id via
/// DatabaseManager.GetOrCreatePlayer, stores it in PlayerSession, remembers
/// the name in PlayerPrefs for next launch, and loads the next scene.
/// Pressing Enter in the input field submits too.
/// </summary>
public class LoginManager : MonoBehaviour
{
    private const string LastUsernameKey = "Plenus.LastUsername";
    private const int MinNameLength = 2;
    private const int MaxNameLength = 20;

    [Header("UI References")]
    [SerializeField] private TMP_InputField usernameInput;
    [SerializeField] private Button loginButton;
    [SerializeField] private TextMeshProUGUI statusText; // optional

    [Header("Flow")]
    [SerializeField] private string nextSceneName = "Menu";

    private void Start()
    {
        loginButton.onClick.AddListener(TryLogin);
        usernameInput.onSubmit.AddListener(_ => TryLogin());

        // Prefill the last used name so returning players just press Enter.
        string lastName = PlayerPrefs.GetString(LastUsernameKey, "");
        if (!string.IsNullOrEmpty(lastName))
            usernameInput.text = lastName;

        usernameInput.Select();
        SetStatus("");
    }

    public void TryLogin()
    {
        string name = usernameInput.text.Trim();

        if (name.Length < MinNameLength)
        {
            SetStatus($"Name must be at least {MinNameLength} characters.");
            return;
        }
        if (name.Length > MaxNameLength)
        {
            SetStatus($"Name must be at most {MaxNameLength} characters.");
            return;
        }

        loginButton.interactable = false;
        SetStatus("Logging in...");

        try
        {
            long playerId = DatabaseManager.GetOrCreatePlayer(name);
            if (playerId <= 0)
            {
                SetStatus("Login failed. Please try again.");
                loginButton.interactable = true;
                return;
            }

            PlayerSession.SignIn(playerId, name);
            PlayerPrefs.SetString(LastUsernameKey, name);
            PlayerPrefs.Save();

            Debug.Log($"[LoginManager] Logged in as '{name}' (player_id={playerId})");
            SceneManager.LoadScene(nextSceneName, LoadSceneMode.Single);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[LoginManager] Login error: {e}");
            SetStatus("Could not reach the database.");
            loginButton.interactable = true;
        }
    }

    private void SetStatus(string message)
    {
        if (statusText != null) statusText.text = message;
    }
}
