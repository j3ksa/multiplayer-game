# Multiplayer Game

## Overview

Multiplayer Game is a Unity PC project for a multiplayer tower defense game with FPS elements.
Players control a character on top of a tower, shoot enemies from above, and compete or cooperate
depending on the selected mode.

Planned modes:

- PvE survival competition.
- PvP attack and defend.
- Co-op defense.

## Current Status

The repository contains the working Unity 6 project with baseline multiplayer networking implemented:

- **Engine**: Unity 6 (`6000.4.2f1`).
- **Networking Stack**:
  - Unity Netcode for GameObjects (NGO) `2.13.3`.
  - Unity Transport (UTP) `2.7.4`.
  - Unity Relay `1.1.1` & Unity Authentication `3.3.4` (for cross-network WAN play without port forwarding).
- **Core Scripts**:
  - `Assets/Scripts/UI/ConnectionHUD.cs`: Complete match browser HUD with **Server List (browsing available games)**, **Host Game creation**, and **Direct Connect**.
  - `Assets/Scripts/UI/LobbyUI.cs`: Synchronized pre-match lobby room displaying player roster, ready statuses, and host match launch trigger.
  - `Assets/Scripts/Networking/LANDiscoveryManager.cs`: UDP broadcast discovery system for detecting active LAN and loopback sessions in real time.
  - `Assets/Scripts/Networking/SessionManager.cs`: Host-authoritative synchronized session player list, ready checks, and scene transition coordinator.
  - `Assets/Scripts/Networking/PlayerData.cs`: Network-serializable player struct.
  - `Assets/Scripts/Editor/BuildHelper.cs`: 1-click Windows standalone build menu tool.

## How to Run & Test

### In the Unity Editor
1. Open this repository in Unity Editor `6000.4.2f1`.
2. Open `Assets/Scenes/SampleScene.unity`.
3. Press **Play**.
4. The **Multiplayer Session HUD** will appear on screen.

### Connecting Across Different Wi-Fi Networks (Unity Relay)
1. Link your Unity project: In Unity, go to **Edit > Project Settings > Services** and link or create a Project ID (free).
2. **Device 1 (Host)**: Click **Host New Match (Generate Code)**. The HUD will show a 6-character Join Code (e.g. `ABC123`). Copy and send it to player 2.
3. **Device 2 (Client)**: Paste the Join Code and click **Join Match with Code**.

### Building for Windows
In the Unity Editor top menu, select:
**`Tools` -> `Multiplayer` -> `Build Windows Standalone (.exe)`**
This compiles the player directly into the `Builds/` folder and opens it in Windows Explorer.

## Ownership

Current repository contributors:

- j3ksa
- KacperX852
- Admiel
- Jerzynek
