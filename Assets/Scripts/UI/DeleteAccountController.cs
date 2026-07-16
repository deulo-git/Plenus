using System;
using System.Threading.Tasks;
using TMPro;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Permanently deletes the signed-in player's account: the Unity
/// Authentication identity (so that username/password can never sign in
/// again), and — only if they never actually played a match — their local
/// SQLite profile row too. Match history from finished games is
/// intentionally kept even after deletion (see
/// DatabaseManager.DeletePlayerIfNoHistory): erasing it would either be
/// rejected by the players/match_players foreign key, or, if cascaded,
/// would silently blank out the OTHER player's record of a shared match.
/// The login itself is gone either way, which is what "delete account"
/// actually means.
///
/// Setup: this REPLACES BackButtonController on RemoveAcc_BTN — that
/// component was only there for scene navigation and doesn't delete
/// anything (it just logs out, same as LogOut_BTN).
///  1. Remove the BackButtonController component from RemoveAcc_BTN.
///  2. Add this component instead (Button is already present, so no new
///     RequireComponent dependency to satisfy).
///  3. Leave targetSceneName as "Login" (default) unless you want it to
///     land somewhere else after deletion.
///  4. (Optional) Drag a TextMeshProUGUI into statusText for error
///     messages, if this scene has a spot for one.
///
/// Safety: this can't be undone, so it requires two taps. The first arms it
/// (the button's own label flips to a confirmation prompt and auto-disarms
/// after a few seconds); the second, while armed, actually deletes.
/// </summary>
[RequireComponent(typeof(Button))]
public class DeleteAccountController : MonoBehaviour
{
    [SerializeField] private string targetSceneName = "Login";
    [SerializeField] private TextMeshProUGUI statusText; // optional
    [SerializeField] private float confirmWindowSeconds = 4f;
    [SerializeField] private string confirmLabel = "Estàs segur? Torna a prémer";

    private Button _button;
    private TextMeshProUGUI _buttonLabel;
    private string _originalLabel;
    private bool _armed;
    private float _armedUntil;
    private Color _defaultStatusColor = Color.white;

    private void Awake()
    {
        _button = GetComponent<Button>();
        _buttonLabel = _button.GetComponentInChildren<TextMeshProUGUI>();
        if (_buttonLabel != null) _originalLabel = _buttonLabel.text;
        if (statusText != null) _defaultStatusColor = statusText.color;

        _button.onClick.AddListener(OnClicked);
    }

    private void Update()
    {
        if (_armed && Time.unscaledTime >= _armedUntil)
            Disarm();
    }

    private void OnClicked()
    {
        if (!_armed)
        {
            Arm();
            return;
        }

        Disarm();
        _ = DeleteAccountFlowAsync();
    }

    private void Arm()
    {
        _armed = true;
        _armedUntil = Time.unscaledTime + confirmWindowSeconds;
        if (_buttonLabel != null) _buttonLabel.text = confirmLabel;
    }

    private void Disarm()
    {
        _armed = false;
        if (_buttonLabel != null && _originalLabel != null) _buttonLabel.text = _originalLabel;
    }

    private async Task DeleteAccountFlowAsync()
    {
        _button.interactable = false;
        SetStatus("Eliminant compte...");

        try
        {
            // This is the actual account deletion: it invalidates the
            // username/password credential server-side, permanently. Do this
            // FIRST and let it throw if anything's wrong (not signed in,
            // connectivity) — we don't want to touch local data unless the
            // real account is actually gone.
            await AuthenticationService.Instance.DeleteAccountAsync();

            long localPlayerId = PlayerSession.PlayerId;
            if (localPlayerId > 0)
            {
                bool removed = DatabaseManager.DeletePlayerIfNoHistory(localPlayerId);
                Debug.Log(removed
                    ? $"[DeleteAccountController] Removed local profile row for player {localPlayerId}."
                    : $"[DeleteAccountController] Kept local profile row for player {localPlayerId} (has match history); login is gone regardless.");
            }

            // Same PlayerPrefs key LoginManager uses to prefill the username
            // field — deleted so the Login screen doesn't prefill a name that
            // can no longer sign in.
            PlayerPrefs.DeleteKey("Plenus.LastUsername");
            PlayerPrefs.Save();

            PlayerSession.SignOut();

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                NetworkManager.Singleton.Shutdown();

            Debug.Log("[DeleteAccountController] Account deleted.");
            await Task.Yield(); // let the log flush before the scene tears everything down
            SceneManager.LoadScene(targetSceneName, LoadSceneMode.Single);
        }
        catch (AuthenticationException e)
        {
            Debug.Log($"[DeleteAccountController] Deletion rejected ({e.ErrorCode}): {e.Message}");
            SetStatus("No s'ha pogut eliminar el compte. Torna a iniciar sessió i prova-ho de nou.", isError: true);
            _button.interactable = true;
        }
        catch (RequestFailedException e)
        {
            Debug.Log($"[DeleteAccountController] Deletion failed ({e.ErrorCode}): {e.Message}");
            SetStatus(e.ErrorCode == CommonErrorCodes.TransportError
                ? "No hi ha connexió. Comprova la xarxa i torna-ho a provar."
                : "No s'ha pogut eliminar el compte. Torna-ho a provar.", isError: true);
            _button.interactable = true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[DeleteAccountController] Unexpected error: {e}");
            SetStatus("Alguna cosa ha fallat. Torna-ho a provar.", isError: true);
            _button.interactable = true;
        }
    }

    private void SetStatus(string message, bool isError = false)
    {
        if (statusText == null) return;
        statusText.text = message;
        statusText.color = isError ? Color.red : _defaultStatusColor;
    }
}
