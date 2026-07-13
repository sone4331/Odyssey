# Odyssey

Odyssey is a Unity gameplay-client portfolio project focused on production-oriented character control, combat, AI, data-driven configuration, testing, profiling, and host-authoritative multiplayer.

## Current state

The industrialization baseline is executable rather than aspirational: Core, Gameplay, Unity and Editor assembly boundaries are in place; player combat uses the shared Ability/Health pipeline; configuration is imported from CSV into a validated runtime asset; saves use versioned atomic JSON; and health UI updates from domain events.

The next milestones are player responsibility cleanup, layered Utility AI, performance evidence, and an isolated host-authoritative NetworkArena. Lua, full GAS, deterministic lockstep, KCP integration and a general-purpose build framework are intentionally outside the first portfolio release.

## Local quality gates

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\RunCoreTests.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\TestDocumentation.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\Tests\RunDocumentationAuditSpecs.ps1
```

Production C# types explain responsibility, pattern and design rationale in Chinese XML summaries. Known legacy controllers are listed in `Tools/DocumentationAuditExclusions.txt` and must be removed from that list when their owning milestone refactors them.

## Environment

- Unity `2023.2.20f1c1`
- Input System `1.7.0`
- Cinemachine `2.10.0`
- Windows is the primary development and demonstration platform.

## Third-party content

The current prototype scene references Unity 3D Game Kit Lite content. That package and TextMesh Pro example assets are intentionally excluded from the public source repository. Import the matching original package distribution before opening `Assets/_Project/Scenes/Level_01.unity`.

## Repository policy

The local `架构学习文档` directory contains study notes derived from an external commercial project. It is intentionally excluded from the portfolio repository and is not presented as Odyssey implementation work.

The repository uses `type(scope): 中文说明` Conventional Commits. A completed module is pushed only after the relevant pure C#, Unity and documentation checks pass.
