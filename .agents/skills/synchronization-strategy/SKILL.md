---
name: synchronization-strategy
description: "Use when: you need guidance on how to synchronize movement, shots, enemies, towers, resources, scores, and round-state data in the planned multiplayer architecture."
---

# Synchronization Strategy

## Principle

Different gameplay systems should use different synchronization models. Avoid treating all data as
high-frequency replicated state.

## Player Movement and Aim

- Process local input immediately on the owning client.
- Replicate movement and aiming frequently enough for smooth remote playback.
- Let the host confirm accepted gameplay state.

## Shots and Damage

- Send fire actions as gameplay events.
- Resolve damage authoritatively on the host.
- Replicate health from the host to clients.

## Enemy Waves

- Drive wave start and spawn timing from the host.
- Keep enemy health, death, and progression authoritative on the host.

## Towers and Build State

- Send placement requests from the initiating client.
- Validate placement, cost, and ownership on the host.
- Replicate accepted tower state to all clients.

## Score, Resources, and Results

- Treat resources, score, survival time, and summary data as host-authoritative.
- Use reliable synchronization for state transitions that must not be missed.