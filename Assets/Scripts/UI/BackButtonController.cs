using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Generic "go back" behaviour for the corner BackButton of each scene.
///
/// Setup: add to a GameObject that has a Button, set targetSceneName to the
/// scene this button should return to. No manual onClick wiring is needed;
/// the listener is registered in Awake.
///
/// Behaviour:
///  - If a network session is running (hosting or connected), it is shut down
///    first so we never carry a live session back into a menu scene.
///  - Optionally signs the local player out (used by the Menu scene's back
///    button, which returns to the Login scene).
/// </summary>
[RequireComponent(typeof(Button))]
public class BackButtonController : MonoBehaviour
{
    [Tooltip("Scene loaded when this button is pressed.")]
    [SerializeField] private string targetSceneName = "Menu";

    [Tooltip("Also sign the local player out (use when going back to Login).")]
    [SerializeField] private bool signOutPlayer = false;

    private void Awake()
    {
        GetComponent<Button>().onClick.AddListener(GoBack);
    }

    public void GoBack()
    {
        // Backing out of a lobby: leave/delete the UGS session first (fire and
        // forget — we don't want to block the button on a network round trip).
        // Harmless no-op if there's no active lobby session (e.g. Menu's own
        // back button, or PlayerConfig).
        if (LobbySession.IsActive)
        {
            _ = LobbySession.LeaveOrDeleteAsync();
        }

        // Leaving a networked context: stop hosting / disconnect cleanly first.
        // NetworkManager survives scene loads (DontDestroyOnLoad), so an active
        // session would otherwise leak into the target scene.
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
        }

        if (signOutPlayer)
        {
            PlayerSession.SignOut();

            // Also tear down the Unity Authentication session, not just our local
            // PlayerSession state. UGS survives scene loads, so if we skipped this
            // the Login scene would still see the player as "signed in" and reject
            // the next SignIn with error 10000. Guarded because services may not
            // be initialized in every scene this button can appear in.
            if (UnityServices.State == ServicesInitializationState.Initialized
                && AuthenticationService.Instance.IsSignedIn)
            {
                AuthenticationService.Instance.SignOut();
            }
        }

        SceneManager.LoadScene(targetSceneName, LoadSceneMode.Single);
    }
}
