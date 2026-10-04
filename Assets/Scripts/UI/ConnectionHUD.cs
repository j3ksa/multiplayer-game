using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

namespace MultiplayerGame.UI
{
    /// <summary>
    /// Complete connection HUD supporting both:
    /// 1. Unity Relay (Join Codes for playing over different Wi-Fi networks / Internet).
    /// 2. Direct IP (LAN, Tailscale / VPN, or Router Port Forwarding).
    /// </summary>
    public class ConnectionHUD : MonoBehaviour
    {
        private enum Mode { Relay, DirectIP }
        private Mode selectedMode = Mode.Relay;

        [Header("Relay Settings")]
        [SerializeField] private int maxConnections = 4;

        [Header("Direct IP Settings")]
        [SerializeField] private string targetIp = "127.0.0.1";
        [SerializeField] private ushort targetPort = 7777;

        private string joinCodeInput = "";
        private string activeJoinCode = "";
        private string ipInput;
        private string portInput;
        private string statusMessage = "";
        private bool isConnecting = false;

        private void Start()
        {
            ipInput = targetIp;
            portInput = targetPort.ToString();
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(20, 20, 320, 430), "Multiplayer Session HUD", GUI.skin.window);

            if (NetworkManager.Singleton == null)
            {
                GUILayout.Label("NetworkManager not found.");
                GUILayout.EndArea();
                return;
            }

            if (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
            {
                DrawOfflineUI();
            }
            else
            {
                DrawOnlineUI();
            }

            if (!string.IsNullOrEmpty(statusMessage))
            {
                GUILayout.Space(10);
                GUI.color = Color.yellow;
                GUILayout.Label(statusMessage);
                GUI.color = Color.white;
            }

            GUILayout.EndArea();
        }

        private void DrawOfflineUI()
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(selectedMode == Mode.Relay, "Relay (Internet / Wi-Fi)", "Button"))
            {
                selectedMode = Mode.Relay;
            }
            if (GUILayout.Toggle(selectedMode == Mode.DirectIP, "Direct IP (LAN / VPN)", "Button"))
            {
                selectedMode = Mode.DirectIP;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            if (selectedMode == Mode.Relay)
            {
                DrawRelayControls();
            }
            else
            {
                DrawDirectIpControls();
            }
        }

        private void DrawRelayControls()
        {
            GUILayout.Label("<b>Play Across Different Wi-Fi (Relay):</b>");
            GUILayout.Label("No port forwarding required.");

            GUILayout.Space(5);
            GUI.enabled = !isConnecting;

            if (GUILayout.Button("Host New Match (Generate Code)", GUILayout.Height(35)))
            {
                _ = StartRelayHostAsync();
            }

            GUILayout.Space(10);
            GUILayout.Label("Enter Join Code:");
            joinCodeInput = GUILayout.TextField(joinCodeInput.ToUpper().Trim());

            if (GUILayout.Button("Join Match with Code", GUILayout.Height(35)))
            {
                if (string.IsNullOrEmpty(joinCodeInput))
                {
                    statusMessage = "Please enter a valid Join Code.";
                }
                else
                {
                    _ = StartRelayClientAsync(joinCodeInput);
                }
            }

            GUI.enabled = true;
        }

        private void DrawDirectIpControls()
        {
            GUILayout.Label("<b>Direct Network Connection:</b>");
            GUILayout.Label("For LAN, Tailscale/VPN, or Port Forwarding.");

            GUILayout.Space(5);
            GUILayout.Label("Host IP:");
            ipInput = GUILayout.TextField(ipInput);

            GUILayout.Label("Port:");
            portInput = GUILayout.TextField(portInput);

            GUILayout.Space(8);
            GUI.enabled = !isConnecting;

            if (GUILayout.Button("Start Host (Direct)", GUILayout.Height(35)))
            {
                statusMessage = "Starting Host...";
                ConfigureDirectTransport();
                bool started = NetworkManager.Singleton.StartHost();
                statusMessage = started ? "" : "Failed to start Host.";
            }

            if (GUILayout.Button("Join Client (Direct IP)", GUILayout.Height(35)))
            {
                statusMessage = "Connecting to Host...";
                ConfigureDirectTransport();
                bool started = NetworkManager.Singleton.StartClient();
                statusMessage = started ? "" : "Failed to start Client.";
            }

            GUI.enabled = true;
        }

        private void DrawOnlineUI()
        {
            string role = NetworkManager.Singleton.IsHost ? "Host (Server + Client)" :
                          NetworkManager.Singleton.IsServer ? "Dedicated Server" : "Client";

            GUILayout.Label($"Role: <b>{role}</b>");
            GUILayout.Label($"Local Client ID: {NetworkManager.Singleton.LocalClientId}");
            GUILayout.Label($"Connected Players: {NetworkManager.Singleton.ConnectedClientsIds.Count}");

            if (!string.IsNullOrEmpty(activeJoinCode))
            {
                GUILayout.Space(10);
                GUILayout.Label($"<b>Relay Join Code:</b> <color=cyan>{activeJoinCode}</color>");

                if (GUILayout.Button("Copy Join Code to Clipboard", GUILayout.Height(25)))
                {
                    GUIUtility.systemCopyBuffer = activeJoinCode;
                    statusMessage = "Join code copied to clipboard!";
                }
            }

            GUILayout.Space(15);

            if (GUILayout.Button("Disconnect", GUILayout.Height(35)))
            {
                NetworkManager.Singleton.Shutdown();
                activeJoinCode = "";
                statusMessage = "Disconnected.";
            }
        }

        #region Relay Implementation

        private async Task EnsureServicesInitializedAsync()
        {
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
            {
                statusMessage = "Initializing Unity Services...";
                await UnityServices.InitializeAsync();
            }

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                statusMessage = "Signing in anonymously...";
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }
        }

        private async Task StartRelayHostAsync()
        {
            try
            {
                isConnecting = true;
                await EnsureServicesInitializedAsync();

                statusMessage = "Allocating Relay server...";
                Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxConnections);

                activeJoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
                Debug.Log($"[Relay] Host allocation created. Join Code: {activeJoinCode}");

                var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
                transport.SetHostRelayData(
                    allocation.RelayServer.IpV4,
                    (ushort)allocation.RelayServer.Port,
                    allocation.AllocationIdBytes,
                    allocation.Key,
                    allocation.ConnectionData
                );

                NetworkManager.Singleton.StartHost();
                statusMessage = $"Hosting via Relay! Code: {activeJoinCode}";
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Relay] Failed to start host: {ex.Message}");
                statusMessage = $"Relay Error: {ex.Message}";
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

                statusMessage = $"Joining Relay with code: {code}...";
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

                NetworkManager.Singleton.StartClient();
                activeJoinCode = code;
                statusMessage = "Connected via Relay!";
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Relay] Failed to join with code: {ex.Message}");
                statusMessage = $"Join Error: {ex.Message}";
            }
            finally
            {
                isConnecting = false;
            }
        }

        #endregion

        #region Direct IP Implementation

        private void ConfigureDirectTransport()
        {
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (transport != null)
            {
                ushort.TryParse(portInput, out ushort port);
                if (port == 0) port = 7777;
                transport.SetConnectionData(ipInput, port);
            }
        }

        #endregion
    }
}
