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
    /// ready states, and coordinates scene transitions.
    /// </summary>
    public class SessionManager : NetworkBehaviour
    {
        public static SessionManager Instance { get; private set; }

        [Header("Scene Configuration")]
        [SerializeField] private string gameplaySceneName = "SampleScene";

        // Host-authoritative synchronized list of players in the current session
        private readonly NetworkList<PlayerData> players = new NetworkList<PlayerData>(
            new System.Collections.Generic.List<PlayerData>(),
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public NetworkList<PlayerData> Players => players;

        public event Action OnPlayersChanged;
        public event Action<bool> OnAllPlayersReadyStatusChanged;

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

            if (IsServer)
            {
                NetworkManager.Singleton.OnClientConnectedCallback += HandleServerClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback += HandleServerClientDisconnected;

                // Register host player
                RegisterPlayer(NetworkManager.Singleton.LocalClientId, isHost: true);
            }
        }

        public override void OnNetworkDespawn()
        {
            players.OnListChanged -= HandlePlayersListChanged;

            if (IsServer && NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= HandleServerClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback -= HandleServerClientDisconnected;
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
            if (isHost && NetworkBootstrap.Instance != null && !string.IsNullOrEmpty(NetworkBootstrap.Instance.LocalPlayerName))
            {
                playerName = NetworkBootstrap.Instance.LocalPlayerName;
            }

            // Host is automatically ready, or can toggle like others
            PlayerData newPlayer = new PlayerData(clientId, playerName, isReady: isHost, isHost: isHost);
            players.Add(newPlayer);
            Debug.Log($"[SessionManager] Registered player: {newPlayer}");
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
        }

        private void HandlePlayersListChanged(NetworkListEvent<PlayerData> changeEvent)
        {
            OnPlayersChanged?.Invoke();
            OnAllPlayersReadyStatusChanged?.Invoke(AreAllPlayersReady());
        }

        /// <summary>
        /// Request to toggle or set ready state from a client.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void SetReadyServerRpc(bool isReady, ServerRpcParams rpcParams = default)
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

            Debug.Log($"[SessionManager] Starting match. Loading scene: {gameplaySceneName}");
            NetworkManager.Singleton.SceneManager.LoadScene(gameplaySceneName, LoadSceneMode.Single);
        }
    }
}
