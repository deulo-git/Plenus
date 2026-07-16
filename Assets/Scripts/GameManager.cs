using System;
using System.Collections;
using System.Collections.Generic;
using Assets.Scripts;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

// Compact, network-friendly snapshot of one player's score-relevant state.
// All fields are primitives so it works as a NetworkVariable value.
// rowsPacked: 15 rows x 2 bits (0=None,1=First,2=Second).
// colorsPacked: 5 colors x 2 bits (Blue,Red,Green,Yellow,Orange in enum order).
public struct PlayerScoreSync : INetworkSerializable, IEquatable<PlayerScoreSync>
{
    public int score;
    public int wildcards;
    public int stars;
    public int rowsPacked;
    public int colorsPacked;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref score);
        serializer.SerializeValue(ref wildcards);
        serializer.SerializeValue(ref stars);
        serializer.SerializeValue(ref rowsPacked);
        serializer.SerializeValue(ref colorsPacked);
    }

    public bool Equals(PlayerScoreSync o) =>
        score == o.score && wildcards == o.wildcards && stars == o.stars &&
        rowsPacked == o.rowsPacked && colorsPacked == o.colorsPacked;
}

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Network Spawning")]
    [SerializeField] private GameObject networkBoardPrefab;
    [SerializeField] private RectTransform canvasRectTransform;

    public enum GameState { Idle, Initiative, RollingNumeric, ActivePlayerTurn, PassivePlayerTurn }

    [Header("Player Data")]
    // Real names arrive from each client's PlayerSession via SubmitNameServerRpc;
    // these are only placeholders until AssignAndStart resolves them.
    public PlayerData player1 = new PlayerData(1, "Player 1");
    public PlayerData player2 = new PlayerData(2, "Player 2");

    [Header("Managers & Boards")]
    public BoardManager player1Board;
    public BoardManager player2Board;
    public DiceManager diceManager;
    public SelectionManager selectionManager;

    [Header("Debug")]
    [SerializeField] private GameDebugUI debugUI;

    // ------------------------------------------------------------------
    // AUTHORITATIVE, SYNCED GAME STATE (written by the server only)
    // ------------------------------------------------------------------
    private readonly NetworkVariable<int> netState = new NetworkVariable<int>((int)GameState.Idle);
    private readonly NetworkVariable<ulong> netActiveClientId = new NetworkVariable<ulong>(0);
    private readonly NetworkVariable<int> netRound = new NetworkVariable<int>(0);
    private readonly NetworkVariable<bool> netDiceRolled = new NetworkVariable<bool>(false);
    private readonly NetworkVariable<int> netP1Init = new NetworkVariable<int>(0);
    private readonly NetworkVariable<int> netP2Init = new NetworkVariable<int>(0);
    private readonly NetworkVariable<bool> netComparing = new NetworkVariable<bool>(false);
    private readonly NetworkVariable<FixedString512Bytes> netMessage = new NetworkVariable<FixedString512Bytes>(default);

    // Live selection preview: the ACTIVE player's currently chosen number/color die.
    private readonly NetworkVariable<int> netActiveNumber = new NetworkVariable<int>(0);
    private readonly NetworkVariable<int> netActiveColor = new NetworkVariable<int>(-1);

    // Per-player score snapshots (drive each machine's own score panel).
    private readonly NetworkVariable<PlayerScoreSync> netP1ScoreState = new NetworkVariable<PlayerScoreSync>(default);
    private readonly NetworkVariable<PlayerScoreSync> netP2ScoreState = new NetworkVariable<PlayerScoreSync>(default);

    // ---- Board data distribution ----
    private byte[] cachedBoard;
    private bool boardGenerated;

    private BoardManager myBoardInstance;
    private BoardManager opponentBoardInstance;
    private bool viewingOpponent = false;

    // ---- Server-only orchestration ----
    private readonly Dictionary<ulong, BoardManager> boardsByClient = new Dictionary<ulong, BoardManager>();
    private readonly HashSet<ulong> boardReady = new HashSet<ulong>();
    private readonly System.Random serverRng = new System.Random();
    private ulong roundStartingClientId;
    private bool gameStarted;
    private bool gameOver;

    // ---- Server-only: player names by client + database recording ----
    private readonly Dictionary<ulong, string> namesByClient = new Dictionary<ulong, string>();
    private readonly Dictionary<ulong, long> dbMatchPlayerIds = new Dictionary<ulong, long>();
    private long dbBoardId = -1;
    private long dbMatchId = -1;
    private int dbTurnNumber = 0;

    private static readonly int[] EmptyCells = new int[0];

    // ---- Derived views ----
    public GameState CurrentState => (GameState)netState.Value;
    public int currentRound => netRound.Value;

    public PlayerData CurrentActivePlayer =>
        (player2 != null && player2.clientId != player1.clientId && netActiveClientId.Value == player2.clientId)
            ? player2 : player1;

    private int ActivePlayerNumber =>
        (player2 != null && player2.clientId != player1.clientId && netActiveClientId.Value == player2.clientId)
            ? 2 : 1;

    public bool IsMyTurn =>
        NetworkManager.Singleton != null && netActiveClientId.Value == NetworkManager.Singleton.LocalClientId;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public override void OnNetworkSpawn()
    {
        netState.OnValueChanged += (_, __) => OnStateSynced();
        netActiveClientId.OnValueChanged += (_, __) => OnStateSynced();
        netRound.OnValueChanged += (_, __) => OnStateSynced();
        netDiceRolled.OnValueChanged += (_, __) => OnStateSynced();
        netP1Init.OnValueChanged += (_, __) => OnStateSynced();
        netP2Init.OnValueChanged += (_, __) => OnStateSynced();
        netComparing.OnValueChanged += (_, __) => OnStateSynced();
        netMessage.OnValueChanged += (_, __) => OnStateSynced();
        netActiveNumber.OnValueChanged += (_, __) => OnStateSynced();
        netActiveColor.OnValueChanged += (_, __) => OnStateSynced();

        netP1ScoreState.OnValueChanged += (_, cur) => { if (!IsServer) ApplyScoreState(player1, cur); OnScoreSynced(); };
        netP2ScoreState.OnValueChanged += (_, cur) => { if (!IsServer) ApplyScoreState(player2, cur); OnScoreSynced(); };

        if (IsServer)
        {
            // Register the host's own name locally (no RPC needed).
            namesByClient[NetworkManager.ServerClientId] = LocalPlayerNameOrDefault(1);

            EnsureBoardGenerated();
            BuildLocalBoards(cachedBoard);
            boardReady.Add(NetworkManager.ServerClientId);
            TryStartGame();
        }
        else
        {
            // Send this client's logged-in name to the server, then ask for the board.
            SubmitNameServerRpc(LocalPlayerNameOrDefault(2));
            RequestBoardServerRpc();
        }
    }

    // ================= PLAYER IDENTITY =================

    private static string LocalPlayerNameOrDefault(int fallbackNumber)
    {
        return PlayerSession.IsLoggedIn ? PlayerSession.PlayerName : $"Player {fallbackNumber}";
    }

    [ServerRpc(RequireOwnership = false)]
    private void SubmitNameServerRpc(string playerName, ServerRpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;
        if (string.IsNullOrWhiteSpace(playerName)) playerName = $"Player {sender + 1}";
        namesByClient[sender] = playerName.Trim();
    }

    // Database writes must never break gameplay: log a warning and continue.
    private static void TryDb(string operation, Action write)
    {
        try { write(); }
        catch (Exception e) { Debug.LogWarning($"[DB] Failed to {operation}: {e.Message}"); }
    }

    private void OnStateSynced() => RefreshUIFeedback();

    private void OnScoreSynced()
    {
        // A UI hiccup here must never propagate: this runs inside a NetworkVariable
        // callback, and an exception would abort whatever server flow triggered it.
        try { RefreshLocalScorePanel(); }
        catch (Exception e) { Debug.LogWarning($"[Score] panel refresh failed: {e.Message}"); }
        RefreshUIFeedback();
    }

    // ================= BOARD DISTRIBUTION =================

    [ServerRpc(RequireOwnership = false)]
    private void RequestBoardServerRpc(ServerRpcParams rpcParams = default)
    {
        EnsureBoardGenerated();

        ulong requester = rpcParams.Receive.SenderClientId;
        ClientRpcParams target = new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { requester } }
        };
        SendBoardClientRpc(cachedBoard, target);

        boardReady.Add(requester);
        TryStartGame();
    }

    [ClientRpc]
    private void SendBoardClientRpc(byte[] board, ClientRpcParams rpcParams = default)
    {
        BuildLocalBoards(board);
    }

    private void EnsureBoardGenerated()
    {
        if (boardGenerated) return;

        int seed = new System.Random().Next(1, int.MaxValue);
        BoardGenerator generator = new BoardGenerator(seed);
        CellData[,] data = generator.GenerateValidBoard();

        if (data == null)
        {
            Debug.LogError("[Network] Board generation failed on the server.");
            return;
        }

        cachedBoard = EncodeBoard(data);
        boardGenerated = true;
        Debug.Log($"[Network] Server generated board (seed {seed}).");

        // Persist the generated layout (server only reaches this method).
        TryDb("save board", () =>
        {
            var flat = new List<CellData>(BoardGenerator.ROWS * BoardGenerator.COLS);
            foreach (CellData cell in data) flat.Add(cell);
            dbBoardId = DatabaseManager.SaveBoard(flat);
            Debug.Log($"[DB] Board saved (board_id={dbBoardId}).");
        });
    }

    private byte[] EncodeBoard(CellData[,] data)
    {
        int rows = BoardGenerator.ROWS, cols = BoardGenerator.COLS;
        byte[] buffer = new byte[rows * cols];
        int i = 0;
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                int color = (int)data[r, c].Color & 0x0F;
                int star = data[r, c].HasStar ? 0x10 : 0;
                buffer[i++] = (byte)(color | star);
            }
        return buffer;
    }

    private CellData[,] DecodeBoard(byte[] buffer)
    {
        int rows = BoardGenerator.ROWS, cols = BoardGenerator.COLS;
        CellData[,] data = new CellData[rows, cols];
        int i = 0;
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                byte b = buffer[i++];
                CellData cell = new CellData((CellColor)(b & 0x0F), r, c);
                cell.HasStar = (b & 0x10) != 0;
                data[r, c] = cell;
            }
        return data;
    }

    private void BuildLocalBoards(byte[] board)
    {
        if (board == null || board.Length == 0)
        {
            Debug.LogError("[Network] No board data received; cannot build boards.");
            return;
        }

        Transform canvasTransform = canvasRectTransform != null ? canvasRectTransform.transform : null;
        if (canvasTransform == null)
        {
            Canvas found = FindAnyObjectByType<Canvas>();
            if (found != null) canvasTransform = found.transform;
        }
        if (canvasTransform == null)
        {
            Debug.LogError("[Network] No Canvas found to place the boards.");
            return;
        }

        myBoardInstance = CreateBoardInstance(canvasTransform, "MyBoard");
        myBoardInstance.BuildFromData(DecodeBoard(board));
        myBoardInstance.SetInteractable(true);

        opponentBoardInstance = CreateBoardInstance(canvasTransform, "OpponentBoard");
        opponentBoardInstance.BuildFromData(DecodeBoard(board));
        opponentBoardInstance.SetInteractable(false);
        opponentBoardInstance.gameObject.SetActive(false);

        viewingOpponent = false;
        Debug.Log("[Network] Local boards built. Showing my own board.");
    }

    private BoardManager CreateBoardInstance(Transform parent, string label)
    {
        GameObject go = Instantiate(networkBoardPrefab, parent, false);
        go.name = label;
        return go.GetComponent<BoardManager>();
    }

    public void ToggleOpponentView()
    {
        if (myBoardInstance == null || opponentBoardInstance == null)
        {
            Debug.LogWarning("[Network] Boards are not ready yet.");
            return;
        }

        viewingOpponent = !viewingOpponent;
        myBoardInstance.gameObject.SetActive(!viewingOpponent);
        opponentBoardInstance.gameObject.SetActive(viewingOpponent);

        Debug.Log(viewingOpponent
            ? "[Network] Now viewing the OPPONENT's board (read-only)."
            : "[Network] Now viewing MY OWN board.");
    }

    // ================= START ORCHESTRATION =================

    private void TryStartGame()
    {
        if (!IsServer || gameStarted) return;

        int expected = NetworkManager.ConnectedClientsList.Count;
        if (expected >= 2 && boardReady.Count >= expected)
        {
            gameStarted = true;
            AssignAndStart();
        }
    }

    private void AssignAndStart()
    {
        ulong hostId = NetworkManager.ServerClientId;
        ulong otherId = hostId;
        foreach (ulong id in NetworkManager.ConnectedClientsIds)
        {
            if (id != hostId) { otherId = id; break; }
        }

        // Resolve real names (submitted by each client at spawn) before assigning.
        string p1Name = namesByClient.TryGetValue(hostId, out string n1) ? n1 : "Player 1";
        string p2Name = namesByClient.TryGetValue(otherId, out string n2) ? n2 : "Player 2";
        player1.playerName = p1Name;
        player2.playerName = p2Name;

        ApplyPlayerAssignment(hostId, otherId);
        AssignPlayersClientRpc(hostId, otherId, p1Name, p2Name);

        roundStartingClientId = hostId;
        StartGame();
    }

    private void ApplyPlayerAssignment(ulong p1Id, ulong p2Id)
    {
        player1.clientId = p1Id;
        player2.clientId = p2Id;

        ulong myId = NetworkManager.Singleton.LocalClientId;
        ulong oppId = (p1Id == myId) ? p2Id : p1Id;

        boardsByClient.Clear();
        if (myBoardInstance != null) boardsByClient[myId] = myBoardInstance;
        if (opponentBoardInstance != null) boardsByClient[oppId] = opponentBoardInstance;

        player1Board = boardsByClient.ContainsKey(p1Id) ? boardsByClient[p1Id] : null;
        player2Board = boardsByClient.ContainsKey(p2Id) ? boardsByClient[p2Id] : null;
    }

    [ClientRpc]
    private void AssignPlayersClientRpc(ulong p1Id, ulong p2Id, string p1Name, string p2Name)
    {
        if (IsServer) return;
        player1.playerName = p1Name;
        player2.playerName = p2Name;
        ApplyPlayerAssignment(p1Id, p2Id);
        OnScoreSynced();
    }

    private void StartGame()
    {
        if (!IsServer) return;

        gameOver = false;
        netP1Init.Value = 0;
        netP2Init.Value = 0;
        netComparing.Value = false;
        netRound.Value = 0;
        netDiceRolled.Value = false;
        SvClearActiveDice();

        ResetDiceClientRpc();
        SetColorDiceActiveClientRpc(false);

        // If the InitialFight scene already decided who starts, skip the in-match
        // initiative phase entirely and begin the first round with that winner.
        if (InitiativeResult.HasResult)
        {
            ulong winner = InitiativeResult.Consume();
            bool winnerConnected = winner == player1.clientId || winner == player2.clientId;
            if (winnerConnected)
            {
                PushScoreStates();
                ServerBeginMatch(winner);
                return;
            }
            Debug.LogWarning("[Network] InitiativeResult winner is not a connected player; falling back to the in-match initiative phase.");
        }

        // Set the actual game state FIRST so that, even if a downstream UI refresh has a
        // problem, the game has already started (state = Initiative) rather than aborting.
        netActiveClientId.Value = player1.clientId;
        netState.Value = (int)GameState.Initiative;
        SvSetMessage("Start! Player 1, roll the numeric dice for initiative.");

        PushScoreStates();
    }

    // ================= DICE ROLLING =================

    public void OnRollButtonPressed()
    {
        if (!IsMyTurn) return;
        RequestRollServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestRollServerRpc(ServerRpcParams rpcParams = default)
    {
        if (gameOver) return;

        ulong sender = rpcParams.Receive.SenderClientId;
        if (sender != netActiveClientId.Value) return;

        GameState s = (GameState)netState.Value;

        if (s == GameState.Initiative)
        {
            int[] n = ServerRollNumeric();
            int total = n[0] + n[1] + n[2];
            ApplyRollClientRpc(n, new int[3], false);
            HandleInitiativeRoll(total);
        }
        else if (s == GameState.ActivePlayerTurn)
        {
            if (netDiceRolled.Value)
            {
                SvSetMessage("You already rolled the dice this round!");
                return;
            }

            int[] n = ServerRollNumeric();
            int[] c = ServerRollColor();
            netDiceRolled.Value = true;
            ApplyRollClientRpc(n, c, true);
            SvSetMessage("Dice rolled. Choose your number and color, then your cells.");
        }
        else if (s == GameState.PassivePlayerTurn)
        {
            SvSetMessage("Passive turn: pick from the leftover dice, you cannot roll.");
        }
    }

    private int[] ServerRollNumeric()
    {
        int[] a = new int[3];
        for (int i = 0; i < 3; i++) a[i] = serverRng.Next(1, 7);
        return a;
    }

    private int[] ServerRollColor()
    {
        int[] a = new int[3];
        for (int i = 0; i < 3; i++) a[i] = serverRng.Next(0, 6);
        return a;
    }

    [ClientRpc]
    private void ApplyRollClientRpc(int[] numericValues, int[] colorFaces, bool colorActive)
    {
        if (diceManager != null) diceManager.ApplyRoll(numericValues, colorFaces, colorActive);
    }

    // ================= INITIATIVE =================

    private void HandleInitiativeRoll(int total)
    {
        if (ActivePlayerNumber == 1)
        {
            netP1Init.Value = total;
            SvSetMessage($"Player 1 rolled {total} for initiative. Player 2, roll now.");
            netActiveClientId.Value = player2.clientId;
        }
        else
        {
            netP2Init.Value = total;
            SvSetMessage($"Player 2 rolled {total}. Comparing initiative...");
            StartCoroutine(WaitAndCompareInitiative());
        }
    }

    private IEnumerator WaitAndCompareInitiative()
    {
        netComparing.Value = true;
        int countdown = 3;
        while (countdown > 0)
        {
            SvSetMessage($"Comparing initiative... {countdown}");
            yield return new WaitForSeconds(1f);
            countdown--;
        }
        netComparing.Value = false;
        DetermineInitiativeWinner();
    }

    private void DetermineInitiativeWinner()
    {
        int p1 = netP1Init.Value;
        int p2 = netP2Init.Value;

        if (p1 == p2)
        {
            netP1Init.Value = 0;
            netP2Init.Value = 0;
            netActiveClientId.Value = player1.clientId;
            netState.Value = (int)GameState.Initiative;
            SvSetMessage("Initiative tie! Player 1, roll again.");
            return;
        }

        ulong winner = p1 > p2 ? player1.clientId : player2.clientId;
        ServerBeginMatch(winner);
    }

    // Starts round 1 with the given initiative winner. Called either from the
    // in-match initiative phase above, or directly from StartGame when the
    // InitialFight scene already decided the winner.
    private void ServerBeginMatch(ulong winner)
    {
        netActiveClientId.Value = winner;
        roundStartingClientId = winner;
        netState.Value = (int)GameState.ActivePlayerTurn;
        netRound.Value = 1;
        netDiceRolled.Value = false;
        SvClearActiveDice();

        // Initiative is decided: create the match record (turn_order 1 = initiative winner).
        TryDb("create match", () =>
        {
            if (dbBoardId < 0 || dbMatchId >= 0) return;

            ulong loser = (winner == player1.clientId) ? player2.clientId : player1.clientId;
            string winnerName = (winner == player1.clientId) ? player1.playerName : player2.playerName;
            string loserName = (winner == player1.clientId) ? player2.playerName : player1.playerName;

            long winnerDbId = DatabaseManager.GetOrCreatePlayer(winnerName);
            long loserDbId = DatabaseManager.GetOrCreatePlayer(loserName);
            if (loserDbId == winnerDbId) // both used the same name: keep rows distinct
                loserDbId = DatabaseManager.GetOrCreatePlayer(loserName + " (2)");

            var (matchId, matchPlayerIds) = DatabaseManager.CreateMatch(
                dbBoardId, new List<long> { winnerDbId, loserDbId });

            dbMatchId = matchId;
            dbMatchPlayerIds[winner] = matchPlayerIds[0];
            dbMatchPlayerIds[loser] = matchPlayerIds[1];
            dbTurnNumber = 0;
            Debug.Log($"[DB] Match created (match_id={dbMatchId}).");
        });

        ResetDiceClientRpc();
        SetColorDiceActiveClientRpc(true);
        SvSetMessage($"Player {ActivePlayerNumber} wins initiative and starts!");
    }

    // ================= COMMIT A SELECTION =================

    public void CommitSelection(int[] cellIndices, int number, int colorInt, bool numWild, bool colWild, int numIndex, int colorIndex)
    {
        CommitSelectionServerRpc(cellIndices, number, colorInt, numWild, colWild, numIndex, colorIndex);
    }

    [ServerRpc(RequireOwnership = false)]
    private void CommitSelectionServerRpc(int[] cells, int number, int colorInt, bool numWild, bool colWild, int numIndex, int colorIndex, ServerRpcParams rpcParams = default)
    {
        if (gameOver) return;

        ulong sender = rpcParams.Receive.SenderClientId;
        if (sender != netActiveClientId.Value) return;
        if (!boardsByClient.ContainsKey(sender)) return;

        PlayerData actor = (sender == player1.clientId) ? player1 : player2;
        BoardManager board = boardsByClient[sender];

        foreach (int idx in cells) board.MarkCellByIndex(idx);

        foreach (int idx in cells)
        {
            int r = idx / BoardGenerator.COLS;
            int c = idx % BoardGenerator.COLS;
            if (board.BoardData[r, c].HasStar) actor.totalStarsCollected++;
        }

        int wc = 0;
        if (numWild) wc++;
        if (colWild) wc++;
        if (wc > 0) actor.wildcardsRemaining -= wc;

        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.ProcessTurnScoring(actor, board);
        }

        // Log the confirmed move (dice choice + marked cells) for history/replay.
        TryDb("record move", () =>
        {
            if (dbMatchId < 0 || !dbMatchPlayerIds.ContainsKey(sender)) return;
            dbTurnNumber++;

            var marked = new List<(int row, int col)>(cells.Length);
            foreach (int idx in cells)
                marked.Add((idx / BoardGenerator.COLS, idx % BoardGenerator.COLS));

            DatabaseManager.RecordMove(
                dbMatchId, dbMatchPlayerIds[sender], dbTurnNumber,
                (GameState)netState.Value == GameState.ActivePlayerTurn,
                numWild ? 6 : number,
                colWild ? CellColor.Black : (CellColor)colorInt,
                numWild || colWild,
                marked);
        });

        ApplyMarksClientRpc(sender, cells);
        LockDiceClientRpc(numIndex, colorIndex);

        // Clear the live preview highlights and dice pick now that the move is committed.
        SendPreviewCells(sender, EmptyCells);
        SvClearActiveDice();

        PushScoreStates();

        if (gameOver) return;

        ServerEndTurn();
    }

    [ClientRpc]
    private void ApplyMarksClientRpc(ulong actingClientId, int[] cells)
    {
        if (!boardsByClient.ContainsKey(actingClientId)) return;
        BoardManager board = boardsByClient[actingClientId];
        foreach (int idx in cells) board.MarkCellByIndex(idx);
    }

    [ClientRpc]
    private void LockDiceClientRpc(int numIndex, int colorIndex)
    {
        if (DiceButtonUI.Instance != null) DiceButtonUI.Instance.LockByIndex(numIndex, colorIndex);
    }

    [ClientRpc]
    private void ResetDiceClientRpc()
    {
        if (diceManager != null)
        {
            diceManager.ResetAllDice();
            diceManager.ResetDiceState();
        }
        if (selectionManager != null) selectionManager.ResetDiceSelectionState();
    }

    [ClientRpc]
    private void SetColorDiceActiveClientRpc(bool active)
    {
        if (diceManager != null) diceManager.SetColorDiceActive(active);
    }

    // ================= LIVE PREVIEW (opponent pick display) =================

    // Called locally by SelectionManager whenever the acting player's in-progress
    // selection changes (die chosen, or a cell added/removed).
    public void BroadcastPreview(int[] cells, int number, int colorInt)
    {
        if (!IsMyTurn) return;
        PreviewServerRpc(cells, number, colorInt);
    }

    [ServerRpc(RequireOwnership = false)]
    private void PreviewServerRpc(int[] cells, int number, int colorInt, ServerRpcParams rpcParams = default)
    {
        if (gameOver) return;
        ulong sender = rpcParams.Receive.SenderClientId;
        if (sender != netActiveClientId.Value) return;

        netActiveNumber.Value = number;
        netActiveColor.Value = colorInt;
        SendPreviewCells(sender, cells);
    }

    // Send the acting player's highlighted cells to everyone EXCEPT the acting player
    // (they already see their own highlights locally).
    private void SendPreviewCells(ulong actingId, int[] cells)
    {
        if (!IsServer) return;

        List<ulong> targets = new List<ulong>();
        foreach (ulong id in NetworkManager.ConnectedClientsIds)
            if (id != actingId) targets.Add(id);

        if (targets.Count == 0) return;

        ClientRpcParams p = new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = targets.ToArray() }
        };
        PreviewCellsClientRpc(actingId, cells, p);
    }

    [ClientRpc]
    private void PreviewCellsClientRpc(ulong actingClientId, int[] cells, ClientRpcParams rpcParams = default)
    {
        if (!boardsByClient.ContainsKey(actingClientId)) return;
        boardsByClient[actingClientId].SetPreviewSelection(cells);
    }

    // ================= TURN ADVANCE (server) =================

    private void ServerEndTurn()
    {
        ulong finishing = netActiveClientId.Value;
        GameState s = (GameState)netState.Value;

        if (s == GameState.ActivePlayerTurn)
        {
            netState.Value = (int)GameState.PassivePlayerTurn;
            ulong other = (netActiveClientId.Value == player1.clientId) ? player2.clientId : player1.clientId;
            netActiveClientId.Value = other;
            SvSetMessage("Passive turn: pick from the leftover dice.");
        }
        else if (s == GameState.PassivePlayerTurn)
        {
            ServerStartNewRound();
        }

        SendPreviewCells(finishing, EmptyCells);
        SvClearActiveDice();
    }

    private void ServerStartNewRound()
    {
        netRound.Value = netRound.Value + 1;

        roundStartingClientId = (roundStartingClientId == player1.clientId) ? player2.clientId : player1.clientId;
        netActiveClientId.Value = roundStartingClientId;

        netState.Value = (int)GameState.ActivePlayerTurn;
        netDiceRolled.Value = false;

        ResetDiceClientRpc();
        SetColorDiceActiveClientRpc(true);
        SvSetMessage($"Round {netRound.Value} begins.");
    }

    // ================= PASS =================

    public void PassTurn()
    {
        if (!IsMyTurn) return;
        if (selectionManager != null) selectionManager.CancelSelection();
        RequestPassServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestPassServerRpc(ServerRpcParams rpcParams = default)
    {
        if (gameOver) return;
        ulong sender = rpcParams.Receive.SenderClientId;
        if (sender != netActiveClientId.Value) return;
        ServerEndTurn();
    }

    // ================= CLICK VALIDATION =================

    public bool IsCellClickValid(BoardManager boardClicked)
    {
        if (CurrentState == GameState.Initiative || CurrentState == GameState.Idle) return false;

        if (CurrentActivePlayer.clientId != NetworkManager.Singleton.LocalClientId)
        {
            Debug.LogWarning("[Network] You cannot play, it is not your turn!");
            return false;
        }

        if (boardClicked != myBoardInstance)
        {
            Debug.LogWarning("[Network] You can only click cells on your own board!");
            return false;
        }

        return true;
    }

    // ================= SCORE SYNC =================

    private void PushScoreStates()
    {
        if (!IsServer) return;
        netP1ScoreState.Value = BuildScoreSync(player1, player1Board);
        netP2ScoreState.Value = BuildScoreSync(player2, player2Board);
    }

    private PlayerScoreSync BuildScoreSync(PlayerData p, BoardManager board)
    {
        int total = (ScoreManager.Instance != null) ? ScoreManager.Instance.CalculateTotalScore(p, board) : 0;

        int rowsPacked = 0;
        for (int i = 0; i < BoardGenerator.ROWS; i++)
            rowsPacked |= ((int)p.completedRows[i] & 3) << (2 * i);

        int colorsPacked = 0;
        int ci = 0;
        foreach (CellColor col in Enum.GetValues(typeof(CellColor)))
        {
            if (col == CellColor.Black) continue;
            CompletionOrder o = p.completedColors.ContainsKey(col) ? p.completedColors[col] : CompletionOrder.None;
            colorsPacked |= ((int)o & 3) << (2 * ci);
            ci++;
        }

        return new PlayerScoreSync
        {
            score = total,
            wildcards = p.wildcardsRemaining,
            stars = p.totalStarsCollected,
            rowsPacked = rowsPacked,
            colorsPacked = colorsPacked
        };
    }

    // On clients, rebuild the local PlayerData object from the synced snapshot so the
    // existing ScoreUI can render it unchanged.
    private void ApplyScoreState(PlayerData p, PlayerScoreSync s)
    {
        p.wildcardsRemaining = s.wildcards;
        p.totalStarsCollected = s.stars;

        for (int i = 0; i < BoardGenerator.ROWS; i++)
            p.completedRows[i] = (CompletionOrder)((s.rowsPacked >> (2 * i)) & 3);

        int ci = 0;
        foreach (CellColor col in Enum.GetValues(typeof(CellColor)))
        {
            if (col == CellColor.Black) continue;
            p.completedColors[col] = (CompletionOrder)((s.colorsPacked >> (2 * ci)) & 3);
            ci++;
        }
    }

    private void RefreshLocalScorePanel()
    {
        if (NetworkManager.Singleton == null) return;

        ulong myId = NetworkManager.Singleton.LocalClientId;
        PlayerData local = (myId == player1.clientId) ? player1 : player2;
        BoardManager localBoard = boardsByClient.ContainsKey(myId) ? boardsByClient[myId] : myBoardInstance;
        if (local == null || localBoard == null) return;

        // Rebuild the score breakdown so player.score is correct on this machine.
        if (ScoreManager.Instance != null) ScoreManager.Instance.CalculateTotalScore(local, localBoard);

        // The scene may contain more than one ScoreUI (the standalone panel AND one
        // inside the BoardPlayerPanel prefab). Update every one with THIS machine's
        // local player, so whichever panel is on screen always shows "you".
        // ScoreUI is fully null-guarded, so an unwired panel is simply skipped.
        ScoreUI[] panels = FindObjectsByType<ScoreUI>(FindObjectsSortMode.None);
        foreach (ScoreUI panel in panels)
        {
            if (panel != null) panel.UpdateUI(local, localBoard);
        }
    }

    // ================= WIN (log only for now) =================

    public void Restart()
    {
        Debug.Log("[Network] Online restart is not implemented yet.");
    }

    public void EndGame()
    {
        gameOver = true;

        int s1 = ScoreManager.Instance != null ? ScoreManager.Instance.CalculateTotalScore(player1, player1Board) : 0;
        int s2 = ScoreManager.Instance != null ? ScoreManager.Instance.CalculateTotalScore(player2, player2Board) : 0;

        string result;
        if (s1 > s2) result = $"Game Over! Player 1 ({player1.playerName}) wins {s1} - {s2}.";
        else if (s2 > s1) result = $"Game Over! Player 2 ({player2.playerName}) wins {s2} - {s1}.";
        else result = $"Game Over! It's a draw at {s1} - {s2}.";

        netState.Value = (int)GameState.Idle;
        SvClearActiveDice();
        SvSetMessage(result);
        PushScoreStates();
        Debug.Log(result);

        LogPlayerScore(player1);
        LogPlayerScore(player2);

        // Persist final results: scores, row/color breakdowns, winner, leaderboard.
        if (IsServer)
        {
            TryDb("finish match", () =>
            {
                if (dbMatchId < 0) return;

                var results = new Dictionary<long, PlayerData>();
                if (dbMatchPlayerIds.TryGetValue(player1.clientId, out long mp1)) results[mp1] = player1;
                if (dbMatchPlayerIds.TryGetValue(player2.clientId, out long mp2)) results[mp2] = player2;

                DatabaseManager.FinishMatch(dbMatchId, results);
                Debug.Log($"[DB] Match {dbMatchId} finished and saved.");
                dbMatchId = -1;
            });
        }
    }

    // ================= UI FEEDBACK =================

    private string GetActionInstruction(GameState s)
    {
        switch (s)
        {
            case GameState.Initiative: return "Roll numeric dice to determine turn order.";
            case GameState.ActivePlayerTurn: return "Select one numeric and one color die to mark cells.";
            case GameState.PassivePlayerTurn: return "Pick from the leftover dice to mark cells.";
            default: return "Press Start to begin.";
        }
    }

    // Perspective-aware status text, so each owner sees "YOUR turn" vs "opponent's turn".
    private string BuildLocalStatusMessage()
    {
        if (netComparing.Value) return netMessage.Value.ToString();

        GameState s = CurrentState;
        bool mine = IsMyTurn;

        switch (s)
        {
            case GameState.Initiative:
                return mine
                    ? "<color=#7CFC00><b>YOUR turn</b></color> — roll the numeric dice for initiative."
                    : "<color=#AAAAAA>Waiting for the opponent's initiative roll…</color>";

            case GameState.ActivePlayerTurn:
                if (!netDiceRolled.Value)
                    return mine
                        ? "<color=#7CFC00><b>YOUR turn</b></color> — roll the dice."
                        : "<color=#AAAAAA>Opponent's turn — waiting for their roll…</color>";
                return mine
                    ? "<color=#7CFC00><b>YOUR turn</b></color> — pick one number + one color die, choose your cells, then Confirm."
                    : "<color=#AAAAAA>Opponent rolled and is choosing their move…</color>";

            case GameState.PassivePlayerTurn:
                return mine
                    ? "<color=#7CFC00><b>YOUR passive turn</b></color> — pick from the leftover dice."
                    : "<color=#AAAAAA>Opponent is taking their passive turn…</color>";

            default:
                return netMessage.Value.ToString();
        }
    }

    public void RefreshUIFeedback()
    {
        if (debugUI == null) return;

        GameState s = (GameState)netState.Value;

        string info = BuildLocalStatusMessage()
                      + $"\n<b>Scores</b> — P1: {netP1ScoreState.Value.score}   P2: {netP2ScoreState.Value.score}";

        string activeBoardName = (ActivePlayerNumber == 1)
            ? (player1Board != null ? player1Board.name : "P1")
            : (player2Board != null ? player2Board.name : "P2");

        string colorName = netActiveColor.Value >= 0 ? ((CellColor)netActiveColor.Value).ToString() : "None";

        debugUI.Refresh(
            netRound.Value,
            s.ToString(),
            info,
            ActivePlayerNumber,
            netDiceRolled.Value,
            player1Board != null ? player1Board.GetMarkedCellsCount() : 0,
            player2Board != null ? player2Board.GetMarkedCellsCount() : 0,
            GetActionInstruction(s),
            netP1Init.Value,
            netP2Init.Value,
            s == GameState.Initiative,
            netActiveNumber.Value,
            colorName,
            activeBoardName
        );
    }

    // ================= SERVER HELPERS =================

    private void SvClearActiveDice()
    {
        if (!IsServer) return;
        netActiveNumber.Value = 0;
        netActiveColor.Value = -1;
    }

    private void SvSetMessage(string m)
    {
        if (!IsServer) return;
        if (m == null) m = "";
        if (m.Length > 400) m = m.Substring(0, 400);
        netMessage.Value = new FixedString512Bytes(m);
    }

    private void LogPlayerScore(PlayerData player)
    {
        ScoreBreakdown bd = player.scoreBreakdown;

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("");
        sb.AppendLine("=================================");
        sb.AppendLine($"           {player.playerName}");
        sb.AppendLine("=================================");
        sb.AppendLine("");
        sb.AppendLine("ROWS:");

        int rowsTotal = 0;
        foreach (var row in bd.rows)
        {
            sb.AppendLine($"  Row {row.rowIndex + 1,-2} | {row.order,-6} | +{row.points}");
            rowsTotal += row.points;
        }
        sb.AppendLine($"  Rows Total: +{rowsTotal}");

        sb.AppendLine("");
        sb.AppendLine("COLORS:");

        int colorsTotal = 0;
        foreach (var color in bd.colors)
        {
            sb.AppendLine($"  {color.color,-10} | {color.order,-6} | +{color.points}");
            colorsTotal += color.points;
        }
        sb.AppendLine($"  Colors Total: +{colorsTotal}");

        int starPenalty = bd.unmarkedStars * ScoreConfig.PenaltyPerUnmarkedStar;
        int wildcardBonus = bd.unusedWildcards * ScoreConfig.RewardPerUnusedWildcard;

        sb.AppendLine("");
        sb.AppendLine("STARS:");
        sb.AppendLine($"  Unmarked: {bd.unmarkedStars}");
        sb.AppendLine($"  Penalty: {starPenalty}");

        sb.AppendLine("");
        sb.AppendLine("WILDCARDS:");
        sb.AppendLine($"  Remaining: {bd.unusedWildcards}");
        sb.AppendLine($"  Bonus: +{wildcardBonus}");

        sb.AppendLine("");
        sb.AppendLine("---------------------------------");
        sb.AppendLine($"TOTAL SCORE: {bd.Total}");
        sb.AppendLine("=================================");

        Debug.Log(sb.ToString());
    }
}