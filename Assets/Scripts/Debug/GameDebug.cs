using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SocialPlatforms.Impl;
using UnityEngine.UI;

public class GameDebugUI : MonoBehaviour
{
    [Header("Status")]
    [SerializeField] private TextMeshProUGUI gameStatusText;

    [Header("Player Panels")]
    [SerializeField] private Image p1Panel;
    [SerializeField] private Image p2Panel;

    [SerializeField] private Color defaultPanelColor;
    [SerializeField] private Color currentPanelColor;

    private ScoreUI scoreUI;

    public void Refresh(
        int currentRound,
        string state,
        string extraInfo,
        int activePlayer,
        bool diceRolled,
        int markedP1,
        int markedP2,
        string action,
        int initiativeP1,
        int initiativeP2,
        bool initiativePhase,
        int lastNumericDice,
        string lastColorDice,
        string currentBoard)
    {
        StringBuilder sb = new();

        sb.AppendLine($"<size=120%><b>GAME ROUND: {currentRound}</b></size>");
        sb.AppendLine($"Status: <color=yellow>{state}</color>");
        sb.AppendLine("-----------------------------------");

        sb.AppendLine(extraInfo);

        if (initiativePhase)
        {
            sb.AppendLine("<b>INITIATIVE PHASE</b>");
            sb.AppendLine($"Player 1 Score: <color=cyan>{initiativeP1}</color>");
            sb.AppendLine($"Player 2 Score: <color=cyan>{initiativeP2}</color>");
        }
        else if (diceRolled)
        {
            sb.AppendLine($"Active Player: <b>{activePlayer}</b>");
            sb.AppendLine($"Current Board: {currentBoard}");

            sb.AppendLine();
            sb.AppendLine("<b>Active Dice:</b>");
            sb.AppendLine($"- Number: <color=cyan>{lastNumericDice}</color>");
            sb.AppendLine($"- Color: <color=yellow>{lastColorDice}</color>");
        }
        else
        {
            sb.AppendLine();
            sb.AppendLine("<i>Waiting for dice roll...</i>");
        }

        sb.AppendLine();
        sb.AppendLine("<b>Action:</b>");
        sb.AppendLine(action);

        gameStatusText.text = sb.ToString();

        p1Panel.color = activePlayer == 1 ? currentPanelColor : defaultPanelColor;
        p2Panel.color = activePlayer == 2 ? currentPanelColor : defaultPanelColor;

    }

}