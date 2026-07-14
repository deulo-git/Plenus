using Assets.Scripts;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static PlayerData;

public class ScoreUI : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private TextMeshProUGUI totalScoreText;
    [SerializeField] private TextMeshProUGUI completedRowsText;
    [SerializeField] private TextMeshProUGUI playerNameText;
    [SerializeField] private TextMeshProUGUI totalStarsText;
    [SerializeField] private TextMeshProUGUI totalMarkedCellsText;
    [SerializeField] private ColorPalette colorPalette;

    [Header("Wildcards")]
    [SerializeField] private GameObject wildcardContainer; // El GridLayoutGroup
    [SerializeField] private GameObject wildcardPrefab;   // Prefab d'una imatge de comodí

    [Header("Color Completion")]
    [SerializeField] private RawImage[] colorStatusImages; // Assigna les 2 RawImages aquí

    private List<GameObject> activeWildcardIcons = new List<GameObject>();

    // Crida aquest mètode cada cop que el jugador fa un moviment.
    // Every serialized reference is null-guarded: this panel is now driven from the
    // network layer, and a missing Inspector slot must never throw (that would abort
    // the game-start RPC on the host).
    public void UpdateUI(PlayerData player, BoardManager playerBoard)
    {
        if (player == null) return;

        if (playerNameText != null)
            playerNameText.text = player.playerName;

        if (totalScoreText != null)
            totalScoreText.text = $"Score: {player.score}";

        UpdateWildcards(player.wildcardsRemaining, player);
        UpdateRowsText(player);
        UpdateColorStatus(player);

        int playableColors = System.Enum.GetValues(typeof(CellColor)).Length - 1;

        if (totalStarsText != null)
            totalStarsText.text = $"Stars: [{player.totalStarsCollected}/{BoardGenerator.STAR_COLOR * playableColors}]";

        if (totalMarkedCellsText != null && playerBoard != null)
            totalMarkedCellsText.text = $"Cells: [{playerBoard.GetMarkedCellsCount()}/{BoardGenerator.ROWS * BoardGenerator.COLS}]";
    }

    private void UpdateWildcards(int count, PlayerData player)
    {
        if (wildcardContainer == null || wildcardPrefab == null) return;

        foreach (var icon in activeWildcardIcons) Destroy(icon);
        activeWildcardIcons.Clear();

        for (int i = 0; i < count; i++)
        {
            GameObject icon = Instantiate(wildcardPrefab, wildcardContainer.transform);
            activeWildcardIcons.Add(icon);
        }
    }

    private void UpdateRowsText(PlayerData player)
    {
        if (completedRowsText == null) return;

        string text = "Rows: ";

        for (int i = 0; i < player.completedRows.Length; i++)
        {
            char rowLetter = (char)('A' + i);

            switch (player.completedRows[i])
            {
                case CompletionOrder.First:
                    text += $"<color=green>{rowLetter}</color> ";
                    break;

                case CompletionOrder.Second:
                    text += $"<color=orange>{rowLetter}</color> ";
                    break;
            }
        }

        completedRowsText.text = text;
    }

    private void UpdateColorStatus(PlayerData player)
    {
        if (colorStatusImages == null || colorPalette == null) return;

        int imageIndex = 0;

        foreach (var pair in player.completedColors)
        {
            if (pair.Value == CompletionOrder.None)
                continue;

            if (imageIndex >= colorStatusImages.Length)
                break;

            if (colorStatusImages[imageIndex] != null)
                colorStatusImages[imageIndex].color = colorPalette.GetColor(pair.Key);
            imageIndex++;
        }

        while (imageIndex < colorStatusImages.Length)
        {
            if (colorStatusImages[imageIndex] != null)
                colorStatusImages[imageIndex].color = Color.clear;
            imageIndex++;
        }
    }
}
