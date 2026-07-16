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
/// Runs the JoinLobby scene: lets the player either type a join code
/// (LobbyCode_INPUT + JoinLobbyCode_BTN) or pick an open lobby from the
/// nearby-lobbies browse list (OpenLobbies_Container > LobbiesContent,
/// populated with Lobby_PRFB rows). Either path joins the same kind of
/// Unity Gaming Services session HostLobbyManager creates, which
/// auto-configures Netcode for GameObjects as a client over Relay — no
/// separate "connect" step needed afterwards.
///
/// After a successful join we deliberately stay in this scene rather than
/// loading anything ourselves: once the host starts the match, Netcode's
/// own NetworkSceneManager pulls every connected client along automatically
/// (same pattern already used by InitialFightManager -> Main).
/// </summary>
public class JoinLobbyManager : MonoBehaviour
{
    [Header("Join by code")]
    [SerializeField] private TMP_InputField codeInput;   // LobbyCode_INPUT
    [SerializeField] private Button joinCodeButton;       // JoinLobbyCode_BTN

    [Header("Browse lobbies")]
    [SerializeField] private Transform lobbiesContainer;  // OpenLobbies_Container > LobbiesContent
    [SerializeField] private GameObject lobbyRowPrefab;   // Lobby_PRFB
    [SerializeField] private float refreshIntervalSeconds = 2f;

    [SerializeField] private TextMeshProUGUI statusText; // optional

    [Header("Flow")]
    [SerializeField] private string menuSceneName = "Menu";

    private readonly List<GameObject> _spawnedRows = new List<GameObject>();
    private QuerySessionsResults _queryResults;
    private Coroutine _refreshRoutine;
    private bool _joining;
    private bool _joined;
    private bool _cleanedUp;
    private string _ownSceneName;

    private async void Start()
    {
        // We live on the same GameObject as NetworkManager, which survives scene
        // loads (DontDestroyOnLoad). Once Netcode's own scene sync pulls us into
        // the match scene (or we get sent back to Menu), this component has
        // nothing left to do — watch for that and self-remove instead of quietly
        // riding along forever.
        _ownSceneName = gameObject.scene.name;
        SceneManager.sceneLoaded += OnAnySceneLoaded;

        if (joinCodeButton != null) joinCodeButton.onClick.AddListener(() => _ = JoinByCodeAsync());
        if (codeInput != null) codeInput.onSubmit.AddListener(ignored => _ = JoinByCodeAsync());

        try
        {
            await EnsureServicesReadyAsync();
        }
        catch (Exception e)
        {
            Debug.LogError($"[JoinLobbyManager] UGS init failed: {e}");
            SetStatus("No hi ha connexió. Torna-ho a provar.");
            return;
        }

        await RefreshLobbyListOnceAsync();
        _refreshRoutine = StartCoroutine(RefreshLoop());
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

        if (_refreshRoutine != null) StopCoroutine(_refreshRoutine);
        _queryResults?.StopPolling();

        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
    }

    private static async Task EnsureServicesReadyAsync()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
            await UnityServices.InitializeAsync();
        if (!AuthenticationService.Instance.IsSignedIn)
            throw new InvalidOperationException("Not signed in.");
    }

    // ================= BROWSE LIST =================

    private async Task RefreshLobbyListOnceAsync()
    {
        try
        {
            _queryResults = await MultiplayerService.Instance.QuerySessionsAsync(new QuerySessionsOptions());
            _queryResults.StartPolling(Mathf.Max(1, Mathf.RoundToInt(refreshIntervalSeconds)));
            RenderLobbyList(_queryResults.Sessions);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[JoinLobbyManager] Could not query open lobbies: {e}");
        }
    }

    private IEnumerator RefreshLoop()
    {
        WaitForSeconds wait = new WaitForSeconds(refreshIntervalSeconds);
        while (true)
        {
            yield return wait;
            if (_queryResults != null && !_joined) RenderLobbyList(_queryResults.Sessions);
        }
    }

    private void RenderLobbyList(IList<ISessionInfo> sessions)
    {
        if (lobbiesContainer == null || lobbyRowPrefab == null) return;

        foreach (GameObject row in _spawnedRows) if (row != null) Destroy(row);
        _spawnedRows.Clear();

        if (sessions == null) return;

        foreach (ISessionInfo info in sessions)
        {
            if (info.AvailableSlots <= 0 || info.IsLocked) continue;

            GameObject row = Instantiate(lobbyRowPrefab, lobbiesContainer);
            _spawnedRows.Add(row);

            LobbyListItemUI ui = row.GetComponent<LobbyListItemUI>();
            if (ui == null) continue;

            string hostName = !string.IsNullOrEmpty(info.Name) ? info.Name : "Lobby";
            int occupied = info.MaxPlayers - info.AvailableSlots;
            string sessionId = info.Id;

            ui.Set(hostName, occupied, info.MaxPlayers, () => _ = JoinByIdAsync(sessionId));
        }
    }

    // ================= JOINING =================

    private async Task JoinByCodeAsync()
    {
        if (_joining || _joined) return;
        string code = codeInput != null ? codeInput.text.Trim().ToUpperInvariant() : "";
        if (string.IsNullOrEmpty(code))
        {
            SetStatus("Introdueix un codi.");
            return;
        }

        _joining = true;
        SetButtonsInteractable(false);
        SetStatus("Unint-se a la sala...");

        try
        {
            ISession session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code, BuildJoinOptions());
            OnJoined(session);
        }
        catch (Exception e)
        {
            Debug.Log($"[JoinLobbyManager] Join by code failed: {e}");
            SetStatus("No s'ha trobat cap sala amb aquest codi.");
            _joining = false;
            SetButtonsInteractable(true);
        }
    }

    private async Task JoinByIdAsync(string sessionId)
    {
        if (_joining || _joined) return;

        _joining = true;
        SetButtonsInteractable(false);
        SetStatus("Unint-se a la sala...");

        try
        {
            ISession session = await MultiplayerService.Instance.JoinSessionByIdAsync(sessionId, BuildJoinOptions());
            OnJoined(session);
        }
        catch (Exception e)
        {
            Debug.Log($"[JoinLobbyManager] Join by id failed: {e}");
            SetStatus("Aquesta sala ja no està disponible.");
            _joining = false;
            SetButtonsInteractable(true);
        }
    }

    private static JoinSessionOptions BuildJoinOptions()
    {
        string displayName = PlayerSession.IsLoggedIn ? PlayerSession.PlayerName : "Player";
        return new JoinSessionOptions
        {
            Type = LobbySession.SessionType,
            PlayerProperties = new Dictionary<string, PlayerProperty>
            {
                { LobbySession.DisplayNamePropertyKey, new PlayerProperty(displayName, VisibilityPropertyOptions.Member) }
            }
        };
    }

    private void OnJoined(ISession session)
    {
        LobbySession.Set(session);
        _joining = false;
        _joined = true;

        _queryResults?.StopPolling();
        foreach (GameObject row in _spawnedRows) if (row != null) Destroy(row);
        _spawnedRows.Clear();

        SetStatus("Connectat! Esperant que l'amfitrió comenci la partida...");

        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
    }

    // ================= DISCONNECT SAFETY =================

    private void OnClientDisconnected(ulong clientId)
    {
        if (NetworkManager.Singleton == null || clientId != NetworkManager.Singleton.LocalClientId) return;

        LobbySession.Clear();
        NetworkManager.Singleton.Shutdown();
        SetStatus("S'ha perdut la connexió amb l'amfitrió.");
        SceneManager.LoadScene(menuSceneName, LoadSceneMode.Single);
    }

    private void SetButtonsInteractable(bool interactable)
    {
        if (joinCodeButton != null) joinCodeButton.interactable = interactable;
    }

    private void SetStatus(string message)
    {
        if (statusText != null) statusText.text = message;
    }
}
