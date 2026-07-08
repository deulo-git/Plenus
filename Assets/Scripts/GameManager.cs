using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using static UnityEditor.Experimental.GraphView.GraphView;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public enum GameState { Idle, Initiative, RollingNumeric, ActivePlayerTurn, PassivePlayerTurn }
    public GameState CurrentState { get; private set; } = GameState.Idle;
    public int currentRound = 0;
    public string extraUIInfo = "";
    [Header("Player Data")]
    public PlayerData player1 = new PlayerData(1,"Sergi");
    public PlayerData player2 = new PlayerData(2, "Ari");



    [Header("Managers & Boards")]
    public BoardManager player1Board;
    public BoardManager player2Board;
    public DiceManager diceManager;
    public SelectionManager selectionManager;

    [Header("Initiative Tracking")]
    private int p1InitiativeScore = 0;
    private int p2InitiativeScore = 0;
    public bool isComparingInitiative = false;

    [Header("Debug")]
    [SerializeField] private GameDebugUI debugUI;

    [Header("Game State")]
    public PlayerData CurrentRoundStartingPlayer { get; private set; }
    public PlayerData CurrentActivePlayer { get; private set;}

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
        CurrentActivePlayer = player1;
    }

    public void StartGame()
    {
        player1Board.GenerateBoard();
        player2Board.ShareDataWith(player1Board.BoardData);

        CurrentState = GameState.Initiative;
        diceManager.SetColorDiceActive(false);

        extraUIInfo = "Start! Player 1, roll numeric dice for initiative.";
        RefreshUIFeedback();
        UpdatePlayersUI();
    }

    // --- NOU: Aquest mètode centralitza el clic de rodatge ---
    public void ProcessRollAction()
    {
        if (CurrentState == GameState.Initiative)
        {
            int totalScore = diceManager.RollNumericDiceOnly();
            HandleInitiativeRoll(totalScore);
        }
        else if (CurrentState == GameState.ActivePlayerTurn)
        {
            // NEW: Prevent rolling if dice are already rolled this round
            if (diceManager.AreDiceRolled)
            {
                extraUIInfo = "<color=red>You already rolled the dice this round!</color>";
                RefreshUIFeedback();
                return;
            }

            diceManager.RollAllDice();
            extraUIInfo = $"Player {CurrentActivePlayer.playerID} rolled the dice. Select one number and one color.";
            RefreshUIFeedback();
        }
        else if (CurrentState == GameState.PassivePlayerTurn)
        {
            // NEW: Prevent rolling on passive turn
            extraUIInfo = "<color=orange>It's your passive turn! Choose from the remaining dice, you cannot roll.</color>";
            RefreshUIFeedback();
        }
    }

    private void HandleInitiativeRoll(int totalScore)
    {
        if (CurrentActivePlayer.playerID == 1)
        {
            p1InitiativeScore = totalScore;
            RefreshUIFeedback();
            CurrentActivePlayer = player2;
        }
        else
        {
            p2InitiativeScore = totalScore;
            RefreshUIFeedback();
            DiceManager.Instance.SetDiceInteractable(false);
            StartCoroutine(WaitAndCompareInitiative());
        }
    }

    private void ResetDice()
    {
        diceManager.ResetAllDice();
        diceManager.ResetDiceState();
    }

    private void SetCurrentBoard()
    {
        selectionManager.SetActiveBoard(
            CurrentActivePlayer.playerID == 1
                ? player1Board
                : player2Board);
    }

    private IEnumerator WaitAndCompareInitiative()
    {
        isComparingInitiative = true;

        int countdown = 3;
        while (countdown > 0)
        {
            extraUIInfo = $"Player 2 rolled a {p2InitiativeScore}. Comparing results... {countdown}";
            RefreshUIFeedback();
            yield return new WaitForSeconds(1f);
            countdown--;
        }

        extraUIInfo = "START!";
        yield return new WaitForSeconds(0.75f);

        isComparingInitiative = false;
        DetermineInitiativeWinner();
        RefreshUIFeedback();
    }

    public void DetermineInitiativeWinner()
    {
        if (p1InitiativeScore > p2InitiativeScore)
        {
            CurrentActivePlayer = player1;
            extraUIInfo = $"Player 1 starts! (P1: {p1InitiativeScore} vs P2: {p2InitiativeScore})";
        }
        else if (p2InitiativeScore > p1InitiativeScore)
        {
            CurrentActivePlayer = player2;
            extraUIInfo = $"Player 2 starts! (P2: {p2InitiativeScore} vs P1: {p1InitiativeScore})";
        }
        else
        {
            extraUIInfo = "It's a tie! Roll again.";
            p1InitiativeScore = 0;
            p2InitiativeScore = 0;
            CurrentActivePlayer = player1;
            return;
        }
        RefreshUIFeedback();
        CurrentState = GameState.ActivePlayerTurn;
        CurrentRoundStartingPlayer = CurrentActivePlayer;
        diceManager.SetColorDiceActive(true);
        ResetDice();
        currentRound = 1; // Start round 1
    }

    public void StartNewRound()
    {
        SetCurrentBoard();
        currentRound++; // NEW: Increment round counter
        CurrentState = GameState.ActivePlayerTurn;

        // Alternate the starting player for the new round
        if (CurrentRoundStartingPlayer == player1)
        {
            CurrentRoundStartingPlayer = player2;
        }
        else
        {
            CurrentRoundStartingPlayer = player1;
        }

        // Set the active player to the new starting player
        CurrentActivePlayer = CurrentRoundStartingPlayer;

        ResetDice();

        extraUIInfo = $"Round {currentRound} starts! Player {CurrentActivePlayer.playerID}, please roll the dice.";
        RefreshUIFeedback();

    }

    public void EndTurn()
    {
        if (CurrentState == GameState.ActivePlayerTurn)
        {
            CurrentState = GameState.PassivePlayerTurn;
            if (CurrentActivePlayer.playerID == 1)
            {
                selectionManager.SetActiveBoard(player1Board);
                CurrentActivePlayer = player2;
            }
            else
            {
                selectionManager.SetActiveBoard(player2Board);
                CurrentActivePlayer = player1;
            }                        

            diceManager.LockUsedDice();
            selectionManager.CancelSelection();
            selectionManager.ResetDiceSelection();
            RefreshUIFeedback();
        }
        else if (CurrentState == GameState.PassivePlayerTurn)
        {
            selectionManager.CancelSelection();
            StartNewRound();
            SetCurrentBoard();
            diceManager.SetDiceInteractable(false);
        }
        UpdatePlayersUI();

    }

    public bool IsCellClickValid(BoardManager boardClicked)
    {
        if (CurrentState == GameState.Initiative) return false;

        if (CurrentActivePlayer.playerID == 1 && boardClicked == player1Board) return true;
        if (CurrentActivePlayer.playerID == 2 && boardClicked == player2Board) return true;

        return false;
    }

    private string GetActionInstruction()
    {
        switch (CurrentState)
        {
            case GameState.Initiative: return "Roll numeric dice to determine turn order.";
            case GameState.ActivePlayerTurn: return "Select one numeric and one color die to mark cells.";
            case GameState.PassivePlayerTurn: return "Waiting for opponent to finish turn.";
            default: return "Press Start to begin.";
        }
    }

    public void RefreshUIFeedback()
    {
        if (isComparingInitiative) return;

        debugUI.Refresh(
             currentRound,
             CurrentState.ToString(),
             extraUIInfo,
             CurrentActivePlayer.playerID,
             diceManager.AreDiceRolled,
             player1Board.GetMarkedCellsCount(),
             player2Board.GetMarkedCellsCount(),
             GetActionInstruction(),
             p1InitiativeScore,
             p2InitiativeScore,
             CurrentState == GameState.Initiative,
             selectionManager.ActiveNumber ?? 0,
             selectionManager.ActiveColor?.ToString() ?? "None",
             CurrentActivePlayer.playerID == 1
                 ? player1Board.name
                 : player2Board.name
         );
    }

    public void Restart()
    {
        p1InitiativeScore = 0;
        p2InitiativeScore = 0;
        CurrentState = GameState.Initiative;
        currentRound = 0;
        extraUIInfo = "Game restarted! Player 1, roll numeric dice for initiative.";
        ResetDice();
        player1Board.GenerateBoard();
        player2Board.ShareDataWith(player1Board.BoardData);
        RefreshUIFeedback();
        player1.ResetScoreData();
        player2.ResetScoreData();
    }

    public void PassTurn()
    {
        
        if (CurrentState == GameState.ActivePlayerTurn || CurrentState == GameState.PassivePlayerTurn)
        {
            foreach (var cell in selectionManager.currentCellsSelection)
            {
                cell.LogicData.IsMarked = false;
                cell.SetSelectedVisual(false);
            }

            selectionManager.ResetDiceSelectionState();
            diceManager.ResetAllDice(); 
            EndTurn();
        }
    }

    // Check if the current player has enough wildcards to make the move
    public bool HasEnoughWildcards(int requiredAmount)
    {
        return CurrentActivePlayer.wildcardsRemaining >= requiredAmount;
    }

    // Consume the wildcards and update UI
    public void ConsumeWildcards(int amount)
    {
        CurrentActivePlayer.wildcardsRemaining -= amount;
    }

    public void UpdatePlayersUI()
    {         
        player1.uiReference.UpdateUI(player1,player1Board);
        player2.uiReference.UpdateUI(player2,player2Board);
    }

    internal void ManageMarkedStars(List<CellView> currentCellsSelection, PlayerData currentActivePlayer)
    {
        foreach (var cell in currentCellsSelection)
        {
            if (cell.LogicData.IsMarked && cell.LogicData.HasStar)
            {
                currentActivePlayer.totalStarsCollected++;
            }
        }
    }

    public void EndGame()
    {
       
        ScoreManager.Instance.CalculateTotalScore(player1, player1Board);
        ScoreManager.Instance.CalculateTotalScore(player2, player2Board);

        PlayerData winner;

        if (player1.score > player2.score)
        {
            winner = player1;
        }
        else if (player2.score > player1.score)
        {
            winner = player2;
        }
        else
        {
            // Tie handling
            winner = null;
        }


        if (winner != null)
        {
            Debug.Log($"Game Over! Winner: {winner.playerName} ({winner.score} points)");
        }
        else
        {
            Debug.Log($"Game Over! Draw! Score: {player1.score}");
        }


        LogPlayerScore(player1);
        LogPlayerScore(player2);
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
            sb.AppendLine(
                $"  Row {row.rowIndex + 1,-2} | {row.order,-6} | +{row.points}"
            );

            rowsTotal += row.points;
        }

        sb.AppendLine($"  Rows Total: +{rowsTotal}");


        sb.AppendLine("");
        sb.AppendLine("COLORS:");

        int colorsTotal = 0;

        foreach (var color in bd.colors)
        {
            sb.AppendLine(
                $"  {color.color,-10} | {color.order,-6} | +{color.points}"
            );

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