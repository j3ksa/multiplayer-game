# AGENTS.md

## Purpose

This file is a lightweight blueprint for future AI contributors working on this repository.

The repository now contains the initial Unity project scaffold. Future code changes should treat the
guidance in this file and the skill documents under `.agents/skills` as the current architectural
direction, not as proof that the gameplay systems already exist.

## Project Intent

The project is planned as a Unity multiplayer tower defense game for PC with FPS elements.

Core concept:

- The player controls a character on top of a tower.
- The player shoots at enemies from an elevated position.
- The game supports PvE competitive survival, PvP attack-and-defend, and co-op defense.

## Current Repository Maturity

- The Unity project scaffold exists on Unity 6 (`6000.4.2f1`).
- Multiplayer networking baseline is installed (`com.unity.netcode.gameobjects`, `com.unity.transport`, `com.unity.services.relay`, `com.unity.services.authentication`, `com.unity.services.core`).
- Core networking scripts are implemented under `Assets/Scripts/`:
  - `Assets/Scripts/Networking/`: `NetworkBootstrap.cs`, `SessionManager.cs`, `PlayerData.cs`.
  - `Assets/Scripts/UI/`: `ConnectionHUD.cs` supporting both Unity Relay (Join Codes) and Direct IP (LAN/VPN).
  - `Assets/Scripts/Editor/`: `BuildHelper.cs` for 1-click Windows standalone builds.
- Documentation and skill files remain the source of truth for architecture assumptions.

## Recommended Technical Direction

Until the team makes a different decision, assume the project is targeting:

- Unity on PC (Unity 6 `6000.4.2f1`).
- C# gameplay code.
- Unity Netcode for GameObjects (NGO).
- Unity Transport (UTP).
- Unity Relay and Authentication (for cross-network WAN play without router port forwarding).
- Host-client topology with host-authoritative shared game state.

The host should be treated as authoritative for:

- Lobby readiness.
- Match start.
- Enemy wave control.
- Health and defeat state.
- Tower placement validation.
- Score and end-of-round summary.

## AI Working Rules

When contributing to this repository in the future:

- Prefer small, reversible changes.
- **Critical NGO Hierarchy Rule**: Never add a `NetworkBehaviour` to the `NetworkManager` GameObject or any of its children. `NetworkManager` must only host standard `MonoBehaviour` components (like `ConnectionHUD`) and `UnityTransport`. Networked state managers (`NetworkBehaviour`) must reside on their own separate GameObjects with a `NetworkObject`.
- Avoid adding unnecessary packages or UI dependencies (e.g., `com.unity.ugui`) unless explicitly requested and confirmed.
- Keep gameplay-critical state authoritative on the host unless the project architecture changes.
- Distinguish clearly between local presentation logic and networked game state.
- Update README.md when architectural assumptions materially change.

## Expected Future Structure

The final repository will likely gain a Unity-oriented structure, but this is still provisional.

Reasonable future placeholders include:

- Assets/Scenes
- Assets/Scripts
- Assets/Prefabs
- Assets/Art
- Assets/Audio
- Packages
- ProjectSettings

These paths are directional examples, not complete repository facts yet.

## Skills Layout

Detailed AI-oriented documentation is split into focused skills under `.agents/skills`.

Current skill topics:

- `project-overview`
- `network-architecture`
- `session-flow`
- `synchronization-strategy`
- `implementation-roadmap`
- `unity-ecs-patterns`

When updating project direction, keep those skill documents aligned with this file.

The `unity-ecs-patterns` skill should be treated as the primary implementation guide for gameplay
code architecture and performance-oriented system design.

## Networking Guidance for Future Changes

When the project reaches implementation stage, AI contributors should align new code with the
current documentation direction:

- Use event-driven synchronization for ready state, scene transitions, wave start, and end-of-match
  state.
- Use host-side validation for tower placement, scoring, and objective completion.
- Avoid treating clients as the source of truth for health, score, or wave progression.
- Keep movement and aiming responsive locally, but ensure accepted state is consistent across the
  session.

## Documentation and Planning Expectations

If future work changes the scope, game modes, or networking approach:

- Update README.md first or in the same change.
- Mark open decisions explicitly.
- Prefer one clear recommendation over several half-defined alternatives.

## Known Open Decisions
 
The following items remain open:

- Match discovery / lobby listing backend (Lobby service vs. Join Code sharing).
- Maximum supported player count per session.
- Final map and scene naming.
- Dedicated server support versus host-only support.

## Scope Guardrail

At the current stage, most AI work should focus on:

- Documentation.
- Planning artifacts.
- Architecture decisions.
- Repository conventions.
- Incremental Unity project implementation.

AI should not assume finished runtime behavior, implemented multiplayer features, or code structure
that is not yet present.