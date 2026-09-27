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

- The Unity project scaffold exists.
- The repository includes the default sample scene and project configuration.
- Gameplay, networking flow, and production structure are still incomplete.
- Documentation and skill files remain the source of truth for architecture assumptions.

AI contributors should avoid pretending that unfinished gameplay systems already exist.

## Recommended Technical Direction

Until the team makes a different decision, assume the project is targeting:

- Unity on PC.
- C# gameplay code.
- Unity ECS, Jobs, and Burst as the main gameplay implementation direction.
- Unity Netcode for GameObjects.
- Unity Transport.
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
- Keep gameplay-critical state authoritative on the host unless the project architecture changes.
- Distinguish clearly between local presentation logic and networked game state.
- Do not invent backend services, scene names, or folder structures as hard facts unless they are
  present in the repository.
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

The following items remain intentionally undecided:

- Match discovery backend.
- Maximum supported player count.
- Final map and scene naming.
- Final testing workflow.
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