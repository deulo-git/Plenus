using System;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Drives the end-of-match panel (EndingCanvas). It is shown once the game is
/// over (a player has completed two colours). GameManager.EndGame() runs on the
/// server, syncs the FINAL score snapshots to every machine, and then invokes a
/// ClientRpc that calls <see cref="Show"/> on every client (host included).
///
/// The panel is populated entirely from the two synced PlayerData objects
/// (GameManager.player1 / GameManager.player2), so it works identically on the
/// host and on the remote client. Nothing here writes network state — it is a
/// pure read-only view of the final result.
///
/// IMPORTANT — this component lives on the EndingCanvas, which starts INACTIVE.
/// A serialized reference to it from GameManager stays valid even while the
/// object is inactive, and Show() re-activates the panel itself, so this class
/// deliberately does NOT rely on Awake/Start having run: every reference is
/// resolved from the Inspector and every step is null-guarded.
///
/// Inspector wiring (see the EndingCanvas hierarchy):
///   Result_IMG        -> resultImage (Image) OR resultRawImage (RawImage)
///   Result_TXT        -> resultText
///   RowsCompleted     -> rowsContainer (the GridLayoutGroup transform)
///   RowResult_Container prefab -> rowResultPrefab (child "Letter_TXT" + "Value_TXT",
///                                 background = the prefab root's Image)
///   Stats:
///     PlayerAUsername_TXT / PlayerBUsername_TXT
///     P1_ColourScore_TXT  / P2_ColourScore_TXT
///     P1_JokerScore_TXT   / P2_JokerScore_TXT
///     P1_StarsScore_TXT   / P2_StarsScore_TXT
///     P1_FinaleScore_TXT  / P2_FinaleScore_TXT
///   Buttons: BackToMenu_BTN -> backToMenuButton, RemainHere_BTN -> remainHereButton
///   diceRoot -> the "Dice" GameObject under SafeAreaRoot (hidden on show)
/// </summary>
public class EndGameUI : MonoBehaviour
{
    public static EndGameUI Instance { get; private set; }

    [Header("Panel root (activated on Show). Defaults to this GameObject.")]
    [SerializeField] private GameObject endingRoot;

    [Header("Result (assign EITHER an Image+Sprites OR a RawImage+Textures)")]
    [SerializeField] private Image resultImage;
    [SerializeField] private RawImage resultRawImage;
    [SerializeField] private Sprite winSprite;
    [SerializeField] private Sprite loseSprite;
    [SerializeField] private Sprite drawSprite; // optional; falls back to winSprite
    [SerializeField] private Texture winTexture;
    [SerializeField] private Texture loseTexture;
    [SerializeField] private Texture drawTexture; // optional; falls back to winTexture

    [SerializeField] private TextMeshProUGUI resultText;
    [SerializeField] private string victoryText = "Victory";
    [SerializeField] private string defeatText = "Loose";
    [SerializeField] private string drawText = "Draw";

    [Header("Rows completed grid")]
    [Tooltip("The GridLayoutGroup transform that holds the RowResult_Container instances.")]
    [SerializeField] private Transform rowsContainer;
    [Tooltip("RowResult_Container prefab (root Image = background, children Letter_TXT / Value_TXT).")]
    [SerializeField] private GameObject rowResultPrefab;
    [Tooltip("Row completed FIRST (yellow).")]
    [SerializeField] private Color rowFirstColor = new Color(1f, 0.82f, 0.2f);
    [Tooltip("Row completed SECOND (grey).")]
    [SerializeField] private Color rowSecondColor = new Color(0.65f, 0.65f, 0.65f);
    [Tooltip("Whose completed rows are listed in the grid.")]
    [SerializeField] private RowsPerspective rowsShown = RowsPerspective.LocalPlayer;

    public enum RowsPerspective { LocalPlayer, Player1, Player2 }

    [Header("Stats — usernames")]
    [SerializeField] private TextMeshProUGUI playerAUsernameText; // PlayerAUsername_TXT
    [SerializeField] private TextMeshProUGUI playerBUsernameText; // PlayerBUsername_TXT

    [Header("Stats — colours / jokers / stars / finale")]
    [SerializeField] private TextMeshProUGUI p1ColourScoreText;
    [SerializeField] private TextMeshProUGUI p2ColourScoreText;
    [SerializeField] private TextMeshProUGUI p1JokerScoreText;
    [SerializeField] private TextMeshProUGUI p2JokerScoreText;
    [SerializeField] private TextMeshProUGUI p1StarsScoreText;
    [SerializeField] private TextMeshProUGUI p2StarsScoreText;
    [SerializeField] private TextMeshProUGUI p1FinaleScoreText;
    [SerializeField] private TextMeshProUGUI p2FinaleScoreText;

    [Header("Buttons")]
    [SerializeField] private Button backToMenuButton; // BackToMenu_BTN
    [SerializeField] private Button remainHereButton; // RemainHere_BTN
    [SerializeField] private string menuSceneName = "Menu";

    [Header("Gameplay elements to hide")]
    [Tooltip("The whole 'Dice' GameObject under SafeAreaRoot; hidden once the game is over.")]
    [SerializeField] private GameObject diceRoot;

    // RowResult_Container instances we spawned, so Show() can be called again cleanly.
    private readonly List<GameObject> spawnedRows = new List<GameObject>();
    private bool _buttonsWired;

    private void Awake()
    {
        Instance = this;
    }

    private void OnEnable()
    {
        Instance = this;
    }

    /// <summary>
    /// Activate and populate the end-of-match panel from the final, synced state.
    /// Safe to call on the host and on remote clients; every reference is guarded.
    /// </summary>
    public void Show()
    {
        GameObject root = endingRoot != null ? endingRoot : gameObject;
        if (root != null) root.SetActive(true);

        // The match is over: hide the dice so no further rolls look possible.
        if (diceRoot != null) diceRoot.SetActive(false);

        WireButtons();

        GameManager gm = GameManager.Instance;
        if (gm == null)
        {
            Debug.LogWarning("[EndGameUI] GameManager.Instance is null; cannot populate the ending panel.");
            return;
        }

        PlayerData p1 = gm.player1;
        PlayerData p2 = gm.player2;
        if (p1 == null || p2 == null)
        {
            Debug.LogWarning("[EndGameUI] Player data missing; cannot populate the ending panel.");
            return;
        }

        // Rebuild both score breakdowns from the synced state. CalculateTotalScore
        // reads only the PlayerData fields (rows/colours/stars/wildcards); the board
        // argument is unused, so null is fine on every machine (clients have no boards).
        RebuildBreakdown(p1);
        RebuildBreakdown(p2);

        // Local perspective decides Victory / Loose.
        PlayerData local = ResolveLocalPlayer(gm);
        PlayerData opponent = (local == p1) ? p2 : p1;

        PopulateResult(local, opponent);
        PopulateRows(ResolveRowsPlayer(gm, local));
        PopulateStats(p1, p2);
    }

    private void WireButtons()
    {
        if (_buttonsWired) return;

        if (backToMenuButton != null)
        {
            backToMenuButton.onClick.RemoveListener(OnBackToMenu);
            backToMenuButton.onClick.AddListener(OnBackToMenu);
        }
        if (remainHereButton != null)
        {
            remainHereButton.onClick.RemoveListener(OnRemainHere);
            remainHereButton.onClick.AddListener(OnRemainHere);
        }
        _buttonsWired = true;
    }

    private static void RebuildBreakdown(PlayerData p)
    {
        if (ScoreManager.Instance != null)
            ScoreManager.Instance.CalculateTotalScore(p, null);
    }

    private PlayerData ResolveLocalPlayer(GameManager gm)
    {
        if (NetworkManager.Singleton == null) return gm.player1;

        ulong myId = NetworkManager.Singleton.LocalClientId;
        bool distinct = gm.player2 != null && gm.player2.clientId != gm.player1.clientId;
        return (distinct && myId == gm.player2.clientId) ? gm.player2 : gm.player1;
    }

    private PlayerData ResolveRowsPlayer(GameManager gm, PlayerData local)
    {
        switch (rowsShown)
        {
            case RowsPerspective.Player1: return gm.player1;
            case RowsPerspective.Player2: return gm.player2;
            default: return local;
        }
    }

    // ---------------- RESULT ----------------

    private void PopulateResult(PlayerData local, PlayerData opponent)
    {
        int result = local.score.CompareTo(opponent.score); // >0 win, <0 lose, 0 draw

        string label = result > 0 ? victoryText : (result < 0 ? defeatText : drawText);
        if (resultText != null) resultText.text = label;

        if (resultImage != null)
        {
            Sprite s = result > 0 ? winSprite : (result < 0 ? loseSprite : (drawSprite != null ? drawSprite : winSprite));
            if (s != null) resultImage.sprite = s;
        }

        if (resultRawImage != null)
        {
            Texture t = result > 0 ? winTexture : (result < 0 ? loseTexture : (drawTexture != null ? drawTexture : winTexture));
            if (t != null) resultRawImage.texture = t;
        }
    }

    // ---------------- ROWS COMPLETED GRID ----------------

    private void PopulateRows(PlayerData player)
    {
        // Clear any rows from a previous Show().
        foreach (GameObject go in spawnedRows)
            if (go != null) Destroy(go);
        spawnedRows.Clear();

        if (rowsContainer == null || rowResultPrefab == null || player == null || player.completedRows == null)
            return;

        for (int i = 0; i < player.completedRows.Length; i++)
        {
            CompletionOrder order = player.completedRows[i];
            if (order == CompletionOrder.None) continue;

            GameObject entry = Instantiate(rowResultPrefab, rowsContainer, false);
            entry.SetActive(true);
            spawnedRows.Add(entry);

            char letter = (char)('A' + i);
            int points = (order == CompletionOrder.First)
                ? ScoreConfig.RowRewards[i].first
                : ScoreConfig.RowRewards[i].second;

            TextMeshProUGUI letterText = GetText(FindDeep(entry.transform, "Letter_TXT"));
            if (letterText != null) letterText.text = letter.ToString();

            TextMeshProUGUI valueText = GetText(FindDeep(entry.transform, "Value_TXT"));
            if (valueText != null) valueText.text = $"+{points}";

            Image background = entry.GetComponent<Image>();
            if (background != null)
                background.color = (order == CompletionOrder.First) ? rowFirstColor : rowSecondColor;
        }
    }

    // ---------------- STATS ----------------

    private void PopulateStats(PlayerData p1, PlayerData p2)
    {
        if (playerAUsernameText != null) playerAUsernameText.text = p1.playerName;
        if (playerBUsernameText != null) playerBUsernameText.text = p2.playerName;

        FillColumn(p1, p1ColourScoreText, p1JokerScoreText, p1StarsScoreText, p1FinaleScoreText);
        FillColumn(p2, p2ColourScoreText, p2JokerScoreText, p2StarsScoreText, p2FinaleScoreText);
    }

    private static void FillColumn(PlayerData p, TextMeshProUGUI colour, TextMeshProUGUI joker,
                                   TextMeshProUGUI stars, TextMeshProUGUI finale)
    {
        ScoreBreakdown bd = p.scoreBreakdown;

        // Colours: "count (+points)" — e.g. green first + red second => "2 (+8)".
        int colourCount = bd.colors.Count;
        int colourPoints = 0;
        foreach (ScoreBreakdown.ColorScore c in bd.colors) colourPoints += c.points;
        if (colour != null) colour.text = $"{colourCount} (+{colourPoints})";

        // Jokers: "remaining (+points)" — e.g. 3 unused => "3 (+3)".
        int jokerPoints = bd.unusedWildcards * ScoreConfig.RewardPerUnusedWildcard;
        if (joker != null) joker.text = $"{bd.unusedWildcards} (+{jokerPoints})";

        // Stars: "unmarked (penalty)" — e.g. 5 left => "5 (-10)". Penalty is already negative.
        int starPenalty = bd.unmarkedStars * ScoreConfig.PenaltyPerUnmarkedStar;
        if (stars != null) stars.text = $"{bd.unmarkedStars} ({starPenalty})";

        // Final total.
        if (finale != null) finale.text = p.score.ToString();
    }

    // ---------------- BUTTONS ----------------

    // Return to the main menu. Mirrors BackButtonController: shut down any live
    // Netcode session first so we never carry it into the menu scene.
    private void OnBackToMenu()
    {
        if (LobbySession.IsActive)
            _ = LobbySession.LeaveOrDeleteAsync();

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            NetworkManager.Singleton.Shutdown();

        SceneManager.LoadScene(menuSceneName, LoadSceneMode.Single);
    }

    // Stay and look at the boards: just hide the ending panel. The game is already
    // over server-side (state Idle), so no moves are possible, and the dice stay
    // hidden. The player can still toggle between their own and the opponent board.
    private void OnRemainHere()
    {
        GameObject root = endingRoot != null ? endingRoot : gameObject;
        if (root != null) root.SetActive(false);
    }

    // ---------------- HELPERS ----------------

    private static TextMeshProUGUI GetText(Transform t) => t != null ? t.GetComponent<TextMeshProUGUI>() : null;

    // Depth-first search by exact name, including inactive children.
    private static Transform FindDeep(Transform root, string name)
    {
        if (root == null) return null;
        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        foreach (Transform t in all)
            if (t.name == name) return t;
        return null;
    }
}
