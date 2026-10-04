using MultiplayerGame.Networking;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MultiplayerGame.UI
{
    /// <summary>
    /// Attach this component to a UI Prefab for each player item in the Lobby player list.
    /// Customize visuals freely in the Unity Editor / Canvas.
    /// </summary>
    public class PlayerEntryUI : MonoBehaviour
    {
        [Header("UI Element References")]
        [SerializeField] private TMP_Text playerNameText;
        [SerializeField] private TMP_Text roleBadgeText;
        [SerializeField] private TMP_Text readyBadgeText;
        [SerializeField] private GameObject localPlayerIndicator;

        [Header("Status Visuals")]
        [SerializeField] private Color readyColor = new Color(0.2f, 0.85f, 0.2f);
        [SerializeField] private Color notReadyColor = new Color(1f, 0.65f, 0.1f);
        [SerializeField] private Color hostRoleColor = new Color(1f, 0.84f, 0f);
        [SerializeField] private Color playerRoleColor = new Color(0.53f, 0.81f, 0.98f);

        /// <summary>
        /// Populates this UI element with player information.
        /// </summary>
        public void Setup(PlayerData player, bool isLocalPlayer)
        {
            if (playerNameText != null)
            {
                playerNameText.text = player.PlayerName.ToString();
            }

            if (roleBadgeText != null)
            {
                roleBadgeText.text = player.IsHost ? "HOST" : "PLAYER";
                roleBadgeText.color = player.IsHost ? hostRoleColor : playerRoleColor;
            }

            if (readyBadgeText != null)
            {
                readyBadgeText.text = player.IsReady ? "READY" : "NOT READY";
                readyBadgeText.color = player.IsReady ? readyColor : notReadyColor;
            }

            if (localPlayerIndicator != null)
            {
                localPlayerIndicator.SetActive(isLocalPlayer);
            }
        }
    }
}
