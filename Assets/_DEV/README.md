# _DEV — Current Milestone

This folder holds active work for the current milestone only. `Future_DEV` holds the
shelved v0.3-style Create/Join Room system (room codes, private/public rooms) — kept intact
for when that scope comes back, not part of this build.

## Layout

- **Game/Scenes/Drive.unity** — the only accepted scene. Started as a fully local,
  non-networked scene (`PlayerMovement` + `PlayerCamera`, real dynamic Rigidbody, PhysX-resolved
  collisions) and networking has been built on top of it in place, rather than in a copy.
- **Game/Player/** — `PlayerMovement.cs`, `PlayerCamera.cs`, `PlayerNetInput.cs`,
  `DriveNetworkBootstrap.cs`, `PlayerCar.prefab` (the spawnable networked car). See
  `Documentation/Networking_Progress.md` for the full networking approach and current status.
- **Game/Art/Bolt/** — the player robot model (`PlayerRobot.prefab`) used in Drive.
- **Game/Editor/DriveSceneSetup.cs** — builds/resets the Drive scene, including regenerating
  `PlayerCar.prefab` (`Game/Setup Drive Scene` menu item).
- **Documentation/** — progress notes for whatever's currently active here, same convention as
  before: docs for shelved work move to `Future_DEV/Documentation/` instead.

The earlier Matchmaking/lobby system, the networked `ChassisController` vehicle model, AI
bots, and the mini-game prototype have been removed — they weren't wired to this scene and
aren't part of the current direction. Networking (Photon Fusion, Shared Mode, Forecast Physics)
has been rebuilt from `PlayerMovement` as the foundation instead.
