using System;
using MultiplayerGame.Networking;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MultiplayerGame.UI
{
    /// <summary>
    /// Attach this component to a UI Prefab for each server item in the Server List.
    /// Customize visuals freely in the Unity Editor / Canvas.
    /// </summary>
    public class ServerEntryUI : MonoBehaviour
    {
        [Header("UI Element References")]
        [SerializeField] private TMP_Text serverNameText;
        [SerializeField] private TMP_Text hostNameText;
        [SerializeField] private TMP_Text connectionTypeText;
        [SerializeField] private TMP_Text playerCountText;
        [SerializeField] private Button joinButton;
        [SerializeField] private TMP_Text joinButtonText;

        private DiscoveredServer boundServer;
        private Action<DiscoveredServer> onJoinCallback;

        private void Awake()
        {
            if (joinButton != null)
            {
                joinButton.onClick.AddListener(HandleJoinClicked);
            }
        }

        private void OnDestroy()
        {
            if (joinButton != null)
            {
                joinButton.onClick.RemoveListener(HandleJoinClicked);
            }
        }

        /// <summary>
        /// Populates this UI element with discovered server information.
        /// </summary>
        public void Setup(DiscoveredServer server, Action<DiscoveredServer> joinAction)
        {
            boundServer = server;
            onJoinCallback = joinAction;

            if (serverNameText != null)
            {
                serverNameText.text = server.ServerName;
            }

            if (hostNameText != null)
            {
                hostNameText.text = $"Host: {server.HostPlayerName}";
            }

            if (connectionTypeText != null)
            {
                connectionTypeText.text = $"[{server.ConnectionType}]";
            }

            if (playerCountText != null)
            {
                playerCountText.text = $"{server.CurrentPlayers} / {server.MaxPlayers}";
            }

            bool isFull = server.CurrentPlayers >= server.MaxPlayers;
            if (joinButton != null)
            {
                joinButton.interactable = !isFull;
            }

            if (joinButtonText != null)
            {
                joinButtonText.text = isFull ? "Full" : "Join";
            }
        }

        private void HandleJoinClicked()
        {
            if (boundServer != null && onJoinCallback != null)
            {
                onJoinCallback.Invoke(boundServer);
            }
        }
    }
}
