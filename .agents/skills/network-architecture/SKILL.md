---
name: network-architecture
description: "Use when: you need the recommended Unity multiplayer stack, authority model, transport guidance, or network responsibilities for future implementation work."
---

# Network Architecture

## Recommended Stack

- Unity Netcode for GameObjects (NGO) `2.13.3`.
- Unity Transport (UTP) `2.7.4`.
- Unity Relay `1.1.1` and Unity Authentication `3.3.4` (for cross-network WAN play without router port forwarding).
- Host-client session topology.

## Critical NGO Component Hierarchy Rule

In Netcode for GameObjects (NGO), **`NetworkManager` is strictly prohibited from having any `NetworkBehaviour` component attached to its root GameObject or any of its children**.

- `[NetworkManager]` GameObject must ONLY contain:
  - `NetworkManager`
  - `UnityTransport`
  - Standard `MonoBehaviour` utility components (such as `ConnectionHUD`).
- Any networked gameplay script inheriting from `NetworkBehaviour` (such as `SessionManager` or player controllers) must be placed on its own separate GameObject that contains a `NetworkObject` component.

## Cross-Network Connectivity (WAN & Wi-Fi)

To allow players on different Wi-Fi networks (different NATs/firewalls) to connect without manual router port forwarding:

1. **Unity Relay (Primary)**:
   - Host requests an allocation from Unity Relay via `RelayService.Instance.CreateAllocationAsync()`.
   - Host retrieves a 6-character Join Code via `GetJoinCodeAsync()`.
   - Host configures `UnityTransport` using `transport.SetHostRelayData(...)`.
   - Client joins using `RelayService.Instance.JoinAllocationAsync(joinCode)` and `transport.SetClientRelayData(...)`.
   - Relay proxies the UDP traffic securely over the internet.
2. **Direct IP (Fallback)**:
   - Supports LAN, VPN (e.g. Tailscale / ZeroTier), or manual router port forwarding on port `7777`.

## Authority Model

The host should remain authoritative for gameplay-critical shared state.

Host authority includes:

- Lobby membership and ready state.
- Match start.
- Enemy wave spawning and progression.
- Enemy and player health resolution.
- Tower placement validation.
- Shared objectives.
- Score and end-of-round summary.

Clients should control local input and presentation, but the host should confirm accepted match
state.

## Protocol Guidance

Unity Transport uses UDP-style networking suitable for real-time gameplay.

- Use unreliable updates for transient state such as movement snapshots and look rotation.
- Use reliable delivery for critical events such as ready toggles, scene transitions, tower
  placement, score updates, damage confirmation, and end-of-match results.

## Why This Direction

This stack fits a student Unity project because it minimizes infrastructure overhead while still
supporting real-time multiplayer design over both LAN and the public internet.

## Open Decisions

- Match discovery / lobby listing backend (Lobby service vs. Join Code sharing).
- Final player count per session.
- Whether the project will stay host-only or later add dedicated-server support.