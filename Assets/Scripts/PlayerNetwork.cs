using Unity.Netcode;
using UnityEngine;

public class PlayerNetwork : NetworkBehaviour
{
    public PlayerData PlayerData { get; private set; }

    public override void OnNetworkSpawn()
    {
        PlayerData = new PlayerData(
            (int)OwnerClientId + 1,
            $"Player {OwnerClientId + 1}"
        );
        Debug.Log($"Player spawned. Owner={OwnerClientId} IsOwner={IsOwner} IsServer={IsServer}");

        PlayerData.clientId = OwnerClientId;
    }
}