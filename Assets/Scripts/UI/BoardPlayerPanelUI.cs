using System.Collections.Generic;
using Assets.Scripts;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives the on-screen "board owner" stats panel (BoardPlayerPanel) from a
/// PlayerData snapshot: jokers left, colors completed, stars, and per-row
/// completion. It is fed by GameManager with the data of whichever board the
/// local user is currently looking at (own board, or the opponent's board
/// when the opponent view is toggled), so the panel always matches the board
/// on screen.
///
/// Wiring is intentionally minimal: every UI element is auto-located by name
/// under this object's UI root, so the only things worth setting in the
/// Inspector are the ColorPalette and the two row-completion colors. You may
/// still assign the roots explicitly if you ever rename or move things.
///
/// Expected hierarchy (names are what we search for, wherever they live):
///   JokersImages      -> 8 child icons (one per remaining joker)
///   ColoursImages     -> child Images ("Colour", "Colour 2") = completed-color slots
///   ColourCount_TXT   -> "x/2" text
///   StarsCount_TXT    -> "x / 15" text
///   Letters_Container -> Letter_A .. Letter_O (one Image per board row)
/// </summary>
public class BoardPlayerPanelUI : MonoBehaviour
{
    [Header("Palette (same asset the board uses)")]
    [SerializeField] private ColorPalette colorPalette;

    [Header("Row completion colors (Inspector-tunable)")]
    [Tooltip("Row completed FIRST (before the opponent).")]
    [SerializeField] private Color rowFirstColor = new Color(1f, 0.55f, 0.1f);   // taronja
    [Tooltip("Row completed SECOND (after the opponent).")]
    [SerializeField] private Color rowSecondColor = new Color(0.6f, 0.6f, 0.6f); // gris

    [Header("Optional explicit roots (auto-found by name if left empty)")]
    [SerializeField] private Transform jokersImagesRoot;      // JokersImages
    [SerializeField] private Transform coloursImagesRoot;     // ColoursImages
    [SerializeField] private TextMeshProUGUI colourCountText; // ColourCount_TXT
    [SerializeField] private TextMeshProUGUI starsCountText;   // StarsCount_TXT
    [SerializeField] private Transform lettersContainer;      // Letters_Container

    // A color slot that hasn't been earned yet shows plain white.
    private static readonly Color EmptyColourSlot = Color.white;

    private readonly List<GameObject> jokerIcons = new List<GameObject>();
    private readonly List<Image> colourSlots = new List<Image>();
    private readonly List<Image> letterImages = new List<Image>(); // index i -> row i (A..O)
    private Color[] letterDefaultColors;
    private bool _resolved;

    private void Awake() => Resolve();

    // Locate and cache every UI element once. Safe to call again; it no-ops after
    // the first successful pass.
    private void Resolve()
    {
        if (_resolved) return;

        Transform root = transform.root;

        if (jokersImagesRoot == null) jokersImagesRoot = FindDeep(root, "JokersImages");
        if (coloursImagesRoot == null) coloursImagesRoot = FindDeep(root, "ColoursImages");
        if (colourCountText == null) colourCountText = GetText(FindDeep(root, "ColourCount_TXT"));
        if (starsCountText == null) starsCountText = GetText(FindDeep(root, "StarsCount_TXT"));
        if (lettersContainer == null) lettersContainer = FindDeep(root, "Letters_Container");

        jokerIcons.Clear();
        if (jokersImagesRoot != null)
            foreach (Transform child in jokersImagesRoot)
                jokerIcons.Add(child.gameObject);

        colourSlots.Clear();
        if (coloursImagesRoot != null)
            foreach (Transform child in coloursImagesRoot)
            {
                Image img = child.GetComponent<Image>();
                if (img != null) colourSlots.Add(img);
            }

        letterImages.Clear();
        if (lettersContainer != null)
            for (int i = 0; i < BoardGenerator.ROWS; i++)
            {
                char letter = (char)('A' + i);
                Transform t = FindDeep(lettersContainer, "Letter_" + letter);
                letterImages.Add(t != null ? t.GetComponent<Image>() : null);
            }

        // Remember each letter's authored color so uncompleted rows are restored to it.
        letterDefaultColors = new Color[letterImages.Count];
        for (int i = 0; i < letterImages.Count; i++)
            letterDefaultColors[i] = letterImages[i] != null ? letterImages[i].color : Color.white;

        _resolved = true;
    }

    /// <summary>Update the whole panel from the given player's synced data.</summary>
    public void Refresh(PlayerData player)
    {
        if (player == null) return;
        Resolve();

        UpdateJokers(player.wildcardsRemaining);
        UpdateColours(player);
        UpdateStars(player.totalStarsCollected);
        UpdateRows(player);
    }

    // Show the first `remaining` joker icons, hide the rest (each spent joker
    // removes one, from the end).
    private void UpdateJokers(int remaining)
    {
        for (int i = 0; i < jokerIcons.Count; i++)
            if (jokerIcons[i] != null) jokerIcons[i].SetActive(i < remaining);
    }

    // Paint one slot per completed color; leave the rest white. Update "x/2".
    private void UpdateColours(PlayerData player)
    {
        int filled = 0;

        if (player.completedColors != null)
            foreach (KeyValuePair<CellColor, CompletionOrder> pair in player.completedColors)
            {
                if (pair.Value == CompletionOrder.None) continue;
                if (filled >= colourSlots.Count) break;
                if (colourSlots[filled] != null && colorPalette != null)
                    colourSlots[filled].color = colorPalette.GetColor(pair.Key);
                filled++;
            }

        for (int i = filled; i < colourSlots.Count; i++)
            if (colourSlots[i] != null) colourSlots[i].color = EmptyColourSlot;

        if (colourCountText != null)
            colourCountText.text = $"{filled}/{colourSlots.Count}";
    }

    private void UpdateStars(int stars)
    {
        if (starsCountText == null) return;
        int playableColors = System.Enum.GetValues(typeof(CellColor)).Length - 1; // exclude Black
        int totalStars = BoardGenerator.STAR_COLOR * playableColors;               // 3 * 5 = 15
        starsCountText.text = $"{stars} / {totalStars}";
    }

    // Paint each completed row's letter: orange if completed first, grey if second,
    // otherwise restore its authored color.
    private void UpdateRows(PlayerData player)
    {
        if (player.completedRows == null) return;

        int n = Mathf.Min(letterImages.Count, player.completedRows.Length);
        for (int i = 0; i < n; i++)
        {
            if (letterImages[i] == null) continue;
            switch (player.completedRows[i])
            {
                case CompletionOrder.First:  letterImages[i].color = rowFirstColor;  break;
                case CompletionOrder.Second: letterImages[i].color = rowSecondColor; break;
                default:                     letterImages[i].color = letterDefaultColors[i]; break;
            }
        }
    }

    private static TextMeshProUGUI GetText(Transform t) => t != null ? t.GetComponent<TextMeshProUGUI>() : null;

    // Depth-first search by exact name, including inactive objects.
    private static Transform FindDeep(Transform root, string name)
    {
        if (root == null) return null;
        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        foreach (Transform t in all)
            if (t.name == name) return t;
        return null;
    }
}
