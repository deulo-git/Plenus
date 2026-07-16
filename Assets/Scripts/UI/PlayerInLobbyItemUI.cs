using TMPro;
using UnityEngine;

/// <summary>
/// Populates one row of "PlayerInLobby_PRFB" inside HostLobby's
/// Container > PlayersLobby_Container list. Purely a "set my fields"
/// controller — HostLobbyManager owns the actual player list and decides
/// what to show and when.
/// </summary>
public class PlayerInLobbyItemUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI usernameInitialText; // Avatar > UsernameInitial_TXT
    [SerializeField] private TextMeshProUGUI usernameText;        // Username_TXT
    [SerializeField] private TextMeshProUGUI statusText;          // Status_TXT

    private static readonly Color ReadyColor = new Color(0.35f, 0.8f, 0.4f);
    private static readonly Color ConnectingColor = new Color(0.9f, 0.75f, 0.25f);

    /// <param name="displayName">The player's username.</param>
    /// <param name="isYou">True if this row belongs to whoever is looking at the screen.</param>
    /// <param name="isSessionHost">True if this row is the lobby's host.</param>
    /// <param name="isReady">True once this player is fully connected over Netcode.</param>
    public void Set(string displayName, bool isYou, bool isSessionHost, bool isReady)
    {
        if (usernameInitialText != null)
        {
            usernameInitialText.text = !string.IsNullOrEmpty(displayName)
                ? displayName.Substring(0, 1).ToUpperInvariant()
                : "?";
        }

        if (usernameText != null)
        {
            // The host's own row says "you" to the host and "host" to the
            // client; a non-host row only ever says "you" (to its own
            // player) since there's no other label to show a 2-player duel.
            string suffix = isYou ? " (you)" : (isSessionHost ? " (host)" : "");
            usernameText.text = (displayName ?? "Player") + suffix;
        }

        if (statusText != null)
        {
            statusText.text = isReady ? "Ready" : "Connecting";
            statusText.color = isReady ? ReadyColor : ConnectingColor;
        }
    }
}
