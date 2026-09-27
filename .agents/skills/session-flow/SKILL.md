---
name: session-flow
description: "Use when: you need the required player flow for host, join, lobby ready-up, gameplay scene loading, match ending, or post-match transitions."
---

# Session Flow

## Required Flow

The project should support this end-to-end multiplayer loop:

1. Main menu.
2. Host game or join game.
3. Server browser or session list.
4. Pre-match lobby.
5. Ready system for all players.
6. Host-controlled match start.
7. Gameplay scene load.
8. Automatic round end when the objective is resolved.
9. Result summary.
10. Restart, next level, or return to lobby.

## Main Menu Expectations

The main menu should expose:

- Host game.
- Join game.
- Server browser.
- Exit game.

## Lobby Expectations

The lobby should support:

- Player list.
- Selected mode.
- Selected map or level.
- Ready state.
- Host-controlled start.

The gameplay scene should load only when every participant is ready and the host confirms start.

## End-of-Match Expectations

When the objective is resolved, the round should stop for all players and show a shared summary
state before any restart or return action.