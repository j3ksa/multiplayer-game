---
name: network-architecture
description: "Use when: you need the recommended Unity multiplayer stack, authority model, transport guidance, or network responsibilities for future implementation work."
---

# Network Architecture

## Recommended Stack

- Unity Netcode for GameObjects.
- Unity Transport.
- Host-client session topology.

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
supporting real-time multiplayer design.

## Open Decisions

- Final discovery or lobby backend.
- Final player count.
- Whether the project will stay host-only or later add dedicated-server support.