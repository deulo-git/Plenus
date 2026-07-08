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

    // Crida aquest mètode cada cop que el jugador fa un moviment 
    public void UpdateUI(PlayerData player, BoardManager playerBoard)
    {
        // Actualitza el nom del jugador
        if (player == null) return;
        playerNameText.text = player.playerName;

        // 1. Actualitzar Puntuació
        totalScoreText.text = $"Score: {player.score}";

        // 2. Actualitzar Comodins
        UpdateWildcards(player.wildcardsRemaining, player);

        // 3. Actualitzar Files completades (Text A-O)
        UpdateRowsText(player);

        // 4. Actualitzar Colors completats
        UpdateColorStatus(player);

        // 5. Actualitzar Estrelles totals
        int playableColors = System.Enum.GetValues(typeof(CellColor)).Length - 1;
        totalStarsText.text = $"Stars: [{player.totalStarsCollected}/{BoardGenerator.STAR_COLOR * playableColors}]";

        totalMarkedCellsText.text = $"Cells: [{playerBoard.GetMarkedCellsCount()}/{BoardGenerator.ROWS * BoardGenerator.COLS}]";
    }

    private void UpdateWildcards(int count, PlayerData player)
    {
        // Neteja icones actuals
        foreach (var icon in activeWildcardIcons) Destroy(icon);
        activeWildcardIcons.Clear();

        // Crea tantes icones com comodins queden
        for (int i = 0; i < count; i++)
        {
            GameObject icon = Instantiate(wildcardPrefab, wildcardContainer.transform);
            activeWildcardIcons.Add(icon);
        }

    }

    private void UpdateRowsText(PlayerData player)
    {
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
        int imageIndex = 0;

        foreach (var pair in player.completedColors)
        {
            // Ignore colors not completed
            if (pair.Value == CompletionOrder.None)
                continue;

            if (imageIndex >= colorStatusImages.Length)
                break;

            colorStatusImages[imageIndex].color = colorPalette.GetColor(pair.Key);
            imageIndex++;
        }

        // Clear remaining images
        while (imageIndex < colorStatusImages.Length)
        {
            colorStatusImages[imageIndex].color = Color.clear;
            imageIndex++;
        }
    }
}