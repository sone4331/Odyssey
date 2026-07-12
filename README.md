# Odyssey

Odyssey is a Unity gameplay-client portfolio project focused on production-oriented character control, combat, AI, data-driven configuration, testing, profiling, and host-authoritative multiplayer.

## Current state

The project is being migrated from a compact gameplay prototype into a testable vertical slice. The first implementation milestone establishes assembly boundaries and pure C# gameplay primitives before scene-facing systems are migrated.

## Environment

- Unity `2023.2.20f1c1`
- Input System `1.7.0`
- Cinemachine `2.10.0`
- Windows is the primary development and demonstration platform.

## Third-party content

The current prototype scene references Unity 3D Game Kit Lite content. That package and TextMesh Pro example assets are intentionally excluded from the public source repository. Import the matching original package distribution before opening `Assets/_Project/Scenes/Level_01.unity`.

## Repository policy

The local `架构学习文档` directory contains study notes derived from an external commercial project. It is intentionally excluded from the portfolio repository and is not presented as Odyssey implementation work.

Detailed architecture, test, performance, networking, and build instructions will be added as their corresponding milestones become executable.
