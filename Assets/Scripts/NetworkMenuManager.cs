using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement; // Required for LoadSceneMode
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// Lobby menu networking over the internet using Unity Relay.
///
/// Flow: the Host creates a Relay allocation, gets a short join code, and
/// StartHost()s through Relay. The other player types that code and joins the
/// same allocation, then StartClient()s. No IP addresses, no port forwarding —
/// Relay routes both players through Unity's servers. This also works for two
/// machines on the same LAN, so it replaces the old direct-IP connection.
///
/// Requires Unity Gaming Services set up (project linked + Relay enabled) and
/// the com.unity.services.multiplayer package installed.
/// </summary>
public class NetworkMenuManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Button hostButton;
    [SerializeField] private Button joinButton;
    [SerializeField] private Button startGameButton; // Shown to the host once someone joins
    [SerializeField] private TextMeshProUGUI statusText;

    [Header("Relay")]
    [Tooltip("The client types the host's join code here. When hosting, the " +
             "generated code is written into this same field so it's easy to copy. " +
             "(Renamed from the old LAN 'ipInputField'.)")]
    [FormerlySerializedAs("ipInputField")]
    [SerializeField] private TMP_InputField joinCodeInput;

    [Tooltip("Optional. If assigned, the host's join code is shown here in big. " +
             "If null, it is shown in the status text instead. (Renamed from 'localIpText'.)")]
    [FormerlySerializedAs("localIpText")]
    [SerializeField] private TextMeshProUGUI codeDisplayText;

    [Tooltip("Total players in a match (host included). A 2-player game needs 1 extra Relay connection.")]
    [SerializeField] private int maxPlayers = 2;

    // Relay's secure transport protocol. "dtls" = encrypted UDP (recommended);
    // "udp" is unencrypted; "wss" is for WebGL builds.
    private const string RelayConnectionType = "dtls";

    // Guards UGS init/sign-in so it happens exactly once even if both buttons
    // are pressed or the flow is retried.
    private Task _servicesReady;

    private void Start()
    {
        if(SceneManager.GetActiveScene().name == "Menu")
        {
            hostButton.onClick.AddListener(OnHostClicked);
            joinButton.onClick.AddListener(OnJoinClicked);
            startGameButton.onClick.AddListener(StartGame);

            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
            NetworkManager.Singleton.OnTransportFailure += OnTransportFailure;
        }
        

        // Warm up Unity Gaming Services in the background so the first Host/Join
        // press doesn't have to wait for initialization + sign-in.
        _servicesReady = InitializeServicesAsync();
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            NetworkManager.Singleton.OnTransportFailure -= OnTransportFailure;
        }
    }

    // ================= UGS INITIALIZATION =================

    // Initializes Unity Services and signs in anonymously (Relay needs an
    // authenticated session; anonymous is enough and is per-machine).
    private async Task InitializeServicesAsync()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
            await UnityServices.InitializeAsync();

        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
    }

    // Awaits (and if needed retries) the one-time init before any Relay call.
    private Task EnsureServicesReady()
    {
        if (_servicesReady == null || _servicesReady.IsFaulted || _servicesReady.IsCanceled)
            _servicesReady = InitializeServicesAsync();
        return _servicesReady;
    }

    private UnityTransport Transport => NetworkManager.Singleton.GetComponent<UnityTransport>();

    // ================= HOST =================

    private async void OnHostClicked()
    {
        DisableButtons();
        statusText.text = "Creating room...";

        try
        {
            await EnsureServicesReady();

            // maxPlayers includes the host; Relay counts the OTHER connections.
            int maxConnections = Mathf.Max(1, maxPlayers - 1);
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxConnections);
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            Transport.SetRelayServerData(allocation.ToRelayServerData(RelayConnectionType));

            if (NetworkManager.Singleton.StartHost())
            {
                ShowJoinCode(joinCode);
            }
            else
            {
                statusText.text = "Could not start the host. Try again.";
                EnableButtons();
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[Relay] Host failed: {e}");
            statusText.text = "Could not create the room. Check your internet connection.";
            EnableButtons();
        }
    }

    // ================= JOIN =================

    private async void OnJoinClicked()
    {
        string joinCode = joinCodeInput != null ? joinCodeInput.text.Trim().ToUpperInvariant() : "";
        if (string.IsNullOrEmpty(joinCode))
        {
            statusText.text = "Type the room code first.";
            return;
        }

        DisableButtons();
        statusText.text = $"Joining room {joinCode}...";

        try
        {
            await EnsureServicesReady();

            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
            Transport.SetRelayServerData(joinAllocation.ToRelayServerData(RelayConnectionType));

            if (!NetworkManager.Singleton.StartClient())
            {
                statusText.text = "Could not start the connection. Try again.";
                EnableButtons();
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[Relay] Join failed: {e}");
            statusText.text = "Wrong code, or the room no longer exists.";
            EnableButtons();
        }
    }

    // ================= CONNECTION CALLBACKS =================

    private void OnClientConnected(ulong clientId)
    {
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            statusText.text = "Connected!";
        }
        else if (NetworkManager.Singleton.IsHost)
        {
            statusText.text = $"Player {clientId} joined the game! Ready to start.";
        }

        if (NetworkManager.Singleton.IsHost && NetworkManager.Singleton.ConnectedClientsList.Count >= 2)
        {
            startGameButton.gameObject.SetActive(true);
        }
    }

    // On a client this fires for a failed/dropped connection, so we reset the menu
    // instead of leaving it stuck on "Joining...".
    private void OnClientDisconnected(ulong clientId)
    {
        if (NetworkManager.Singleton == null) return;

        if (!NetworkManager.Singleton.IsServer && clientId == NetworkManager.Singleton.LocalClientId)
        {
            string reason = NetworkManager.Singleton.DisconnectReason;
            statusText.text = string.IsNullOrEmpty(reason)
                ? "Disconnected. Check the code and try again."
                : $"Disconnected: {reason}";
            ResetMenu();
        }
        else if (NetworkManager.Singleton.IsServer && NetworkManager.Singleton.ConnectedClientsList.Count < 2)
        {
            statusText.text = "The other player left. Waiting for players...";
            startGameButton.gameObject.SetActive(false);
        }
    }

    private void OnTransportFailure()
    {
        statusText.text = "Network error - connection closed. You can try again.";
        ResetMenu();
    }

    private void ResetMenu()
    {
        EnableButtons();
        startGameButton.gameObject.SetActive(false);
    }

    // ================= START THE MATCH =================

    // Loads the InitialFight scene, where the initiative roll-off decides
    // who starts; that scene then loads Main with the winner already known.
    private void StartGame()
    {
        if (NetworkManager.Singleton.IsServer)
        {
            statusText.text = "Loading game...";
            NetworkManager.Singleton.SceneManager.LoadScene("InitialFight", LoadSceneMode.Single);
        }
    }

    // ================= HELPERS =================

    private void ShowJoinCode(string joinCode)
    {
        // Put the code where the client would type it too, so the host can just
        // read/copy it off the screen.
        if (joinCodeInput != null)
        {
            joinCodeInput.SetTextWithoutNotify(joinCode);
            joinCodeInput.interactable = false;
        }

        string message = $"Room code: {joinCode}\nShare it with the other player. Waiting for them to join...";
        if (codeDisplayText != null)
            codeDisplayText.text = $"Code: {joinCode}";

        statusText.text = message;
    }

    private void DisableButtons()
    {
        hostButton.interactable = false;
        joinButton.interactable = false;
    }

    private void EnableButtons()
    {
        hostButton.interactable = true;
        joinButton.interactable = true;
        if (joinCodeInput != null) joinCodeInput.interactable = true;
    }
}
