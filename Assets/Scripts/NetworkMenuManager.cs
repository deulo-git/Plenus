using Unity.Netcode;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.SceneManagement; // Required for LoadSceneMode

public class NetworkMenuManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Button hostButton;
    [SerializeField] private Button joinButton;
    [SerializeField] private Button startGameButton; // NEW: Button to start the game
    [SerializeField] private TextMeshProUGUI statusText;

    private void Start()
    {
        // Add listeners to the buttons
        hostButton.onClick.AddListener(StartHost);
        joinButton.onClick.AddListener(StartClient);
        startGameButton.onClick.AddListener(StartGame); // NEW: Listener for the start button
        
        // Subscribe to the event: "Tell me when ANY client successfully connects"
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
    }

    private void OnDestroy()
    {
        // Best practice: Unsubscribe when the object is destroyed to prevent memory leaks
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        }
    }

    private void StartHost()
    {
        if (NetworkManager.Singleton.StartHost())
        {
            statusText.text = "Host started successfully. Waiting for players...";
            DisableButtons();
        }
        else
        {
            statusText.text = "Failed to start Host.";
        }
    }

    private void StartClient()
    {
        if (NetworkManager.Singleton.StartClient())
        {
            // We just say "Connecting..." here. The actual "Connected!" text 
            // will be handled by the OnClientConnected callback.
            statusText.text = "Connecting to Host...";
            DisableButtons();
        }
        else
        {
            statusText.text = "Failed to initiate connection.";
        }
    }

    // This function runs automatically whenever a connection is fully established
    private void OnClientConnected(ulong clientId)
    {
        // If the ID that just connected matches OUR ID, it means we successfully joined
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            statusText.text = "Connected!";
        }
        // If we are the Host, update the text to show someone joined
        else if (NetworkManager.Singleton.IsHost)
        {
            statusText.text = $"Player {clientId} joined the game! Ready to start.";
        }

        // NEW: Check if we are the Host and if there are enough players (2 in this case)
        if (NetworkManager.Singleton.IsHost && NetworkManager.Singleton.ConnectedClientsList.Count >= 2)
        {
            // Only the Host will see the "Start Game" button
            startGameButton.gameObject.SetActive(true);
        }
    }

    // NEW: Function to load the Main scene
    private void StartGame()
    {
        Debug.Log(NetworkManager.Singleton.ConnectedClientsList.Count);
        foreach (var client in NetworkManager.Singleton.ConnectedClientsIds)
        {
            Debug.Log(client);
        }
        // Double check: Only the server/host is allowed to change the networked scene
        if (NetworkManager.Singleton.IsServer)
        {
            statusText.text = "Loading game...";

            // Use NetworkManager's SceneManager to load the scene. 
            // This ensures all connected clients automatically load the scene as well.
            NetworkManager.Singleton.SceneManager.LoadScene("Main", LoadSceneMode.Single);
        }
    }

    private void DisableButtons()
    {
        // Prevent clicking multiple times
        hostButton.interactable = false;
        joinButton.interactable = false;
    }
}