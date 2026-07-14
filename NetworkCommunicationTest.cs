using Unity.Netcode;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

// Note that we inherit from NetworkBehaviour, not MonoBehaviour
public class NetworkCommunicationTest : NetworkBehaviour
{
    private Button sayHiButton;
    private TextMeshProUGUI hiOutputText;

    public override void OnNetworkSpawn()
    {
        // Only the person who owns this player object should hook up their button
        if (IsOwner)
        {
            // Find the UI elements in the Menu Scene dynamically
            sayHiButton = GameObject.Find("SayHiButton").GetComponent<Button>();
            hiOutputText = GameObject.Find("HiOutputText").GetComponent<TextMeshProUGUI>();

            sayHiButton.onClick.AddListener(OnSayHiClicked);
        }
    }

    private void OnSayHiClicked()
    {
        // The local client gets their ID and tells the Server to broadcast the message
        ulong senderId = NetworkManager.Singleton.LocalClientId;
        SayHiServerRpc(senderId);
    }

    // [ServerRpc] runs ONLY on the Server/Host
    [ServerRpc]
    private void SayHiServerRpc(ulong senderId)
    {
        // The server receives the message and tells ALL clients to execute the ClientRpc
        SayHiClientRpc(senderId);
    }

    // [ClientRpc] runs on ALL connected clients
    [ClientRpc]
    private void SayHiClientRpc(ulong senderId)
    {
        // Find the text object if we haven't already
        if (hiOutputText == null)
        {
            hiOutputText = GameObject.Find("HiOutputText").GetComponent<TextMeshProUGUI>();
        }

        // Append the message to the output text
        hiOutputText.text = $"Hi from Player {senderId}!";
    }
}