using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Runs the HostLobby scene: creates a Unity Gaming Services multiplayer
/// Session (Relay-backed, which auto-starts Netcode for GameObjects as
/// host), shows the join code, and lists connected players via
/// PlayerInLobby_PRFB rows under PlayersLobby_Container.
///
/// Because every scene has EnableSceneManagement on, Netcode scene-syncs a
/// newly-joined client straight into whatever scene the host currently has
/// loaded — so the client lands in this same HostLobby scene shortly after
/// joining and runs this same component. We branch behaviour on the local
/// Netcode role:
///  - Host: creates the session, sees Config_Container, and must press
///    StartMatch_BTN explicitly once both players are connected (no auto-start).
///  - Client: never creates a session (it just mirrors the host's session),
///    never sees Config_Container, shows the HOST's real room code and both
///    players, and StartMatch_BTN is locked to "Waiting for host" /
///    non-interactable — only the host can start the match.
///
/// IMPORTANT — why we detect the role from NetworkManager and NOT from
/// LobbySession: on a joining client, JoinLobbyManager.OnJoined() (which calls
/// LobbySession.Set) runs as the continuation of JoinSessionByCodeAsync, and
/// Netcode can scene-sync the client into this scene — running this Start()
/// BEFORE that continuation executes. So at Start() time LobbySession.Current
/// is frequently still null on the client, which previously made the client
/// fall through to the host branch and create a brand-new empty room (it saw
/// only itself + a fresh code). The Netcode role, by contrast, is already
/// settled: a joining client has StartClient()'d (IsClient == true) before the
/// scene sync, while the host has not started Netcode yet at this point.
/// </summary>
public class HostLobbyManager : MonoBehaviour
{
    [Header("Lobby UI")]
    [SerializeField] private TextMeshProUGUI joinCodeText;
    [SerializeField] private Transform playersContainer; // Container > PlayersLobby_Container
    [SerializeField] private GameObject playerRowPrefab;  // PlayerInLobby_PRFB
    [SerializeField] private Button startMatchButton;
    [SerializeField] private TextMeshProUGUI startMatchButtonText; // StartMatch_BTN > StartMatch_BTN_TXT
    [SerializeField] private GameObject configContainer;  // Config_Container - host-only
    [SerializeField] private TextMeshProUGUI statusText; // optional

    [Header("Flow")]
    [SerializeField] private string matchSceneName = "InitialFight";
    [SerializeField] private int maxPlayers = 2;

    private const string StartButtonDefaultText = "Start Match";
    private const string StartButtonWaitingText = "Waiting for host";

    private bool _isClient;
    private bool _sessionEventsBound;

    private readonly List<GameObject> _spawnedRows = new List<GameObject>();
    private IHostSession _hostSession;
    private bool _matchStarting;
    private bool _cleanedUp;
    private string _ownSceneName;

    private async void Start()
    {
        // We live on a dedicated (non-NetworkManager) GameObject in this scene.
        // Once the match actually starts and the host loads matchSceneName,
        // this component would otherwise keep riding along forever with
        // nothing left to do. Watch for that and self-remove.
        _ownSceneName = gameObject.scene.name;
        SceneManager.sceneLoaded += OnAnySceneLoaded;

        // Role detection — see the class summary for why this is Netcode-based
        // and not LobbySession-based.
        NetworkManager nm = NetworkManager.Singleton;
        _isClient = nm != null && nm.IsClient && !nm.IsServer;

        if (nm != null)
        {
            nm.OnClientConnectedCallback += OnNetworkClientConnected;
            nm.OnClientDisconnectCallback += OnNetworkClientDisconnected;
        }

        if (_isClient)
        {
            RunAsClient();
            return;
        }

        await RunAsHostAsync();
    }

    // ================= CLIENT =================

    private void RunAsClient()
    {
        // The client only mirrors the host-owned session; it never creates one
        // and never controls the match start.
        if (configContainer != null) configContainer.SetActive(false);

        if (startMatchButton != null)
        {
            startMatchButton.onClick.RemoveAllListeners();
            startMatchButton.interactable = false;
        }
        if (startMatchButtonText != null) startMatchButtonText.text = StartButtonWaitingText;

        SetStatus("Connectat! Esperant que l'amfitrió comenci la partida...");

        // LobbySession.Current may not be set yet at this instant (it's set by
        // JoinLobbyManager.OnJoined, which can run a frame or two after this
        // scene loaded). Wait for it, then show the host's real code + players.
        StartCoroutine(BindToSessionWhenReady());
    }

    private IEnumerator BindToSessionWhenReady()
    {
        float timeout = 10f;
        while (LobbySession.Current == null && timeout > 0f)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

        ISession session = LobbySession.Current;
        if (session == null)
        {
            SetStatus("No s'ha pogut llegir la sala. Torna enrere i prova-ho de nou.");
            yield break;
        }

        // Show the HOST's room code — never a freshly generated one.
        if (joinCodeText != null && !string.IsNullOrEmpty(session.Code))
            joinCodeText.text = session.Code;

        BindSessionEvents(session);
        RefreshPlayerList();
    }

    // ================= HOST =================

    private async Task RunAsHostAsync()
    {
        if (configContainer != null) configContainer.SetActive(true);
        if (startMatchButtonText != null) startMatchButtonText.text = StartButtonDefaultText;

        if (startMatchButton != null)
        {
            startMatchButton.onClick.AddListener(() => _ = StartMatchAsync());
            startMatchButton.interactable = false;
        }

        SetStatus("Creant la sala...");

        try
        {
            await EnsureServicesReadyAsync();
            await CreateSessionAsync();
        }
        catch (Exception e)
        {
            Debug.LogError($"[HostLobbyManager] Could not create session: {e}");
            SetStatus("No s'ha pogut crear la sala. Torna-ho a provar.");
            return;
        }

        RefreshPlayerList();
    }

    private void OnDestroy() => Cleanup();

    private void OnAnySceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == _ownSceneName) return; // reload of our own scene, not a hand-off
        Cleanup();
        Destroy(this);
    }

    private void Cleanup()
    {
        if (_cleanedUp) return;
        _cleanedUp = true;

        SceneManager.sceneLoaded -= OnAnySceneLoaded;

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnNetworkClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnNetworkClientDisconnected;
        }

        ISession session = LobbySession.Current;
        if (session != null && _sessionEventsBound)
        {
            session.PlayerJoined -= OnSessionPlayerChanged;
            session.PlayerHasLeft -= OnSessionPlayerChanged;
            session.PlayerPropertiesChanged -= OnSessionPropertiesChanged;
        }
    }

    private static async Task EnsureServicesReadyAsync()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
            await UnityServices.InitializeAsync();
        if (!AuthenticationService.Instance.IsSignedIn)
            throw new InvalidOperationException("Not signed in.");
    }

    // ================= SESSION CREATION =================

    private async Task CreateSessionAsync()
    {
        string displayName = PlayerSession.IsLoggedIn ? PlayerSession.PlayerName : "Host";

        SessionOptions options = new SessionOptions
        {
            Name = $"{displayName}'s Lobby",
            MaxPlayers = maxPlayers,
            IsPrivate = false,
            IsLocked = false,
            Type = LobbySession.SessionType,
            PlayerProperties = new Dictionary<string, PlayerProperty>
            {
                { LobbySession.DisplayNamePropertyKey, new PlayerProperty(displayName, VisibilityPropertyOptions.Member) }
            }
        }.WithRelayNetwork();

        _hostSession = await MultiplayerService.Instance.CreateSessionAsync(options);
        LobbySession.Set(_hostSession);

        BindSessionEvents(_hostSession);

        if (joinCodeText != null)
            joinCodeText.text = _hostSession.Code;

        SetStatus("Esperant que el rival s'uneixi...");
    }

    // Subscribes to a session's player-change events exactly once, whether we
    // are the host (our own created session) or the client (the host's session,
    // once LobbySession.Current is populated).
    private void BindSessionEvents(ISession session)
    {
        if (session == null || _sessionEventsBound) return;
        _sessionEventsBound = true;

        session.PlayerJoined += OnSessionPlayerChanged;
        session.PlayerHasLeft += OnSessionPlayerChanged;
        session.PlayerPropertiesChanged += OnSessionPropertiesChanged;
    }

    private void OnSessionPlayerChanged(string playerId) => RefreshPlayerList();
    private void OnSessionPropertiesChanged() => RefreshPlayerList();

    // ================= NETCODE CONNECTION =================

    private void OnNetworkClientConnected(ulong clientId)
    {
        RefreshPlayerList();

        // Only the host controls the Start Match button — it becomes
        // interactable once both players are connected, but the host must
        // still press it explicitly (no auto-start).
        if (!_isClient
            && NetworkManager.Singleton != null
            && NetworkManager.Singleton.IsServer
            && NetworkManager.Singleton.ConnectedClientsList.Count >= maxPlayers)
        {
            if (startMatchButton != null) startMatchButton.interactable = true;
            SetStatus("Els dos jugadors estan connectats! Prem 'Start Match' per començar.");
        }
    }

    private void OnNetworkClientDisconnected(ulong clientId)
    {
        if (_matchStarting) return; // already transitioning to the match, ignore

        RefreshPlayerList();

        if (!_isClient)
        {
            if (startMatchButton != null) startMatchButton.interactable = false;
            SetStatus("El rival s'ha desconnectat. Esperant que torni a entrar...");
        }
    }

    // ================= PLAYER LIST =================

    private void RefreshPlayerList()
    {
        ISession session = LobbySession.Current;
        if (session == null || playersContainer == null || playerRowPrefab == null) return;

        foreach (GameObject row in _spawnedRows) if (row != null) Destroy(row);
        _spawnedRows.Clear();

        string localPlayerId = session.CurrentPlayer?.Id;
        string hostPlayerId = session.Host;

        // Derive "both connected" from the session's own membership so it works
        // identically on the host and the client (ConnectedClientsList is only
        // populated on the server).
        int playerCount = session.Players != null ? session.Players.Count : 0;
        bool bothConnected = playerCount >= maxPlayers;

        foreach (IReadOnlyPlayer player in session.Players)
        {
            GameObject row = Instantiate(playerRowPrefab, playersContainer);
            _spawnedRows.Add(row);

            PlayerInLobbyItemUI ui = row.GetComponent<PlayerInLobbyItemUI>();
            if (ui == null) continue;

            string name = player.Properties != null
                && player.Properties.TryGetValue(LobbySession.DisplayNamePropertyKey, out PlayerProperty prop)
                ? prop.Value
                : "Player";

            bool isYou = player.Id == localPlayerId;
            bool isHostRow = player.Id == hostPlayerId;
            bool isReady = isHostRow || bothConnected;

            ui.Set(name, isYou, isHostRow, isReady);
        }
    }

    // ================= STARTING THE MATCH =================

    private async Task StartMatchAsync()
    {
        if (_matchStarting) return;
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
        if (NetworkManager.Singleton.ConnectedClientsList.Count < maxPlayers) return;

        _matchStarting = true;
        if (startMatchButton != null) startMatchButton.interactable = false;
        SetStatus("Començant la partida...");

        if (_hostSession != null)
        {
            try
            {
                _hostSession.IsLocked = true; // stop anyone else joining mid-match
                await _hostSession.SavePropertiesAsync();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[HostLobbyManager] Could not lock session before starting: {e}");
            }
        }

        NetworkManager.Singleton.SceneManager.LoadScene(matchSceneName, LoadSceneMode.Single);
    }

    private void SetStatus(string message)
    {
        if (statusText != null) statusText.text = message;
    }
}
