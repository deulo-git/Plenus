using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Runs the initiative roll-off in the InitialFight scene.
///
/// Flow: the Menu scene (host) loads this scene via NetworkSceneManager for
/// both players. Once both are assigned, either player may roll at any time
/// (no turn order) - each rolls independently and can't roll again until the
/// round resolves. Highest total wins; ties are re-rolled. The winner is
/// stored in InitiativeResult (server side) and the server then loads the
/// Main scene, where GameManager skips its own initiative phase and starts
/// the match with that winner.
///
/// Scene wiring: one shared Roll button, one DiceManager per player panel
/// (each showing that player's 3D numeric dice), the two Username__TXT labels
/// and the Output_TXT status label.
/// </summary>
public class InitialFightManager : NetworkBehaviour
{
    [Header("UI References")]
    [SerializeField] private Button rollButton;
    [SerializeField] private TextMeshProUGUI playerANameText;
    [SerializeField] private TextMeshProUGUI playerBNameText;
    [SerializeField] private TextMeshProUGUI statusText;

    [Header("Dice")]
    [SerializeField] private DiceManager playerADice;
    [SerializeField] private DiceManager playerBDice;
    [Tooltip("How many numeric dice each player rolls. Must match the dice wired into each player's DiceManager (currently 1).")]
    [SerializeField] private int dicePerPlayer = 1;

    [Header("Flow")]
    [SerializeField] private string mainSceneName = "Main";
    [SerializeField] private string menuSceneName = "Menu";
    [Tooltip("Seconds to let the dice animation settle before comparing (roll animation is ~1.2s).")]
    [SerializeField] private float rollSettleSeconds = 1.6f;
    [Tooltip("Seconds the winner announcement stays on screen before the match scene loads.")]
    [SerializeField] private float winnerDisplaySeconds = 2.5f;

    private enum Phase { WaitingForPlayers, Rolling, Comparing, Done }

    // Authoritative state, written by the server only.
    private readonly NetworkVariable<int> netPhase = new NetworkVariable<int>((int)Phase.WaitingForPlayers);
    private readonly NetworkVariable<ulong> netPlayerAId = new NetworkVariable<ulong>(0);
    private readonly NetworkVariable<ulong> netPlayerBId = new NetworkVariable<ulong>(0);
    private readonly NetworkVariable<FixedString64Bytes> netPlayerAName = new NetworkVariable<FixedString64Bytes>(default);
    private readonly NetworkVariable<FixedString64Bytes> netPlayerBName = new NetworkVariable<FixedString64Bytes>(default);
    private readonly NetworkVariable<FixedString512Bytes> netMessage = new NetworkVariable<FixedString512Bytes>(default);
    private readonly NetworkVariable<bool> netARolled = new NetworkVariable<bool>(false);
    private readonly NetworkVariable<bool> netBRolled = new NetworkVariable<bool>(false);

    // Server-only working state.
    private readonly Dictionary<ulong, string> namesByClient = new Dictionary<ulong, string>();
    private readonly System.Random rng = new System.Random();
    private int totalA;
    private int totalB;
    private bool resolving;

    private Phase CurrentPhase => (Phase)netPhase.Value;

    public override void OnNetworkSpawn()
    {
        netPhase.OnValueChanged += (_, __) => RefreshUI();
        netPlayerAName.OnValueChanged += (_, __) => RefreshUI();
        netPlayerBName.OnValueChanged += (_, __) => RefreshUI();
        netMessage.OnValueChanged += (_, __) => RefreshUI();
        netARolled.OnValueChanged += (_, __) => RefreshUI();
        netBRolled.OnValueChanged += (_, __) => RefreshUI();

        if (rollButton != null) rollButton.onClick.AddListener(OnRollClicked);

        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

        if (IsServer)
        {
            namesByClient[NetworkManager.ServerClientId] = LocalPlayerNameOrDefault(1);
            ServerTryAssignPlayers();
        }
        else
        {
            SubmitNameServerRpc(LocalPlayerNameOrDefault(2));
        }

        RefreshUI();
    }

    public override void OnNetworkDespawn()
    {
        if (rollButton != null) rollButton.onClick.RemoveListener(OnRollClicked);
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
    }

    private static string LocalPlayerNameOrDefault(int fallbackNumber)
    {
        return PlayerSession.IsLoggedIn ? PlayerSession.PlayerName : $"Player {fallbackNumber}";
    }

    // ================= PLAYER ASSIGNMENT =================

    [ServerRpc(RequireOwnership = false)]
    private void SubmitNameServerRpc(string playerName, ServerRpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;
        if (string.IsNullOrWhiteSpace(playerName)) playerName = $"Player {sender + 1}";
        playerName = playerName.Trim();
        namesByClient[sender] = playerName;

        // The host's OnNetworkSpawn runs synchronously and can assign players
        // before this RPC (sent from the client's own OnNetworkSpawn) has had
        // time to make the round trip over the network. When that happens,
        // ServerTryAssignPlayers() already locked in a "Player 2" fallback and
        // never runs again (it's guarded to WaitingForPlayers only). Patch the
        // live NetworkVariable directly here so a late-arriving name still
        // reaches the UI instead of being silently dropped.
        if (CurrentPhase != Phase.WaitingForPlayers)
        {
            if (sender == netPlayerAId.Value) netPlayerAName.Value = playerName;
            else if (sender == netPlayerBId.Value) netPlayerBName.Value = playerName;
        }

        ServerTryAssignPlayers();
    }

    private void ServerTryAssignPlayers()
    {
        if (!IsServer || CurrentPhase != Phase.WaitingForPlayers) return;
        if (NetworkManager.ConnectedClientsList.Count < 2) return;

        ulong hostId = NetworkManager.ServerClientId;
        ulong otherId = hostId;
        foreach (ulong id in NetworkManager.ConnectedClientsIds)
        {
            if (id != hostId) { otherId = id; break; }
        }

        netPlayerAId.Value = hostId;
        netPlayerBId.Value = otherId;
        netPlayerAName.Value = namesByClient.TryGetValue(hostId, out string a) ? a : "Player 1";
        netPlayerBName.Value = namesByClient.TryGetValue(otherId, out string b) ? b : "Player 2";

        totalA = 0;
        totalB = 0;
        netARolled.Value = false;
        netBRolled.Value = false;
        netPhase.Value = (int)Phase.Rolling;
        SvSetMessage("Both players, roll for initiative!");
    }

    // ================= ROLLING =================

    private void OnRollClicked()
    {
        if (!IsRollAvailableForMe()) return;
        RequestRollServerRpc();
    }

    private bool IsRollAvailableForMe()
    {
        if (NetworkManager.Singleton == null) return false;
        if (CurrentPhase != Phase.Rolling) return false;
        ulong myId = NetworkManager.Singleton.LocalClientId;
        if (myId == netPlayerAId.Value) return !netARolled.Value;
        if (myId == netPlayerBId.Value) return !netBRolled.Value;
        return false;
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestRollServerRpc(ServerRpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;
        if (CurrentPhase != Phase.Rolling) return;

        bool isA = sender == netPlayerAId.Value;
        bool isB = sender == netPlayerBId.Value;
        if (!isA && !isB) return;
        if (isA && netARolled.Value) return;
        if (isB && netBRolled.Value) return;

        int[] values = ServerRollDice();
        int total = Sum(values);
        if (isA) { totalA = total; netARolled.Value = true; }
        else { totalB = total; netBRolled.Value = true; }
        ApplyRollClientRpc(isA, values);

        if (netARolled.Value && netBRolled.Value)
        {
            netPhase.Value = (int)Phase.Comparing;
            SvSetMessage($"{netPlayerAName.Value} rolled {totalA}, {netPlayerBName.Value} rolled {totalB}. Comparing...");
            if (!resolving) StartCoroutine(ServerResolveRolls());
        }
        else
        {
            string rollerName = (isA ? netPlayerAName.Value : netPlayerBName.Value).ToString();
            string waitingName = (isA ? netPlayerBName.Value : netPlayerAName.Value).ToString();
            SvSetMessage($"{rollerName} rolled {total}. Waiting for {waitingName} to roll...");
        }
    }

    private int[] ServerRollDice()
    {
        int count = Mathf.Max(1, dicePerPlayer);
        int[] values = new int[count];
        for (int i = 0; i < count; i++) values[i] = rng.Next(1, 7);
        return values;
    }

    private static int Sum(int[] values)
    {
        int total = 0;
        foreach (int v in values) total += v;
        return total;
    }

    [ClientRpc]
    private void ApplyRollClientRpc(bool isPlayerA, int[] values)
    {
        DiceManager dice = isPlayerA ? playerADice : playerBDice;
        if (dice != null) dice.ApplyRoll(values, null, false);
    }

    // ================= RESOLUTION =================

    private IEnumerator ServerResolveRolls()
    {
        resolving = true;

        // Let both dice animations finish before announcing anything.
        yield return new WaitForSeconds(rollSettleSeconds);

        if (totalA == totalB)
        {
            SvSetMessage($"Tie at {totalA}! Roll again, both players.");
            totalA = 0;
            totalB = 0;
            netARolled.Value = false;
            netBRolled.Value = false;
            netPhase.Value = (int)Phase.Rolling;
            resolving = false;
            yield break;
        }

        bool aWins = totalA > totalB;
        ulong winnerId = aWins ? netPlayerAId.Value : netPlayerBId.Value;
        string winnerName = (aWins ? netPlayerAName.Value : netPlayerBName.Value).ToString();

        netPhase.Value = (int)Phase.Done;
        SvSetMessage($"{winnerName} wins the roll-off {Mathf.Max(totalA, totalB)} - {Mathf.Min(totalA, totalB)} and starts the match!");

        // Hand the result to the Main scene's GameManager (server side only).
        InitiativeResult.Set(winnerId);

        yield return new WaitForSeconds(winnerDisplaySeconds);
        NetworkManager.Singleton.SceneManager.LoadScene(mainSceneName, LoadSceneMode.Single);
    }

    // ================= DISCONNECT SAFETY =================

    private void OnClientDisconnected(ulong clientId)
    {
        // We (a client) lost the connection to the host: leave the dead session.
        if (!IsServer && NetworkManager.Singleton != null && clientId == NetworkManager.Singleton.LocalClientId)
        {
            NetworkManager.Singleton.Shutdown();
            SceneManager.LoadScene(menuSceneName, LoadSceneMode.Single);
            return;
        }

        // We are the host and the opponent left before the roll-off finished.
        if (IsServer && CurrentPhase != Phase.Done)
        {
            SvSetMessage("The opponent disconnected. Use the back button to return to the menu.");
        }
    }

    // ================= UI =================

    private void RefreshUI()
    {
        if (playerANameText != null && netPlayerAName.Value.Length > 0)
            playerANameText.text = netPlayerAName.Value.ToString();
        if (playerBNameText != null && netPlayerBName.Value.Length > 0)
            playerBNameText.text = netPlayerBName.Value.ToString();

        if (statusText != null)
        {
            statusText.text = CurrentPhase == Phase.WaitingForPlayers && netMessage.Value.Length == 0
                ? "Waiting for players..."
                : netMessage.Value.ToString();
        }

        if (rollButton != null) rollButton.interactable = IsRollAvailableForMe();
    }

    private void SvSetMessage(string message)
    {
        if (!IsServer) return;
        if (message == null) message = "";
        if (message.Length > 400) message = message.Substring(0, 400);
        netMessage.Value = new FixedString512Bytes(message);
    }
}
