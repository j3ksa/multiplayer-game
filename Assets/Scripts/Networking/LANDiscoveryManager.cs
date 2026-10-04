using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace MultiplayerGame.Networking
{
    /// <summary>
    /// Information about a discovered multiplayer game session.
    /// </summary>
    public class DiscoveredServer
    {
        public string ServerId { get; set; }
        public string ServerName { get; set; }
        public string HostPlayerName { get; set; }
        public string ConnectionType { get; set; } // "Relay" or "DirectIP"
        public string Address { get; set; }       // Relay Join Code or IP address
        public ushort Port { get; set; }
        public int CurrentPlayers { get; set; }
        public int MaxPlayers { get; set; }
        public float LastSeenTime { get; set; }
    }

    /// <summary>
    /// Lightweight UDP broadcast discovery manager for local network & loopback discovery.
    /// Broadcasts active games and allows clients to browse available games in real time.
    /// </summary>
    public class LANDiscoveryManager : MonoBehaviour
    {
        public static LANDiscoveryManager Instance { get; private set; }

        private const int DiscoveryPort = 47777;
        private const string MagicHeader = "TD_LOBBY_V1";
        private const float TimeoutSeconds = 4.5f;

        private UdpClient broadcastClient;
        private UdpClient listenerClient;
        private CancellationTokenSource broadcastCts;
        private CancellationTokenSource listenerCts;

        private readonly ConcurrentDictionary<string, DiscoveredServer> activeServers = new ConcurrentDictionary<string, DiscoveredServer>();
        private readonly List<DiscoveredServer> cachedServerList = new List<DiscoveredServer>();

        public bool IsBroadcasting { get; private set; }
        public bool IsListening { get; private set; }

        public event Action OnServerListUpdated;

        private string currentServerId = "";
        private string currentServerName = "Tower Defense Game";
        private string currentHostName = "Host";
        private string currentConnectionType = "DirectIP";
        private string currentAddress = "127.0.0.1";
        private ushort currentPort = 7777;
        private int currentPlayers = 1;
        private int maxPlayers = 4;

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

        private void Update()
        {
            if (IsListening)
            {
                // Prune expired servers
                float now = Time.time;
                bool changed = false;

                foreach (var pair in activeServers)
                {
                    if (now - pair.Value.LastSeenTime > TimeoutSeconds)
                    {
                        activeServers.TryRemove(pair.Key, out _);
                        changed = true;
                    }
                }

                if (changed)
                {
                    UpdateCachedList();
                    OnServerListUpdated?.Invoke();
                }
            }
        }

        private void OnDestroy()
        {
            StopAll();
        }

        private void OnApplicationQuit()
        {
            StopAll();
        }

        public void StopAll()
        {
            StopBroadcasting();
            StopListening();
        }

        #region Broadcasting (Host)

        /// <summary>
        /// Starts broadcasting this session over UDP on the local network.
        /// </summary>
        public void StartBroadcasting(string serverName, string hostName, string connectionType, string address, ushort port, int currentCount, int maxCount)
        {
            StopBroadcasting();

            currentServerId = Guid.NewGuid().ToString("N").Substring(0, 8);
            currentServerName = serverName;
            currentHostName = hostName;
            currentConnectionType = connectionType;
            currentAddress = address;
            currentPort = port;
            currentPlayers = currentCount;
            maxPlayers = maxCount;

            broadcastCts = new CancellationTokenSource();
            IsBroadcasting = true;

            try
            {
                broadcastClient = new UdpClient();
                broadcastClient.EnableBroadcast = true;
                _ = BroadcastLoopAsync(broadcastCts.Token);
                Debug.Log($"[LANDiscovery] Started broadcasting session '{serverName}' (Type: {connectionType}, Addr: {address})");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LANDiscovery] Failed to initialize broadcast client: {ex.Message}");
                IsBroadcasting = false;
            }
        }

        public void UpdatePlayerCount(int count)
        {
            currentPlayers = count;
        }

        public void StopBroadcasting()
        {
            if (!IsBroadcasting) return;

            IsBroadcasting = false;
            if (broadcastCts != null)
            {
                broadcastCts.Cancel();
                broadcastCts.Dispose();
                broadcastCts = null;
            }

            if (broadcastClient != null)
            {
                try { broadcastClient.Close(); } catch { }
                broadcastClient = null;
            }

            Debug.Log("[LANDiscovery] Stopped broadcasting session.");
        }

        private async Task BroadcastLoopAsync(CancellationToken token)
        {
            IPEndPoint broadcastEp = new IPEndPoint(IPAddress.Broadcast, DiscoveryPort);
            IPEndPoint loopbackEp = new IPEndPoint(IPAddress.Loopback, DiscoveryPort);

            while (!token.IsCancellationRequested && IsBroadcasting)
            {
                try
                {
                    string message = $"{MagicHeader}|{currentServerId}|{currentServerName}|{currentHostName}|{currentConnectionType}|{currentAddress}|{currentPort}|{currentPlayers}|{maxPlayers}";
                    byte[] data = Encoding.UTF8.GetBytes(message);

                    // Send to both LAN broadcast and local loopback (for multi-instance local tests)
                    if (broadcastClient != null)
                    {
                        await broadcastClient.SendAsync(data, data.Length, broadcastEp);
                        await broadcastClient.SendAsync(data, data.Length, loopbackEp);
                    }
                }
                catch (ObjectDisposedException) { break; }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[LANDiscovery] Broadcast error: {ex.Message}");
                }

                try
                {
                    await Task.Delay(1500, token);
                }
                catch (TaskCanceledException) { break; }
            }
        }

        #endregion

        #region Listening (Client / Browser)

        /// <summary>
        /// Starts listening for UDP broadcast beacons from active game sessions.
        /// </summary>
        public void StartListening()
        {
            if (IsListening) return;

            activeServers.Clear();
            cachedServerList.Clear();
            listenerCts = new CancellationTokenSource();
            IsListening = true;

            try
            {
                listenerClient = new UdpClient();
                listenerClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                listenerClient.ExclusiveAddressUse = false;
                listenerClient.Client.Bind(new IPEndPoint(IPAddress.Any, DiscoveryPort));

                _ = ListenerLoopAsync(listenerCts.Token);
                Debug.Log($"[LANDiscovery] Started listening for active game sessions on port {DiscoveryPort}...");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LANDiscovery] Failed to bind listener socket: {ex.Message}");
                IsListening = false;
            }
        }

        public void StopListening()
        {
            if (!IsListening) return;

            IsListening = false;
            if (listenerCts != null)
            {
                listenerCts.Cancel();
                listenerCts.Dispose();
                listenerCts = null;
            }

            if (listenerClient != null)
            {
                try { listenerClient.Close(); } catch { }
                listenerClient = null;
            }

            Debug.Log("[LANDiscovery] Stopped listening for sessions.");
        }

        public void ClearServers()
        {
            activeServers.Clear();
            UpdateCachedList();
            OnServerListUpdated?.Invoke();
        }

        private async Task ListenerLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested && IsListening)
            {
                try
                {
                    if (listenerClient == null) break;

                    UdpReceiveResult result = await listenerClient.ReceiveAsync();
                    string message = Encoding.UTF8.GetString(result.Buffer);

                    if (!message.StartsWith(MagicHeader)) continue;

                    string[] parts = message.Split('|');
                    if (parts.Length < 9) continue;

                    string serverId = parts[1];
                    string serverName = parts[2];
                    string hostName = parts[3];
                    string connectionType = parts[4];
                    string address = parts[5];
                    ushort.TryParse(parts[6], out ushort port);
                    int.TryParse(parts[7], out int currentCount);
                    int.TryParse(parts[8], out int maxCount);

                    // If it was direct IP and address was 127.0.0.1 from a remote host, resolve to sender's IP
                    if (connectionType == "DirectIP" && (address == "127.0.0.1" || address == "0.0.0.0"))
                    {
                        if (!IPAddress.IsLoopback(result.RemoteEndPoint.Address))
                        {
                            address = result.RemoteEndPoint.Address.ToString();
                        }
                    }

                    DiscoveredServer server = new DiscoveredServer
                    {
                        ServerId = serverId,
                        ServerName = serverName,
                        HostPlayerName = hostName,
                        ConnectionType = connectionType,
                        Address = address,
                        Port = port,
                        CurrentPlayers = currentCount,
                        MaxPlayers = maxCount,
                        LastSeenTime = Time.time
                    };

                    activeServers[serverId] = server;
                    UpdateCachedList();
                    OnServerListUpdated?.Invoke();
                }
                catch (ObjectDisposedException) { break; }
                catch (Exception ex)
                {
                    if (!token.IsCancellationRequested)
                    {
                        Debug.LogWarning($"[LANDiscovery] Listener receive error: {ex.Message}");
                    }
                }
            }
        }

        private void UpdateCachedList()
        {
            cachedServerList.Clear();
            cachedServerList.AddRange(activeServers.Values);
        }

        /// <summary>
        /// Returns the currently active, unexpired list of discovered servers.
        /// </summary>
        public IReadOnlyList<DiscoveredServer> GetActiveServers()
        {
            return cachedServerList;
        }

        #endregion
    }
}
