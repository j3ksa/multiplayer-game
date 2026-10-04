---
name: implementation-roadmap
description: "Use when: you need the planned implementation phases, high-level development order, or the main milestones for building the multiplayer systems in this repository."
---

# Implementation Roadmap

## Phase 1 (Completed)

- [x] Lock working Unity version (`6000.4.2f1`).
- [x] Install core multiplayer packages:
  - `com.unity.netcode.gameobjects` (NGO) `2.13.3`.
  - `com.unity.transport` (UTP) `2.7.4`.
  - `com.unity.services.relay` `1.1.1`.
  - `com.unity.services.authentication` `3.3.4`.
  - `com.unity.services.core` `1.14.0`.
- [x] Establish core networking architecture (`NetworkBootstrap.cs`, `SessionManager.cs`, `PlayerData.cs`).
- [x] Build multi-device connection HUD supporting both **Unity Relay (Join Codes)** and **Direct IP (LAN/VPN)**.
- [x] Implement 1-click Windows standalone build tool (`BuildHelper.cs`).

## Phase 2 (In Progress)

- [ ] Networked player prefab and spawning logic (Capsule / tower-top character).
- [ ] Tower-top FPS player movement and look synchronization.
- [ ] Pre-match ready-state UI and host match start trigger.
- [ ] Scene transitions between lobby and gameplay.

## Phase 3

- [ ] Implement enemy wave spawning (host-authoritative).
- [ ] Implement projectile shooting and hit validation on host.
- [ ] Implement tower placement validation.
- [ ] Synchronize health, score, and round state.

## Phase 4

- [ ] Implement end-of-match result summary.
- [ ] Implement restart, next level, and return-to-lobby flow.
- [ ] Network stress testing: latency simulation, disconnect handling, and state recovery.

## Notes

This roadmap is directional and should be updated as gameplay systems and networking features are implemented.