using TMPro;
using UnityEngine;

/// <summary>
/// Shows the logged-in player's name in a TextMeshPro label.
/// Add to any TMP text object (e.g. Username_TXT in the Menu scene);
/// it grabs its own TMP component automatically if the field is empty.
/// </summary>
public class UsernameDisplay : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI label;

    [Tooltip("{0} is replaced by the player name, e.g. \"Hi, {0}!\"")]
    [SerializeField] private string format = "{0}";

    private void Start()
    {
        if (label == null) label = GetComponent<TextMeshProUGUI>();
        if (label == null)
        {
            Debug.LogWarning("[UsernameDisplay] No TextMeshProUGUI found.");
            return;
        }

        string name = PlayerSession.IsLoggedIn ? PlayerSession.PlayerName : "Guest";
        label.text = string.Format(format, name);
    }
}
