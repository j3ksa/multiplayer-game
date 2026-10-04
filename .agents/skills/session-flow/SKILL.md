---
name: session-flow
description: "Use when: you need the required player flow for host, join, lobby ready-up, gameplay scene loading, match ending, or post-match transitions."
---

# Session Flow

## Connection Methods

The project supports two connection pathways:

1. **Unity Relay (WAN / Different Wi-Fi)**:
   - Host clicks **Host New Match** -> Generates 6-character Join Code (e.g. `ABC123`).
   - Host shares the Join Code with players.
   - Clients enter the Join Code and click **Join Match with Code**.
   - Connects through Unity Relay servers without router port forwarding.
2. **Direct IP (LAN / VPN / Port Forwarding)**:
   - Host starts direct session on port `7777`.
   - Clients connect by IP address (e.g. `127.0.0.1`, LAN `192.168.x.x`, or Tailscale VPN IP).

## Required Flow

The project supports this end-to-end multiplayer loop:

1. Main menu / Connection HUD.
2. Host session (Relay Join Code or Direct IP) or join existing session.
3. Pre-match lobby & player synchronization.
4. Ready system for all players.
5. Host-controlled match start.
6. Gameplay scene load via `NetworkSceneManager`.
7. Automatic round end when the objective is resolved.
8. Result summary.
9. Restart, next level, or return to lobby.

## Main Menu Expectations

The main menu should expose:

- Host game (Relay or Direct IP).
- Join game (Join Code input or IP/port input).
- Exit game.

## Lobby Expectations

The lobby should support:

- Synchronized player list (`SessionManager` + `PlayerData`).
- Selected mode.
- Selected map or level.
- Ready state per client.
- Host-controlled start.

The gameplay scene should load only when every participant is ready and the host confirms start.

## End-of-Match Expectations

When the objective is resolved, the round should stop for all players and show a shared summary
state before any restart or return action.