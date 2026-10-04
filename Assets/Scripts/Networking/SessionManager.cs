using System;
using System.Text;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MultiplayerGame.Networking
{
    /// <summary>
    /// Host-authoritative session manager that tracks connected players,
    /// ready states, lobby metadata, and coordinates match transitions.
    /// </summary>
    public class SessionManager : NetworkBehaviour
    {
        public static SessionManager Instance { get; private set; }

        [Header("Scene Configuration")]
        [SerializeField] private string gameplaySceneName = "SampleScene";

        // Host-authoritative synchronized lobby name
        private readonly NetworkVariable<FixedString64Bytes> lobbyName = new NetworkVariable<FixedString64Bytes>(
            new FixedString64Bytes("Multiplayer Game"),
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        // Host-authoritative synchronized list of players in the current session
        private readonly NetworkList<PlayerData> players = new NetworkList<PlayerData>(
            new System.Collections.Generic.List<PlayerData>(),
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public NetworkList<PlayerData> Players => players;
        public string CurrentLobbyName => lobbyName.Value.ToString();
        public bool IsMatchStarted { get; private set; } = false;

        public event Action OnPlayersChanged;
        public event Action<bool> OnAllPlayersReadyStatusChanged;
        public event Action OnMatchStarted;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public override void OnNetworkSpawn()
        {
            players.OnListChanged += HandlePlayersListChanged;
            IsMatchStarted = false;

            if (IsServer)
            {
                NetworkManager.Singleton.OnClientConnectedCallback += HandleServerClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback += HandleServerClientDisconnected;

                // Register host player
                RegisterPlayer(NetworkManager.Singleton.LocalClientId, isHost: true);
            }
            else
            {
                // Request server to set client's display name
                string localName = PlayerPrefs.GetString("Multiplayer_PlayerName", $"Player_{NetworkManager.Singleton.LocalClientId}");
                SetPlayerNameRpc(localName);
            }
        }

        public override void OnNetworkDespawn()
        {
            players.OnListChanged -= HandlePlayersListChanged;
            IsMatchStarted = false;

            if (IsServer && NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= HandleServerClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback -= HandleServerClientDisconnected;
            }
        }

        public void SetLobbyName(string name)
        {
            if (IsServer && !string.IsNullOrEmpty(name))
            {
                lobbyName.Value = new FixedString64Bytes(name);
            }
        }

        private void HandleServerClientConnected(ulong clientId)
        {
            if (!IsServer) return;
            RegisterPlayer(clientId, isHost: clientId == NetworkManager.ServerClientId);
        }

        private void HandleServerClientDisconnected(ulong clientId)
        {
            if (!IsServer) return;
            UnregisterPlayer(clientId);
        }

        private void RegisterPlayer(ulong clientId, bool isHost)
        {
            // Avoid duplicate registrations
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i].ClientId == clientId)
                {
                    return;
                }
            }

            string playerName = $"Player_{clientId}";
            if (isHost)
            {
                playerName = PlayerPrefs.GetString("Multiplayer_PlayerName", "Host");
                if (NetworkBootstrap.Instance != null && !string.IsNullOrEmpty(NetworkBootstrap.Instance.LocalPlayerName))
                {
                    playerName = NetworkBootstrap.Instance.LocalPlayerName;
                }
            }

            // Host is automatically marked ready
            PlayerData newPlayer = new PlayerData(clientId, playerName, isReady: isHost, isHost: isHost);
            players.Add(newPlayer);
            Debug.Log($"[SessionManager] Registered player: {newPlayer}");

            // Update LAN discovery player count if broadcasting
            if (LANDiscoveryManager.Instance != null && LANDiscoveryManager.Instance.IsBroadcasting)
            {
                LANDiscoveryManager.Instance.UpdatePlayerCount(players.Count);
            }
        }

        private void UnregisterPlayer(ulong clientId)
        {
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i].ClientId == clientId)
                {
                    Debug.Log($"[SessionManager] Unregistered player: {players[i]}");
                    players.RemoveAt(i);
                    break;
                }
            }

            if (LANDiscoveryManager.Instance != null && LANDiscoveryManager.Instance.IsBroadcasting)
            {
                LANDiscoveryManager.Instance.UpdatePlayerCount(players.Count);
            }
        }

        private void HandlePlayersListChanged(NetworkListEvent<PlayerData> changeEvent)
        {
            OnPlayersChanged?.Invoke();
            OnAllPlayersReadyStatusChanged?.Invoke(AreAllPlayersReady());
        }

        /// <summary>
        /// Request to update player's display name from client to host.
        /// </summary>
        [Rpc(SendTo.Server)]
        public void SetPlayerNameRpc(string newName, RpcParams rpcParams = default)
        {
            if (string.IsNullOrWhiteSpace(newName)) return;

            ulong senderClientId = rpcParams.Receive.SenderClientId;
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i].ClientId == senderClientId)
                {
                    PlayerData updated = players[i];
                    updated.PlayerName = new FixedString64Bytes(newName.Trim());
                    players[i] = updated;
                    Debug.Log($"[SessionManager] Updated name for client {senderClientId}: {updated.PlayerName}");
                    return;
                }
            }
        }

        /// <summary>
        /// Request to toggle or set ready state from a client.
        /// </summary>
        [Rpc(SendTo.Server)]
        public void SetReadyRpc(bool isReady, RpcParams rpcParams = default)
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;

            for (int i = 0; i < players.Count; i++)
            {
                if (players[i].ClientId == senderClientId)
                {
                    PlayerData updated = players[i];
                    updated.IsReady = isReady;
                    players[i] = updated;
                    Debug.Log($"[SessionManager] Updated ready state for client {senderClientId}: {isReady}");
                    return;
                }
            }
        }


        /// <summary>
        /// Checks whether all players in the session are marked ready.
        /// </summary>
        public bool AreAllPlayersReady()
        {
            if (players.Count == 0) return false;

            for (int i = 0; i < players.Count; i++)
            {
                if (!players[i].IsReady)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Checks if the local player is currently marked ready.
        /// </summary>
        public bool IsLocalPlayerReady()
        {
            if (NetworkManager.Singleton == null) return false;
            ulong localClientId = NetworkManager.Singleton.LocalClientId;

            for (int i = 0; i < players.Count; i++)
            {
                if (players[i].ClientId == localClientId)
                {
                    return players[i].IsReady;
                }
            }
            return false;
        }

        /// <summary>
        /// Host-only action to start match and trigger networked scene load.
        /// </summary>
        public void StartMatch()
        {
            if (!IsServer)
            {
                Debug.LogWarning("[SessionManager] Only the host can start the match.");
                return;
            }

            if (!AreAllPlayersReady())
            {
                Debug.LogWarning("[SessionManager] Cannot start match: Not all players are ready.");
                return;
            }

            IsMatchStarted = true;
            OnMatchStarted?.Invoke();

            // Stop public broadcast once game begins
            if (LANDiscoveryManager.Instance != null)
            {
                LANDiscoveryManager.Instance.StopBroadcasting();
            }

            Debug.Log($"[SessionManager] Starting match. Loading scene: {gameplaySceneName}");
            if (NetworkManager.Singleton.SceneManager != null)
            {
                NetworkManager.Singleton.SceneManager.LoadScene(gameplaySceneName, LoadSceneMode.Single);
            }
            else
            {
                SceneManager.LoadScene(gameplaySceneName, LoadSceneMode.Single);
            }
        }
    }
}
