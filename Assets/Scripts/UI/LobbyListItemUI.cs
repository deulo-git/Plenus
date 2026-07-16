using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Populates one row of "Lobby_PRFB" inside JoinLobby's
/// Container > OpenLobbies_Container > LobbiesContent list, and forwards
/// the row's Join button click back to whoever populated it
/// (JoinLobbyManager), which knows which session this row represents.
/// </summary>
public class LobbyListItemUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI hostInitialText; // HostAvatar > InitialUsername_TXT
    [SerializeField] private TextMeshProUGUI hostNameText;    // HostName_TXT
    [SerializeField] private TextMeshProUGUI playersText;     // Players_TXT
    [SerializeField] private Button joinButton;               // JoinLobby_BTN

    private Action _onJoin;

    public void Set(string hostName, int playerCount, int maxPlayers, Action onJoin)
    {
        if (hostInitialText != null)
        {
            hostInitialText.text = !string.IsNullOrEmpty(hostName)
                ? hostName.Substring(0, 1).ToUpperInvariant()
                : "?";
        }

        if (hostNameText != null) hostNameText.text = hostName ?? "Lobby";
        if (playersText != null) playersText.text = $"{playerCount}/{maxPlayers}";

        _onJoin = onJoin;
        if (joinButton != null)
        {
            joinButton.onClick.RemoveAllListeners();
            joinButton.onClick.AddListener(() => _onJoin?.Invoke());
        }
    }
}
