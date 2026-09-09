# _DEV — Current Milestone

This folder holds active work for the current milestone only. `Future_DEV` holds the
shelved v0.3-style Create/Join Room system (room codes, private/public rooms) — kept intact
for when that scope comes back, not part of this build.

## Layout

- **Game/Scenes/Drive.unity** — the only accepted scene. Started as a fully local,
  non-networked scene (`PlayerMovement` + `PlayerCamera`, real dynamic Rigidbody, PhysX-resolved
  collisions) and networking is now being built on top of it in place, rather than in a copy.
- **Game/Player/** — `PlayerMovement.cs` and `PlayerCamera.cs`.
- **Game/Art/Bolt/** — the player robot model (`PlayerRobot.prefab`) used in Drive.
- **Game/Editor/DriveSceneSetup.cs** — builds/resets the Drive scene
  (`Game/Setup Drive Scene` menu item).

The earlier Matchmaking/lobby system, the networked `ChassisController` vehicle model, AI
bots, and the mini-game prototype have been removed — they weren't wired to this scene and
aren't part of the current direction. Networking is being rebuilt from `PlayerMovement` as the
foundation instead.
