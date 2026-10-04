using System;
using System.Collections.Generic;
using MultiplayerGame.Networking;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace MultiplayerGame.UI
{
    /// <summary>
    /// Pre-match lobby UI that displays connected players, synchronized ready states,
    /// and host-controlled match launch.
    /// Supports both:
    /// 1. Custom Canvas UI elements assigned via Inspector (Buttons, Texts, Roster Container, Prefabs).
    /// 2. Automatic fallback IMGUI if no custom Canvas is assigned.
    /// </summary>
    public class LobbyUI : MonoBehaviour
    {
        public static LobbyUI Instance { get; private set; }

        [Header("Custom Canvas UI Elements (Optional)")]
        [Tooltip("Root GameObject/Panel for the custom lobby Canvas. If assigned, Canvas UI will be driven automatically.")]
        [SerializeField] private GameObject lobbyCanvasPanel;

        [Header("Lobby Metadata")]
        [SerializeField] private TMP_Text lobbyTitleText;
        [SerializeField] private TMP_Text connectionModeText;
        [SerializeField] private TMP_Text joinCodeText;
        [SerializeField] private Button copyCodeButton;
        [SerializeField] private TMP_Text playerCountText;

        [Header("Player Roster Container")]
        [Tooltip("Transform container with a LayoutGroup to spawn player roster entries.")]
        [SerializeField] private Transform playerListContainer;
        [Tooltip("Prefab containing PlayerEntryUI component.")]
        [SerializeField] private GameObject playerEntryPrefab;

        [Header("Action Buttons & Status")]
        [SerializeField] private Button readyButton;
        [SerializeField] private TMP_Text readyButtonText;
        [SerializeField] private Button startMatchButton;
        [SerializeField] private TMP_Text startMatchButtonText;
        [SerializeField] private Button leaveLobbyButton;
        [SerializeField] private TMP_Text statusMessageText;

        [Header("Fallback Settings")]
        [Tooltip("Enable IMGUI fallback if no custom Canvas panel is assigned.")]
        [SerializeField] private bool useFallbackOnGUI = true;
        [SerializeField] private int windowWidth = 460;
        [SerializeField] private int windowHeight = 440;

        public string ActiveJoinCode { get; set; } = "";
        public string ConnectionModeInfo { get; set; } = "Online";

        private Vector2 playerScrollPos;
        private string statusMessage = "";
        private readonly List<GameObject> spawnedEntries = new List<GameObject>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void Start()
        {
            // Bind button listeners if assigned in Inspector
            if (copyCodeButton != null) copyCodeButton.onClick.AddListener(OnCopyCodeClicked);
            if (readyButton != null) readyButton.onClick.AddListener(OnToggleReadyClicked);
            if (startMatchButton != null) startMatchButton.onClick.AddListener(OnStartMatchClicked);
            if (leaveLobbyButton != null) leaveLobbyButton.onClick.AddListener(OnLeaveLobbyClicked);

            if (SessionManager.Instance != null)
            {
                SessionManager.Instance.OnPlayersChanged += RefreshCustomUI;
                SessionManager.Instance.OnAllPlayersReadyStatusChanged += HandleAllPlayersReadyChanged;
            }
        }

        private void OnDestroy()
        {
            if (copyCodeButton != null) copyCodeButton.onClick.RemoveListener(OnCopyCodeClicked);
            if (readyButton != null) readyButton.onClick.RemoveListener(OnToggleReadyClicked);
            if (startMatchButton != null) startMatchButton.onClick.RemoveListener(OnStartMatchClicked);
            if (leaveLobbyButton != null) leaveLobbyButton.onClick.RemoveListener(OnLeaveLobbyClicked);

            if (SessionManager.Instance != null)
            {
                SessionManager.Instance.OnPlayersChanged -= RefreshCustomUI;
                SessionManager.Instance.OnAllPlayersReadyStatusChanged -= HandleAllPlayersReadyChanged;
            }
        }

        private void Update()
        {
            bool isOnlineInLobby = NetworkManager.Singleton != null
                && (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer)
                && (SessionManager.Instance == null || !SessionManager.Instance.IsMatchStarted);

            if (lobbyCanvasPanel != null)
            {
                if (lobbyCanvasPanel.activeSelf != isOnlineInLobby)
                {
                    lobbyCanvasPanel.SetActive(isOnlineInLobby);
                    if (isOnlineInLobby)
                    {
                        RefreshCustomUI();
                    }
                }
            }
        }

        #region Public UI Event Handlers (Attachable in Inspector)

        public void OnToggleReadyClicked()
        {
            if (SessionManager.Instance != null)
            {
                bool isReady = SessionManager.Instance.IsLocalPlayerReady();
                SessionManager.Instance.SetReadyRpc(!isReady);
            }
        }

        public void OnStartMatchClicked()
        {
            if (SessionManager.Instance != null)
            {
                if (SessionManager.Instance.AreAllPlayersReady())
                {
                    SetStatus("Starting match...");
                    SessionManager.Instance.StartMatch();
                }
                else
                {
                    SetStatus("Cannot start: Not all players are ready!");
                }
            }
        }

        public void OnLeaveLobbyClicked()
        {
            LeaveLobby();
        }

        public void OnCopyCodeClicked()
        {
            if (!string.IsNullOrEmpty(ActiveJoinCode))
            {
                GUIUtility.systemCopyBuffer = ActiveJoinCode;
                SetStatus("Join code copied to clipboard!");
            }
        }

        public void SetStatus(string message)
        {
            statusMessage = message;
            if (statusMessageText != null)
            {
                statusMessageText.text = message;
            }
        }

        #endregion

        #region Custom Canvas Population

        public void RefreshCustomUI()
        {
            if (lobbyCanvasPanel == null || !lobbyCanvasPanel.activeSelf) return;

            // Metadata updates
            if (lobbyTitleText != null && SessionManager.Instance != null)
            {
                lobbyTitleText.text = SessionManager.Instance.CurrentLobbyName;
            }

            if (connectionModeText != null)
            {
                connectionModeText.text = ConnectionModeInfo;
            }

            if (joinCodeText != null)
            {
                joinCodeText.text = string.IsNullOrEmpty(ActiveJoinCode) ? "N/A (Direct)" : ActiveJoinCode;
            }

            if (SessionManager.Instance == null || NetworkManager.Singleton == null) return;

            int playerCount = SessionManager.Instance.Players.Count;
            if (playerCountText != null)
            {
                playerCountText.text = $"{playerCount} Players";
            }

            // Populate roster entries
            if (playerListContainer != null && playerEntryPrefab != null)
            {
                // Clear existing
                for (int i = 0; i < spawnedEntries.Count; i++)
                {
                    if (spawnedEntries[i] != null) Destroy(spawnedEntries[i]);
                }
                spawnedEntries.Clear();

                ulong localClientId = NetworkManager.Singleton.LocalClientId;
                for (int i = 0; i < playerCount; i++)
                {
                    var p = SessionManager.Instance.Players[i];
                    GameObject entryObj = Instantiate(playerEntryPrefab, playerListContainer);
                    spawnedEntries.Add(entryObj);

                    var entryUI = entryObj.GetComponent<PlayerEntryUI>();
                    if (entryUI != null)
                    {
                        entryUI.Setup(p, p.ClientId == localClientId);
                    }
                }
            }

            // Update button states
            bool isHost = NetworkManager.Singleton.IsHost;
            bool isLocalReady = SessionManager.Instance.IsLocalPlayerReady();
            bool allReady = SessionManager.Instance.AreAllPlayersReady();

            if (readyButton != null)
            {
                readyButton.gameObject.SetActive(!isHost);
            }

            if (readyButtonText != null)
            {
                readyButtonText.text = isLocalReady ? "Cancel Ready" : "Ready Up!";
            }

            if (startMatchButton != null)
            {
                startMatchButton.gameObject.SetActive(isHost);
                startMatchButton.interactable = allReady;
            }

            if (startMatchButtonText != null)
            {
                startMatchButtonText.text = allReady ? "START GAME" : "Waiting for players...";
            }
        }

        private void HandleAllPlayersReadyChanged(bool allReady)
        {
            if (startMatchButton != null)
            {
                startMatchButton.interactable = allReady;
            }
            if (startMatchButtonText != null)
            {
                startMatchButtonText.text = allReady ? "START GAME" : "Waiting for players...";
            }
        }

        #endregion

        #region Fallback IMGUI Support

        private void OnGUI()
        {
            // If custom canvas panel is assigned, do not draw fallback IMGUI
            if (lobbyCanvasPanel != null || !useFallbackOnGUI)
            {
                return;
            }

            if (NetworkManager.Singleton == null || (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer))
            {
                return;
            }

            if (SessionManager.Instance != null && SessionManager.Instance.IsMatchStarted)
            {
                return;
            }

            float x = (Screen.width - windowWidth) * 0.5f;
            float y = (Screen.height - windowHeight) * 0.5f;
            Rect windowRect = new Rect(Mathf.Max(10, x), Mathf.Max(10, y), windowWidth, windowHeight);

            string title = SessionManager.Instance != null && !string.IsNullOrEmpty(SessionManager.Instance.CurrentLobbyName)
                ? $"Lobby: {SessionManager.Instance.CurrentLobbyName}"
                : "Game Lobby";

            GUILayout.BeginArea(windowRect, title, GUI.skin.window);
            DrawFallbackLobbyContent();
            GUILayout.EndArea();
        }

        private void DrawFallbackLobbyContent()
        {
            if (SessionManager.Instance == null)
            {
                GUILayout.Label("Initializing Session Manager...", GUILayout.Height(30));
                DrawLeaveButton();
                return;
            }

            // Header Section
            GUILayout.BeginVertical("box");
            GUILayout.Label($"Mode: <b>{ConnectionModeInfo}</b> | Players: <b>{SessionManager.Instance.Players.Count}</b>");

            if (!string.IsNullOrEmpty(ActiveJoinCode))
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Relay Code: <color=cyan><b>{ActiveJoinCode}</b></color>");
                if (GUILayout.Button("Copy Code", GUILayout.Width(90), GUILayout.Height(22)))
                {
                    OnCopyCodeClicked();
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();

            GUILayout.Space(8);
            GUILayout.Label("<b>Connected Players:</b>");

            // Players Roster List
            playerScrollPos = GUILayout.BeginScrollView(playerScrollPos, "box", GUILayout.Height(170));
            ulong localClientId = NetworkManager.Singleton.LocalClientId;
            int readyCount = 0;

            for (int i = 0; i < SessionManager.Instance.Players.Count; i++)
            {
                var p = SessionManager.Instance.Players[i];
                if (p.IsReady) readyCount++;

                bool isLocal = p.ClientId == localClientId;

                GUILayout.BeginHorizontal("box");

                string roleTag = p.IsHost ? "<color=#FFD700>[HOST]</color>" : "<color=#87CEEB>[PLAYER]</color>";
                string youTag = isLocal ? " <color=lime>(YOU)</color>" : "";
                GUILayout.Label($"{roleTag} <b>{p.PlayerName}</b>{youTag}", GUILayout.Width(240));

                GUILayout.FlexibleSpace();

                if (p.IsReady)
                {
                    GUI.color = Color.green;
                    GUILayout.Label("<b>[ READY ]</b>", GUILayout.Width(90));
                    GUI.color = Color.white;
                }
                else
                {
                    GUI.color = new Color(1f, 0.65f, 0f);
                    GUILayout.Label("<b>[ NOT READY ]</b>", GUILayout.Width(90));
                    GUI.color = Color.white;
                }

                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();

            GUILayout.Space(6);

            if (!string.IsNullOrEmpty(statusMessage))
            {
                GUI.color = Color.yellow;
                GUILayout.Label(statusMessage);
                GUI.color = Color.white;
            }

            GUILayout.FlexibleSpace();

            DrawControls(readyCount);
        }

        private void DrawControls(int readyCount)
        {
            bool isHost = NetworkManager.Singleton.IsHost;
            bool isLocalReady = SessionManager.Instance.IsLocalPlayerReady();
            int totalPlayers = SessionManager.Instance.Players.Count;
            bool allReady = SessionManager.Instance.AreAllPlayersReady();

            GUILayout.BeginVertical("box");

            if (!isHost)
            {
                string readyBtnText = isLocalReady ? "Cancel Ready (Mark Not Ready)" : "Ready Up!";
                Color prevBg = GUI.backgroundColor;
                GUI.backgroundColor = isLocalReady ? new Color(1f, 0.4f, 0.4f) : new Color(0.4f, 1f, 0.4f);

                if (GUILayout.Button(readyBtnText, GUILayout.Height(38)))
                {
                    OnToggleReadyClicked();
                }
                GUI.backgroundColor = prevBg;

                GUILayout.Label("<color=gray>Waiting for the host to start the game...</color>", GUILayout.Height(20));
            }
            else
            {
                Color prevBg = GUI.backgroundColor;
                if (allReady)
                {
                    GUI.backgroundColor = new Color(0.2f, 0.9f, 0.2f);
                }

                GUI.enabled = allReady;
                string startText = allReady
                    ? "START GAME (All Players Ready!)"
                    : $"Waiting for players ({readyCount}/{totalPlayers} Ready)...";

                if (GUILayout.Button(startText, GUILayout.Height(40)))
                {
                    OnStartMatchClicked();
                }

                GUI.enabled = true;
                GUI.backgroundColor = prevBg;
            }

            GUILayout.Space(4);
            DrawLeaveButton();

            GUILayout.EndVertical();
        }

        private void DrawLeaveButton()
        {
            bool isHost = NetworkManager.Singleton.IsHost;
            string btnText = isHost ? "Close Lobby & Disconnect" : "Leave Lobby";

            if (GUILayout.Button(btnText, GUILayout.Height(28)))
            {
                OnLeaveLobbyClicked();
            }
        }

        #endregion

        public void LeaveLobby()
        {
            statusMessage = "";
            ActiveJoinCode = "";

            if (LANDiscoveryManager.Instance != null)
            {
                LANDiscoveryManager.Instance.StopBroadcasting();
            }

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                NetworkManager.Singleton.Shutdown();
            }
        }
    }
}
