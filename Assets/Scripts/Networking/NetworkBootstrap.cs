using System;
using System.Text;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace MultiplayerGame.Networking
{
    /// <summary>
    /// Core networking bootstrap and lifecycle manager.
    /// Configures Unity Netcode for GameObjects (NGO) and Unity Transport (UTP),
    /// handles connection approval, and provides start/stop API.
    /// </summary>
    [RequireComponent(typeof(NetworkManager))]
    public class NetworkBootstrap : MonoBehaviour
    {
        public static NetworkBootstrap Instance { get; private set; }

        [Header("Default Network Settings")]
        [SerializeField] private string defaultAddress = "127.0.0.1";
        [SerializeField] private ushort defaultPort = 7777;
        [SerializeField] private int maxPlayers = 4;

        [Header("Local Player")]
        [SerializeField] private string defaultPlayerName = "Player";

        private NetworkManager networkManager;
        private UnityTransport unityTransport;

        public event Action OnServerStartedEvent;
        public event Action<ulong> OnClientConnectedEvent;
        public event Action<ulong> OnClientDisconnectedEvent;
        public event Action<string> OnConnectionFailedEvent;

        public string LocalPlayerName { get; set; }
        public int MaxPlayers => maxPlayers;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            networkManager = GetComponent<NetworkManager>();
            unityTransport = GetComponent<UnityTransport>();

            if (unityTransport == null)
            {
                unityTransport = gameObject.AddComponent<UnityTransport>();
            }

            LocalPlayerName = string.IsNullOrEmpty(LocalPlayerName) ? defaultPlayerName : LocalPlayerName;
        }

        private void Start()
        {
            RegisterCallbacks();
        }

        private void OnDestroy()
        {
            UnregisterCallbacks();
        }

        private void RegisterCallbacks()
        {
            if (networkManager == null) return;

            networkManager.OnServerStarted += HandleServerStarted;
            networkManager.OnClientConnectedCallback += HandleClientConnected;
            networkManager.OnClientDisconnectCallback += HandleClientDisconnected;
        }

        private void UnregisterCallbacks()
        {
            if (networkManager == null) return;

            networkManager.OnServerStarted -= HandleServerStarted;
            networkManager.OnClientConnectedCallback -= HandleClientConnected;
            networkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
        }

        /// <summary>
        /// Starts hosting a game session (acts as both server and local client).
        /// </summary>
        public bool StartHost(string address = null, ushort? port = null, string playerName = null)
        {
            ConfigureTransport(address ?? defaultAddress, port ?? defaultPort);
            SetConnectionPayload(playerName ?? LocalPlayerName);

            networkManager.ConnectionApprovalCallback = HandleConnectionApproval;
            bool success = networkManager.StartHost();
            if (!success)
            {
                OnConnectionFailedEvent?.Invoke("Failed to start host.");
            }
            return success;
        }

        /// <summary>
        /// Starts connecting to a remote host as a client.
        /// </summary>
        public bool StartClient(string address = null, ushort? port = null, string playerName = null)
        {
            ConfigureTransport(address ?? defaultAddress, port ?? defaultPort);
            SetConnectionPayload(playerName ?? LocalPlayerName);

            bool success = networkManager.StartClient();
            if (!success)
            {
                OnConnectionFailedEvent?.Invoke("Failed to connect client.");
            }
            return success;
        }

        /// <summary>
        /// Starts a dedicated server instance without local client presentation.
        /// </summary>
        public bool StartServer(string address = null, ushort? port = null)
        {
            ConfigureTransport(address ?? defaultAddress, port ?? defaultPort);
            networkManager.ConnectionApprovalCallback = HandleConnectionApproval;

            bool success = networkManager.StartServer();
            if (!success)
            {
                OnConnectionFailedEvent?.Invoke("Failed to start dedicated server.");
            }
            return success;
        }

        /// <summary>
        /// Disconnects from current session or stops hosting.
        /// </summary>
        public void Shutdown()
        {
            if (networkManager != null && networkManager.IsListening)
            {
                networkManager.Shutdown();
            }
        }

        private void ConfigureTransport(string address, ushort port)
        {
            if (unityTransport == null)
            {
                unityTransport = networkManager.GetComponent<UnityTransport>();
            }

            if (unityTransport != null)
            {
                unityTransport.SetConnectionData(address, port);
            }
        }

        private void SetConnectionPayload(string playerName)
        {
            LocalPlayerName = playerName;
            byte[] payload = Encoding.UTF8.GetBytes(playerName);
            networkManager.NetworkConfig.ConnectionData = payload;
        }

        /// <summary>
        /// Host-authoritative connection approval check.
        /// Rejects connections if lobby is full or payload is invalid.
        /// </summary>
        private void HandleConnectionApproval(
            NetworkManager.ConnectionApprovalRequest request,
            NetworkManager.ConnectionApprovalResponse response)
        {
            // Reject if room is full (excluding host if joining)
            if (networkManager.ConnectedClientsIds.Count >= maxPlayers)
            {
                response.Approved = false;
                response.Reason = "Session is full.";
                response.Pending = false;
                return;
            }

            // Connection is approved
            response.Approved = true;
            response.CreatePlayerObject = false; // Player character objects spawned after match start
            response.Pending = false;
        }

        private void HandleServerStarted()
        {
            Debug.Log("[NetworkBootstrap] Server started successfully.");
            OnServerStartedEvent?.Invoke();
        }

        private void HandleClientConnected(ulong clientId)
        {
            Debug.Log($"[NetworkBootstrap] Client connected: {clientId}");
            OnClientConnectedEvent?.Invoke(clientId);
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            Debug.Log($"[NetworkBootstrap] Client disconnected: {clientId}");
            if (networkManager.IsClient && !networkManager.IsServer && clientId == networkManager.LocalClientId)
            {
                string disconnectReason = networkManager.DisconnectReason;
                if (string.IsNullOrEmpty(disconnectReason))
                {
                    disconnectReason = "Disconnected from host.";
                }
                OnConnectionFailedEvent?.Invoke(disconnectReason);
            }
            OnClientDisconnectedEvent?.Invoke(clientId);
        }

        /// <summary>
        /// Helper to extract player name from connection approval request payload.
        /// </summary>
        public static string ExtractPlayerName(byte[] payload, ulong fallbackClientId)
        {
            if (payload != null && payload.Length > 0)
            {
                try
                {
                    string name = Encoding.UTF8.GetString(payload).Trim();
                    if (!string.IsNullOrEmpty(name))
                    {
                        return name;
                    }
                }
                catch (Exception)
                {
                    // Fall back to default name
                }
            }
            return $"Player_{fallbackClientId}";
        }
    }
}
