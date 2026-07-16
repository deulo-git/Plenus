using TMPro;
using UnityEngine;

/// <summary>
/// Shows the logged-in player's name in a TextMeshPro label.
/// Add to any TMP text object, or to a parent/container of one (e.g. if
/// Username_TXT now lives a couple of levels deep under some UserContainer
/// wrapper) — it will locate the TMP component on itself, on a child, or on
/// a parent, in that order, if the field isn't assigned directly.
/// </summary>
public class UsernameDisplay : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI label;

    [Tooltip("{0} is replaced by the player name, e.g. \"Hi, {0}!\"")]
    [SerializeField] private string format = "{0}";

    private void Start() => Refresh();

    // Also refresh when the object is (re)enabled, in case this panel is
    // shown/hidden rather than the scene being reloaded, or PlayerSession
    // changes after Start() already ran (e.g. re-login without a scene swap).
    private void OnEnable() => Refresh();

    /// <summary>Re-reads PlayerSession and updates the label. Safe to call anytime.</summary>
    public void Refresh()
    {
        if (label == null) label = GetComponent<TextMeshProUGUI>();
        if (label == null) label = GetComponentInChildren<TextMeshProUGUI>(true);
        if (label == null) label = GetComponentInParent<TextMeshProUGUI>();
        if (label == null)
        {
            Debug.LogWarning($"[UsernameDisplay] No TextMeshProUGUI found on/near '{name}'. " +
                              "Assign the 'label' field in the Inspector.");
            return;
        }

        string playerName = PlayerSession.IsLoggedIn ? PlayerSession.PlayerName : "Guest";
        label.text = string.Format(format, playerName);
    }
}
