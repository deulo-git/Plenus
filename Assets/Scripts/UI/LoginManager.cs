using System;
using System.Threading.Tasks;
using TMPro;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Username + password login for the Login scene, backed by Unity
/// Authentication (Unity Gaming Services) instead of a local-only check.
///
/// Why UGS Authentication and not our own password hashing: a "does this
/// password match" check can only be done safely against a secret that lives
/// somewhere the client can't read, which means it has to happen server-side.
/// Unity Authentication already provides exactly that (hashing, storage,
/// verification, rate limiting) for free, and it gives every account a
/// stable cross-device PlayerId — which is exactly what upcoming Cloud
/// Save / Leaderboards data needs to be keyed on. Rolling our own would mean
/// building and hosting that server piece ourselves for no benefit.
///
/// Setup (Inspector wiring):
///  1. Add this component to an empty GameObject (e.g. "LoginController").
///  2. Drag the existing username TMP_InputField into usernameInput.
///  3. Drag "InputField (TMP) (1)" (already present under Inputs, already
///     configured with Content Type = Password) into passwordInput.
///  4. Drag the existing "Button" (the current login button) into loginButton.
///  5. Drag a "Create account" button into createAccountButton. There isn't
///     one in the scene yet — the fastest way is to duplicate the existing
///     "Continue as guest" button (Button (1) under Buttons), change its
///     label to "Create account", and drag that in.
///  6. (Optional) Drag a TextMeshProUGUI into statusText for error messages.
///  7. Set nextSceneName to the scene to load after login (default "Menu").
///
/// Behaviour: on Login, calls AuthenticationService.SignInWithUsernamePasswordAsync;
/// on Create account, calls SignUpWithUsernamePasswordAsync. Either way, once
/// UGS confirms the identity, resolves/creates the matching LOCAL SQLite
/// players row (for the local match/move log only — see PlayerSession.cs),
/// stores everything in PlayerSession, remembers the username in PlayerPrefs
/// for next launch (never the password), and loads the next scene.
/// </summary>
public class LoginManager : MonoBehaviour
{
    private const string LastUsernameKey = "Plenus.LastUsername";

    // Mirrors Unity Authentication's own username/password requirements so we
    // can reject obviously-bad input instantly instead of waiting on a round
    // trip, without duplicating any secret-checking logic (the real
    // acceptance check always happens server-side).
    private const int MinUsernameLength = 3;
    private const int MaxUsernameLength = 20;
    private const int MinPasswordLength = 8;
    private const int MaxPasswordLength = 30;

    [Header("UI References")]
    [SerializeField] private TMP_InputField usernameInput;
    [SerializeField] private TMP_InputField passwordInput;
    [SerializeField] private Button loginButton;
    [SerializeField] private Button createAccountButton;
    [SerializeField] private TextMeshProUGUI statusText; // optional

    [Header("Flow")]
    [SerializeField] private string nextSceneName = "Menu";

    // Guards UGS init so it happens exactly once, and lets us disable the
    // buttons until it's actually safe to call Authentication.
    private Task _servicesReady;

    // Captured from the TextMeshProUGUI's own inspector color at startup, so
    // non-error status text ("Connecting...", "Logging in...") keeps whatever
    // color was designed for it instead of us hardcoding one.
    private Color _defaultStatusColor = Color.white;
    private static readonly Color ErrorStatusColor = Color.red;

    private void Start()
    {
        if (statusText != null)
            _defaultStatusColor = statusText.color;

        loginButton.onClick.AddListener(() => _ = TrySignInAsync());
        if (createAccountButton != null)
            createAccountButton.onClick.AddListener(() => _ = TryCreateAccountAsync());
        usernameInput.onSubmit.AddListener(submittedText => _ = TrySignInAsync());
        passwordInput.onSubmit.AddListener(submittedText => _ = TrySignInAsync());

        // Prefill the last used name so returning players just need to type
        // their password. Never prefill/remember the password itself.
        string lastName = PlayerPrefs.GetString(LastUsernameKey, "");
        if (!string.IsNullOrEmpty(lastName))
            usernameInput.text = lastName;

        usernameInput.Select();
        SetStatus("");

        SetButtonsInteractable(false);
        SetStatus("Connecting...");
        _servicesReady = InitializeServicesAsync();
        _ = AwaitServicesThenEnableButtons();
    }

    // ================= UGS INITIALIZATION =================

    private async Task InitializeServicesAsync()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
            await UnityServices.InitializeAsync();
    }

    private async Task AwaitServicesThenEnableButtons()
    {
        try
        {
            await _servicesReady;
            SetButtonsInteractable(true);
            SetStatus("");
        }
        catch (Exception e)
        {
            Debug.LogError($"[LoginManager] UGS init failed: {e}");
            SetStatus("Could not connect. Check your internet connection and restart.", isError: true);
        }
    }

    // UGS keeps its authenticated session alive across scene loads (the service
    // is a singleton that survives scene changes). If the player reached this
    // scene by logging out — or a previous session simply lingered — the SDK
    // still considers them signed in, and SignIn/SignUp would throw
    // ClientInvalidUserState ("already signed in", error 10000). Clearing any
    // existing session first guarantees the Login scene always starts from a
    // clean slate, so log out -> log back in works every time (even as a
    // different user).
    private void EnsureUgsSignedOut()
    {
        if (AuthenticationService.Instance.IsSignedIn)
            AuthenticationService.Instance.SignOut();
    }

    // ================= LOGIN (existing account) =================

    public async Task TrySignInAsync()
    {
        if (!TryReadAndValidateCredentials(out string username, out string password, isSigningUp: false))
            return;

        SetButtonsInteractable(false);
        SetStatus("Logging in...");

        try
        {
            await _servicesReady;
            EnsureUgsSignedOut();
            await AuthenticationService.Instance.SignInWithUsernamePasswordAsync(username, password);
            await OnSignedIntoUgsAsync(username);
        }
        catch (AuthenticationException e)
        {
            // Expected client-side rejection (bad params / wrong state). This is
            // a normal outcome, not a bug, so log it plainly (no red/yellow
            // console entry) and just show it on screen.
            Debug.Log($"[LoginManager] Sign-in rejected ({e.ErrorCode}): {e.Message}");
            SetStatus(FriendlyAuthMessage(e, isSigningUp: false), isError: true);
            SetButtonsInteractable(true);
        }
        catch (RequestFailedException e)
        {
            // A wrong username/password is rejected by the SERVER and surfaces
            // here (not as an AuthenticationException), alongside genuine network
            // failures. FriendlyRequestMessage tells the two apart. Both are
            // expected, so keep them out of the error console.
            Debug.Log($"[LoginManager] Sign-in failed ({e.ErrorCode}): {e.Message}");
            SetStatus(FriendlyRequestMessage(e, isSigningUp: false), isError: true);
            SetButtonsInteractable(true);
        }
        catch (Exception e)
        {
            Debug.LogError($"[LoginManager] Unexpected sign-in error: {e}");
            SetStatus("Something went wrong. Please try again.", isError: true);
            SetButtonsInteractable(true);
        }
    }

    // ================= CREATE ACCOUNT (new account) =================

    public async Task TryCreateAccountAsync()
    {
        if (!TryReadAndValidateCredentials(out string username, out string password, isSigningUp: true))
            return;

        SetButtonsInteractable(false);
        SetStatus("Creating account...");

        try
        {
            await _servicesReady;
            EnsureUgsSignedOut();
            await AuthenticationService.Instance.SignUpWithUsernamePasswordAsync(username, password);
            await OnSignedIntoUgsAsync(username);
        }
        catch (AuthenticationException e)
        {
            Debug.Log($"[LoginManager] Sign-up rejected ({e.ErrorCode}): {e.Message}");
            SetStatus(FriendlyAuthMessage(e, isSigningUp: true), isError: true);
            SetButtonsInteractable(true);
        }
        catch (RequestFailedException e)
        {
            // Sign-up rejections (username already taken, weak password, …) come
            // back as a server RequestFailedException. Expected, so no console
            // error — just surface the reason on screen.
            Debug.Log($"[LoginManager] Sign-up failed ({e.ErrorCode}): {e.Message}");
            SetStatus(FriendlyRequestMessage(e, isSigningUp: true), isError: true);
            SetButtonsInteractable(true);
        }
        catch (Exception e)
        {
            Debug.LogError($"[LoginManager] Unexpected sign-up error: {e}");
            SetStatus("Something went wrong. Please try again.", isError: true);
            SetButtonsInteractable(true);
        }
    }

    // ================= SHARED POST-AUTH FLOW =================

    // Runs after UGS has confirmed the identity (sign-in or sign-up alike):
    // resolve/create the local SQLite player row used for the local match/move
    // log, store everything in PlayerSession, remember the username, go on.
    private async Task OnSignedIntoUgsAsync(string username)
    {
        try
        {
            long localPlayerId = DatabaseManager.GetOrCreatePlayer(username);
            if (localPlayerId <= 0)
            {
                SetStatus("Signed in, but the local database is unavailable. Please try again.", isError: true);
                SetButtonsInteractable(true);
                return;
            }

            string ugsPlayerId = AuthenticationService.Instance.PlayerId;
            PlayerSession.SignIn(localPlayerId, username, ugsPlayerId, "password");

            PlayerPrefs.SetString(LastUsernameKey, username);
            PlayerPrefs.Save();

            Debug.Log($"[LoginManager] Signed in as '{username}' (ugsPlayerId={ugsPlayerId}, localPlayerId={localPlayerId})");
            await Task.Yield(); // let the log flush before the scene tears everything down
            SceneManager.LoadScene(nextSceneName, LoadSceneMode.Single);
        }
        catch (Exception e)
        {
            Debug.LogError($"[LoginManager] Local database error after sign-in: {e}");
            SetStatus("Could not reach the local database.", isError: true);
            SetButtonsInteractable(true);
        }
    }

    // ================= VALIDATION =================

    private bool TryReadAndValidateCredentials(out string username, out string password, bool isSigningUp)
    {
        username = usernameInput.text.Trim();
        password = passwordInput.text;

        if (isSigningUp)
        {
            // Creating an account: the player is choosing a NEW password, so they
            // need the actual rules to pick a valid one — these mirror Unity
            // Authentication's own server-side requirements.
            if (username.Length < MinUsernameLength || username.Length > MaxUsernameLength)
            {
                SetStatus($"Username must be {MinUsernameLength}-{MaxUsernameLength} characters.", isError: true);
                return false;
            }
            if (password.Length < MinPasswordLength || password.Length > MaxPasswordLength)
            {
                SetStatus($"Password must be {MinPasswordLength}-{MaxPasswordLength} characters, " +
                          "with at least 1 uppercase, 1 lowercase, 1 number and 1 symbol.", isError: true);
                return false;
            }
            return true;
        }

        // Logging into an EXISTING account: the player already knows their own
        // password, so reciting the complexity rules here is just noise (and
        // looks like a bug, since a correct password obviously already meets
        // them). Treat any obviously-empty/short field as a plain wrong-credential
        // error instead, same as what the server would say.
        if (string.IsNullOrEmpty(username))
        {
            SetStatus("Nom d'usuari erroni", isError: true);
            return false;
        }
        if (string.IsNullOrEmpty(password))
        {
            SetStatus("Contrasenya errònia", isError: true);
            return false;
        }
        return true;
    }

    // Unity Authentication's SDK doesn't expose a distinct error code for
    // "wrong password" vs "weak password" vs "username taken" beyond
    // InvalidParameters, so we lean on the server's own message (it's
    // already human-readable) and only special-case the states we ARE sure
    // about from the SDK source.
    private static string FriendlyAuthMessage(AuthenticationException e, bool isSigningUp)
    {
        if (e.ErrorCode == AuthenticationErrorCodes.InvalidParameters)
            return isSigningUp
                ? "Username must be 3-20 characters, and the password 8-30 with an uppercase, a lowercase, a number and a symbol."
                : "Please enter a valid username and password.";

        if (e.ErrorCode == AuthenticationErrorCodes.ClientInvalidUserState)
            return "Please wait a moment and try again.";

        if (!string.IsNullOrEmpty(e.Message))
            return e.Message;

        return isSigningUp
            ? "Could not create the account. Check your username/password requirements."
            : "Could not log in. Check your username and password.";
    }

    // Maps a server-side RequestFailedException to a friendly message. A wrong
    // username/password is rejected by the SERVER and surfaces here (NOT as an
    // AuthenticationException), alongside genuine connectivity failures — the
    // transport error code is what separates "couldn't reach the server" from
    // "the server said no".
    private static string FriendlyRequestMessage(RequestFailedException e, bool isSigningUp)
    {
        if (e.ErrorCode == CommonErrorCodes.TransportError)
            return "Could not reach the login server. Check your connection.";

        if (isSigningUp)
            // Sign-up rejections are actionable (username already taken, password
            // too weak, …) and the server's own message is already readable.
            return string.IsNullOrEmpty(e.Message)
                ? "Could not create the account. Check the requirements and try again."
                : e.Message;

        // Sign-in rejection: deliberately vague so we never reveal whether a
        // given username actually exists.
        return "Nom d'usuari o contrasenya incorrectes.";
    }

    private void SetButtonsInteractable(bool interactable)
    {
        loginButton.interactable = interactable;
        if (createAccountButton != null) createAccountButton.interactable = interactable;
    }

    private void SetStatus(string message, bool isError = false)
    {
        if (statusText == null) return;
        statusText.text = message;
        statusText.color = isError ? ErrorStatusColor : _defaultStatusColor;
    }
}
