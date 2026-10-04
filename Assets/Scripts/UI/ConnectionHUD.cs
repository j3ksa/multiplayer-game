using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MultiplayerGame.Networking;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.UI;

namespace MultiplayerGame.UI
{
    /// <summary>
    /// Multiplayer match browser HUD.
    /// Supports both:
    /// 1. Custom Canvas UI elements assigned via Inspector (InputFields, Buttons, Panels, Containers, Prefabs).
    /// 2. Automatic fallback IMGUI if no custom Canvas is assigned.
    /// </summary>
    public class ConnectionHUD : MonoBehaviour
    {
        public static ConnectionHUD Instance { get; private set; }

        public enum MenuTab { ServerList, HostGame, DirectConnect }
        public enum HostMode { Relay, DirectIP }

        [Header("Custom Canvas UI Elements (Optional)")]
        [Tooltip("Root GameObject/Panel for the custom Main Menu Canvas. If assigned, Canvas UI will be driven automatically.")]
        [SerializeField] private GameObject menuCanvasPanel;

        [Header("Player Profile")]
        [SerializeField] private TMP_InputField playerNameInputField;

        [Header("Navigation / Tab Panels")]
        [SerializeField] private GameObject serverListTabPanel;
        [SerializeField] private GameObject hostGameTabPanel;
        [SerializeField] private GameObject directConnectTabPanel;
        [SerializeField] private Button tabServerListButton;
        [SerializeField] private Button tabHostGameButton;
        [SerializeField] private Button tabDirectConnectButton;

        [Header("Server List UI Elements")]
        [SerializeField] private Transform serverListContainer;
        [SerializeField] private GameObject serverEntryPrefab;
        [SerializeField] private Button refreshServerListButton;
        [SerializeField] private TMP_Text serverCountText;

        [Header("Host Game UI Elements")]
        [SerializeField] private TMP_InputField hostLobbyNameInputField;
        [SerializeField] private Button hostRelayButton;
        [SerializeField] private Button hostDirectButton;

        [Header("Direct Connect UI Elements")]
        [SerializeField] private TMP_InputField joinCodeInputField;
        [SerializeField] private Button joinWithCodeButton;
        [SerializeField] private TMP_InputField directIpInputField;
        [SerializeField] private TMP_InputField directPortInputField;
        [SerializeField] private Button joinDirectButton;

        [Header("Status UI")]
        [SerializeField] private TMP_Text statusMessageText;

        [Header("Fallback Settings")]
        [Tooltip("Enable IMGUI fallback if no custom Canvas panel is assigned.")]
        [SerializeField] private bool useFallbackOnGUI = true;

        [Header("Relay Settings")]
        [SerializeField] private int defaultMaxConnections = 4;

        [Header("Direct IP Settings")]
        [SerializeField] private string targetIp = "127.0.0.1";
        [SerializeField] private ushort targetPort = 7777;

        private MenuTab activeTab = MenuTab.ServerList;
        private HostMode hostMode = HostMode.Relay;

        private string playerNameInput = "Player";
        private string hostLobbyName = "Tower Defense Game";
        private int maxPlayersInput = 4;

        private string joinCodeInput = "";
        private string directIpInput = "127.0.0.1";
        private string directPortInput = "7777";

        private Vector2 serverListScroll;
        private string statusMessage = "";
        private bool isConnecting = false;
        private string activeJoinCode = "";

        private LobbyUI lobbyUI;
        private readonly List<GameObject> spawnedServerEntries = new List<GameObject>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            playerNameInput = PlayerPrefs.GetString("Multiplayer_PlayerName", $"Player_{UnityEngine.Random.Range(100, 999)}");
            hostLobbyName = $"{playerNameInput}'s Match";
            directIpInput = targetIp;
            directPortInput = targetPort.ToString();
        }

        private void Start()
        {
            lobbyUI = GetComponent<LobbyUI>();
            if (lobbyUI == null)
            {
                lobbyUI = gameObject.AddComponent<LobbyUI>();
            }

            if (LANDiscoveryManager.Instance == null)
            {
                GameObject discoveryObj = new GameObject("[LANDiscoveryManager]");
                discoveryObj.AddComponent<LANDiscoveryManager>();
            }

            if (LANDiscoveryManager.Instance != null)
            {
                LANDiscoveryManager.Instance.OnServerListUpdated += HandleServerListUpdated;
                LANDiscoveryManager.Instance.StartListening();
            }

            InitCustomUIBindings();
        }

        private void OnDestroy()
        {
            if (LANDiscoveryManager.Instance != null)
            {
                LANDiscoveryManager.Instance.OnServerListUpdated -= HandleServerListUpdated;
                LANDiscoveryManager.Instance.StopAll();
            }

            RemoveCustomUIBindings();
        }

        private void Update()
        {
            bool isOffline = NetworkManager.Singleton == null || (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer);

            if (menuCanvasPanel != null)
            {
                if (menuCanvasPanel.activeSelf != isOffline)
                {
                    menuCanvasPanel.SetActive(isOffline);
                }
            }
        }

        #region Custom Canvas Setup & Bindings

        private void InitCustomUIBindings()
        {
            if (playerNameInputField != null)
            {
                playerNameInputField.text = playerNameInput;
                playerNameInputField.onValueChanged.AddListener(OnPlayerNameInputChanged);
            }

            if (hostLobbyNameInputField != null)
            {
                hostLobbyNameInputField.text = hostLobbyName;
                hostLobbyNameInputField.onValueChanged.AddListener(val => hostLobbyName = val);
            }

            if (directIpInputField != null) directIpInputField.text = directIpInput;
            if (directPortInputField != null) directPortInputField.text = directPortInput;

            // Navigation Tabs
            if (tabServerListButton != null) tabServerListButton.onClick.AddListener(ShowServerListTab);
            if (tabHostGameButton != null) tabHostGameButton.onClick.AddListener(ShowHostGameTab);
            if (tabDirectConnectButton != null) tabDirectConnectButton.onClick.AddListener(ShowDirectConnectTab);

            // Action Buttons
            if (refreshServerListButton != null) refreshServerListButton.onClick.AddListener(OnRefreshServerListClicked);
            if (hostRelayButton != null) hostRelayButton.onClick.AddListener(OnHostRelayClicked);
            if (hostDirectButton != null) hostDirectButton.onClick.AddListener(OnHostDirectClicked);
            if (joinWithCodeButton != null) joinWithCodeButton.onClick.AddListener(OnJoinRelayWithCodeClicked);
            if (joinDirectButton != null) joinDirectButton.onClick.AddListener(OnJoinDirectIpClicked);

            ShowServerListTab();
        }

        private void RemoveCustomUIBindings()
        {
            if (playerNameInputField != null) playerNameInputField.onValueChanged.RemoveListener(OnPlayerNameInputChanged);
            if (tabServerListButton != null) tabServerListButton.onClick.RemoveListener(ShowServerListTab);
            if (tabHostGameButton != null) tabHostGameButton.onClick.RemoveListener(ShowHostGameTab);
            if (tabDirectConnectButton != null) tabDirectConnectButton.onClick.RemoveListener(ShowDirectConnectTab);
            if (refreshServerListButton != null) refreshServerListButton.onClick.RemoveListener(OnRefreshServerListClicked);
            if (hostRelayButton != null) hostRelayButton.onClick.RemoveListener(OnHostRelayClicked);
            if (hostDirectButton != null) hostDirectButton.onClick.RemoveListener(OnHostDirectClicked);
            if (joinWithCodeButton != null) joinWithCodeButton.onClick.RemoveListener(OnJoinRelayWithCodeClicked);
            if (joinDirectButton != null) joinDirectButton.onClick.RemoveListener(OnJoinDirectIpClicked);
        }

        public void OnPlayerNameInputChanged(string newName)
        {
            if (!string.IsNullOrWhiteSpace(newName))
            {
                playerNameInput = newName.Trim();
                PlayerPrefs.SetString("Multiplayer_PlayerName", playerNameInput);
                PlayerPrefs.Save();
            }
        }

        public void ShowServerListTab()
        {
            activeTab = MenuTab.ServerList;
            if (serverListTabPanel != null) serverListTabPanel.SetActive(true);
            if (hostGameTabPanel != null) hostGameTabPanel.SetActive(false);
            if (directConnectTabPanel != null) directConnectTabPanel.SetActive(false);
            LANDiscoveryManager.Instance?.StartListening();
        }

        public void ShowHostGameTab()
        {
            activeTab = MenuTab.HostGame;
            if (serverListTabPanel != null) serverListTabPanel.SetActive(false);
            if (hostGameTabPanel != null) hostGameTabPanel.SetActive(true);
            if (directConnectTabPanel != null) directConnectTabPanel.SetActive(false);
        }

        public void ShowDirectConnectTab()
        {
            activeTab = MenuTab.DirectConnect;
            if (serverListTabPanel != null) serverListTabPanel.SetActive(false);
            if (hostGameTabPanel != null) hostGameTabPanel.SetActive(false);
            if (directConnectTabPanel != null) directConnectTabPanel.SetActive(true);
        }

        private void HandleServerListUpdated()
        {
            if (serverListContainer == null || serverEntryPrefab == null) return;

            // Clear previous items
            for (int i = 0; i < spawnedServerEntries.Count; i++)
            {
                if (spawnedServerEntries[i] != null) Destroy(spawnedServerEntries[i]);
            }
            spawnedServerEntries.Clear();

            var servers = LANDiscoveryManager.Instance?.GetActiveServers();
            if (servers == null) return;

            if (serverCountText != null)
            {
                serverCountText.text = $"{servers.Count} Games Found";
            }

            for (int i = 0; i < servers.Count; i++)
            {
                var s = servers[i];
                GameObject entryObj = Instantiate(serverEntryPrefab, serverListContainer);
                spawnedServerEntries.Add(entryObj);

                var entryUI = entryObj.GetComponent<ServerEntryUI>();
                if (entryUI != null)
                {
                    entryUI.Setup(s, JoinDiscoveredServer);
                }
            }
        }

        #endregion

        #region Public Actions (Inspector & Code Callable)

        public void OnRefreshServerListClicked()
        {
            SetStatus("Refreshing server list...");
            LANDiscoveryManager.Instance?.ClearServers();
            LANDiscoveryManager.Instance?.StartListening();
        }

        public void OnHostRelayClicked()
        {
            _ = StartRelayHostAsync();
        }

        public void OnHostDirectClicked()
        {
            StartDirectHost();
        }

        public void OnJoinRelayWithCodeClicked()
        {
            string code = joinCodeInputField != null ? joinCodeInputField.text : joinCodeInput;
            code = code?.ToUpper().Trim();

            if (string.IsNullOrEmpty(code))
            {
                SetStatus("Please enter a valid Join Code.");
                return;
            }

            _ = StartRelayClientAsync(code);
        }

        public void OnJoinDirectIpClicked()
        {
            string ip = directIpInputField != null ? directIpInputField.text : directIpInput;
            string portStr = directPortInputField != null ? directPortInputField.text : directPortInput;

            ushort.TryParse(portStr, out ushort port);
            if (port == 0) port = 7777;

            StartDirectClient(ip, port);
        }

        public void SetStatus(string msg)
        {
            statusMessage = msg;
            if (statusMessageText != null)
            {
                statusMessageText.text = msg;
            }
        }

        #endregion

        #region Fallback IMGUI Support

        private void OnGUI()
        {
            if (menuCanvasPanel != null || !useFallbackOnGUI)
            {
                return;
            }

            if (NetworkManager.Singleton == null)
            {
                GUILayout.BeginArea(new Rect(20, 20, 320, 80), "Error", GUI.skin.window);
                GUILayout.Label("NetworkManager not found.");
                GUILayout.EndArea();
                return;
            }

            if (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
            {
                DrawOfflineBrowserUI();
            }
        }

        private void DrawOfflineBrowserUI()
        {
            const int width = 500;
            const int height = 480;
            float x = (Screen.width - width) * 0.5f;
            float y = (Screen.height - height) * 0.5f;
            Rect windowRect = new Rect(Mathf.Max(10, x), Mathf.Max(10, y), width, height);

            GUILayout.BeginArea(windowRect, "Multiplayer Match Browser", GUI.skin.window);

            GUILayout.BeginHorizontal("box");
            GUILayout.Label("Player Name:", GUILayout.Width(85));
            string newName = GUILayout.TextField(playerNameInput, 24);
            if (newName != playerNameInput)
            {
                OnPlayerNameInputChanged(newName);
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6);

            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(activeTab == MenuTab.ServerList, "Browse Games (Server List)", "Button", GUILayout.Height(30)))
            {
                if (activeTab != MenuTab.ServerList) ShowServerListTab();
            }
            if (GUILayout.Toggle(activeTab == MenuTab.HostGame, "Host New Game", "Button", GUILayout.Height(30)))
            {
                if (activeTab != MenuTab.HostGame) ShowHostGameTab();
            }
            if (GUILayout.Toggle(activeTab == MenuTab.DirectConnect, "Direct Connect", "Button", GUILayout.Height(30)))
            {
                if (activeTab != MenuTab.DirectConnect) ShowDirectConnectTab();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(8);

            switch (activeTab)
            {
                case MenuTab.ServerList:
                    DrawFallbackServerListTab();
                    break;
                case MenuTab.HostGame:
                    DrawFallbackHostGameTab();
                    break;
                case MenuTab.DirectConnect:
                    DrawFallbackDirectConnectTab();
                    break;
            }

            if (!string.IsNullOrEmpty(statusMessage))
            {
                GUILayout.Space(8);
                GUI.color = Color.yellow;
                GUILayout.Label(statusMessage);
                GUI.color = Color.white;
            }

            GUILayout.EndArea();
        }

        private void DrawFallbackServerListTab()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("<b>Available Games on Local Network / VPN:</b>");
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Refresh / Scan", GUILayout.Width(110), GUILayout.Height(24)))
            {
                OnRefreshServerListClicked();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(4);

            serverListScroll = GUILayout.BeginScrollView(serverListScroll, "box", GUILayout.Height(260));

            var servers = LANDiscoveryManager.Instance != null
                ? LANDiscoveryManager.Instance.GetActiveServers()
                : null;

            if (servers == null || servers.Count == 0)
            {
                GUILayout.FlexibleSpace();
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label("<color=gray>No active games discovered yet.\nHost a game on another instance, or click Refresh.</color>");
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                GUILayout.FlexibleSpace();
            }
            else
            {
                for (int i = 0; i < servers.Count; i++)
                {
                    var s = servers[i];
                    GUILayout.BeginHorizontal("box");

                    GUILayout.BeginVertical();
                    GUILayout.Label($"<b>{s.ServerName}</b>");
                    string typeColor = s.ConnectionType == "Relay" ? "cyan" : "lime";
                    GUILayout.Label($"Host: {s.HostPlayerName} | <color={typeColor}>[{s.ConnectionType}]</color> | Players: {s.CurrentPlayers}/{s.MaxPlayers}", GUILayout.Height(18));
                    GUILayout.EndVertical();

                    GUILayout.FlexibleSpace();

                    GUI.enabled = !isConnecting && (s.CurrentPlayers < s.MaxPlayers);
                    string joinBtnText = s.CurrentPlayers >= s.MaxPlayers ? "Full" : "Join";

                    if (GUILayout.Button(joinBtnText, GUILayout.Width(75), GUILayout.Height(36)))
                    {
                        JoinDiscoveredServer(s);
                    }
                    GUI.enabled = true;

                    GUILayout.EndHorizontal();
                }
            }

            GUILayout.EndScrollView();
        }

        private void DrawFallbackHostGameTab()
        {
            GUILayout.Label("<b>Create and Host a Game Session:</b>");
            GUILayout.Space(4);

            GUILayout.BeginVertical("box");

            GUILayout.Label("Lobby / Server Name:");
            hostLobbyName = GUILayout.TextField(hostLobbyName, 32);

            GUILayout.Space(4);
            GUILayout.Label("Connection Mode:");
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(hostMode == HostMode.Relay, "Relay (Internet / Cross Wi-Fi)", "Button"))
            {
                hostMode = HostMode.Relay;
            }
            if (GUILayout.Toggle(hostMode == HostMode.DirectIP, "Direct IP (LAN / VPN)", "Button"))
            {
                hostMode = HostMode.DirectIP;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Max Players:", GUILayout.Width(90));
            if (GUILayout.Button("2", maxPlayersInput == 2 ? "Button" : "Toggle")) maxPlayersInput = 2;
            if (GUILayout.Button("3", maxPlayersInput == 3 ? "Button" : "Toggle")) maxPlayersInput = 3;
            if (GUILayout.Button("4", maxPlayersInput == 4 ? "Button" : "Toggle")) maxPlayersInput = 4;
            if (GUILayout.Button("6", maxPlayersInput == 6 ? "Button" : "Toggle")) maxPlayersInput = 6;
            GUILayout.EndHorizontal();

            if (hostMode == HostMode.DirectIP)
            {
                GUILayout.Space(4);
                GUILayout.BeginHorizontal();
                GUILayout.Label("Port:", GUILayout.Width(50));
                directPortInput = GUILayout.TextField(directPortInput, 6);
                GUILayout.EndHorizontal();
            }

            GUILayout.EndVertical();

            GUILayout.Space(12);

            GUI.enabled = !isConnecting;
            Color prevBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.3f, 0.9f, 0.3f);

            if (GUILayout.Button("Create Lobby & Start Hosting", GUILayout.Height(42)))
            {
                if (hostMode == HostMode.Relay)
                {
                    OnHostRelayClicked();
                }
                else
                {
                    OnHostDirectClicked();
                }
            }

            GUI.backgroundColor = prevBg;
            GUI.enabled = true;
        }

        private void DrawFallbackDirectConnectTab()
        {
            GUILayout.Label("<b>Direct Connection:</b>");
            GUILayout.Space(4);

            GUILayout.BeginVertical("box");
            GUILayout.Label("<b>Option 1: Join with Relay Code:</b>");
            joinCodeInput = GUILayout.TextField(joinCodeInput.ToUpper().Trim(), 12);

            GUI.enabled = !isConnecting && !string.IsNullOrEmpty(joinCodeInput);
            if (GUILayout.Button("Connect via Relay Code", GUILayout.Height(32)))
            {
                _ = StartRelayClientAsync(joinCodeInput);
            }
            GUI.enabled = true;
            GUILayout.EndVertical();

            GUILayout.Space(8);

            GUILayout.BeginVertical("box");
            GUILayout.Label("<b>Option 2: Direct IP / LAN / VPN:</b>");
            GUILayout.BeginHorizontal();
            GUILayout.Label("IP:", GUILayout.Width(30));
            directIpInput = GUILayout.TextField(directIpInput);
            GUILayout.Label("Port:", GUILayout.Width(35));
            directPortInput = GUILayout.TextField(directPortInput, 6, GUILayout.Width(60));
            GUILayout.EndHorizontal();

            GUILayout.Space(4);
            GUI.enabled = !isConnecting;
            if (GUILayout.Button("Connect via Direct IP", GUILayout.Height(32)))
            {
                OnJoinDirectIpClicked();
            }
            GUI.enabled = true;
            GUILayout.EndVertical();
        }

        #endregion

        #region Networking Operations

        private void JoinDiscoveredServer(DiscoveredServer server)
        {
            SetStatus($"Joining '{server.ServerName}'...");

            if (server.ConnectionType == "Relay")
            {
                _ = StartRelayClientAsync(server.Address);
            }
            else
            {
                StartDirectClient(server.Address, server.Port);
            }
        }

        private async Task EnsureServicesInitializedAsync()
        {
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
            {
                SetStatus("Initializing Unity Services...");
                await UnityServices.InitializeAsync();
            }

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                SetStatus("Signing in anonymously...");
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }
        }

        private async Task StartRelayHostAsync()
        {
            try
            {
                isConnecting = true;
                await EnsureServicesInitializedAsync();

                SetStatus("Allocating Relay server...");
                Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxPlayersInput);
                activeJoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

                var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
                transport.SetHostRelayData(
                    allocation.RelayServer.IpV4,
                    (ushort)allocation.RelayServer.Port,
                    allocation.AllocationIdBytes,
                    allocation.Key,
                    allocation.ConnectionData
                );

                bool started = NetworkManager.Singleton.StartHost();
                if (started)
                {
                    SetStatus($"Hosting via Relay! Code: {activeJoinCode}");

                    if (lobbyUI != null)
                    {
                        lobbyUI.ActiveJoinCode = activeJoinCode;
                        lobbyUI.ConnectionModeInfo = "Relay (Internet)";
                    }

                    if (SessionManager.Instance != null)
                    {
                        SessionManager.Instance.SetLobbyName(hostLobbyName);
                    }

                    LANDiscoveryManager.Instance?.StartBroadcasting(
                        hostLobbyName,
                        playerNameInput,
                        "Relay",
                        activeJoinCode,
                        0,
                        1,
                        maxPlayersInput
                    );
                }
                else
                {
                    SetStatus("Failed to start Host.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Relay] Failed to start host: {ex.Message}");
                SetStatus($"Relay Error: {ex.Message}");
            }
            finally
            {
                isConnecting = false;
            }
        }

        private async Task StartRelayClientAsync(string code)
        {
            try
            {
                isConnecting = true;
                await EnsureServicesInitializedAsync();

                SetStatus($"Joining Relay with code: {code}...");
                JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(code);

                var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
                transport.SetClientRelayData(
                    joinAllocation.RelayServer.IpV4,
                    (ushort)joinAllocation.RelayServer.Port,
                    joinAllocation.AllocationIdBytes,
                    joinAllocation.Key,
                    joinAllocation.ConnectionData,
                    joinAllocation.HostConnectionData
                );

                bool started = NetworkManager.Singleton.StartClient();
                if (started)
                {
                    activeJoinCode = code;
                    SetStatus("Connected via Relay!");

                    if (lobbyUI != null)
                    {
                        lobbyUI.ActiveJoinCode = code;
                        lobbyUI.ConnectionModeInfo = "Relay (Internet)";
                    }
                }
                else
                {
                    SetStatus("Failed to start Client.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Relay] Failed to join with code: {ex.Message}");
                SetStatus($"Join Error: {ex.Message}");
            }
            finally
            {
                isConnecting = false;
            }
        }

        private void StartDirectHost()
        {
            string portStr = directPortInputField != null ? directPortInputField.text : directPortInput;
            ushort.TryParse(portStr, out ushort port);
            if (port == 0) port = 7777;

            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (transport != null)
            {
                transport.SetConnectionData("0.0.0.0", port);
            }

            bool started = NetworkManager.Singleton.StartHost();
            if (started)
            {
                SetStatus($"Hosting Direct IP on port {port}!");

                if (lobbyUI != null)
                {
                    lobbyUI.ActiveJoinCode = "";
                    lobbyUI.ConnectionModeInfo = $"Direct IP ({port})";
                }

                if (SessionManager.Instance != null)
                {
                    SessionManager.Instance.SetLobbyName(hostLobbyName);
                }

                LANDiscoveryManager.Instance?.StartBroadcasting(
                    hostLobbyName,
                    playerNameInput,
                    "DirectIP",
                    "127.0.0.1",
                    port,
                    1,
                    maxPlayersInput
                );
            }
            else
            {
                SetStatus("Failed to start Host.");
            }
        }

        private void StartDirectClient(string ip, ushort port)
        {
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (transport != null)
            {
                transport.SetConnectionData(ip, port);
            }

            bool started = NetworkManager.Singleton.StartClient();
            if (started)
            {
                SetStatus($"Connecting to {ip}:{port}...");

                if (lobbyUI != null)
                {
                    lobbyUI.ActiveJoinCode = "";
                    lobbyUI.ConnectionModeInfo = $"Direct IP ({ip}:{port})";
                }
            }
            else
            {
                SetStatus("Failed to connect to host.");
            }
        }

        #endregion
    }
}
