# _DEV — Current Milestone

This folder holds active work for the current milestone only. `Future_DEV` holds the
shelved v0.3-style Create/Join Room system (room codes, private/public rooms) — kept intact
for when that scope comes back, not part of this build.

## Layout

- **Game/Scenes/Game.unity** — the one accepted scene: matchmaking lobby and real gameplay
  together, no second scene, no scene load between the two phases. Built by
  `Game/Editor/GameSceneSetup.cs` (`Game/Setup Game Scene` menu item). See
  `Documentation/Networking_Progress.md` for the full networking approach and current status.
- **Game/Vehicle/** — Layer B, the drive model's own pieces: `GroundProbes.cs` (four corner
  raycasts, no knowledge of driving) and `HandlingValues.cs` (every feel value in one
  serializable class). See `Documentation/Vehicle_Model.md`.
- **Game/Player/** — `PlayerMovement.cs` (the per-tick simulation), `PlayerMatchState.cs`
  (ready/searching/`CanMove` — a sibling component, kept separate on purpose), `PlayerCamera.cs`,
  `PlayerNetInput.cs`, `PlayerInputSampler.cs` (keyboard → networked input, called from
  `Matchmaking/Services/QuickMatchFusionService.cs`'s `OnInputAction`), and
  `Player/Resources/PlayerCar.prefab` (the one networked object per player — spawned by the host
  the moment they connect, via `Matchmaking/Services/PlayerLobbySpawner.cs`; not drivable until
  `MatchmakingSessionState.TriggerStart` releases `PlayerMatchState.CanMove`).
- **Game/Arena/** — `ArenaBounds.cs`, the serialized arena dimensions. Placeholder until the art
  team's arena arrives; the component then moves onto the real one and keeps the same role.
- **Game/Art/Bolt/** — the player robot model (`PlayerRobot.prefab`).
- `Game.unity`'s arena includes a ramp and a banked curved section (`GameSceneSetup.cs`'s
  `BuildRamp()`/`BuildCurve()`), for testing driving against something other than flat ground.
  Everything is built from primitives — no custom meshes.
- **Matchmaking/** — Find Match flow: Quick Match via Photon Fusion (`GameMode.Shared`, no
  visible host — see `Documentation/Networking_Progress.md`), ready-toggle + bot-fill start
  logic (bot AI itself not built yet). Connecting happens automatically on scene start, not
  behind a button — Find Match only flags this player as actively searching. No 3D lobby
  placeholder visuals (deleted; see Networking_Progress.md's history section) — you see the
  real `PlayerCar`s of everyone connected, from the moment they connect.
- **Documentation/** — `Roadmap.md` (phases, rulings, what's next), `Vehicle_Model.md` (the
  drive model: architecture, why, current state, tuning) and `Networking_Progress.md` (topology,
  authority, replication). The GDD under `Assets/GDD/` is the source of truth above all three.
  Docs for shelved work move to `Future_DEV/Documentation/` instead, so this folder never goes
  stale.

Note: there is no separate non-networked test scene anymore — an earlier `LocalDrive.unity` +
`LocalPlayerMovement.cs`/`LocalPlayerCamera.cs`/`LocalDriveSceneSetup.cs` existed for tuning
driving feel without networking involved; removal confirmed intentional. Driving-feel testing
(ramp/curve included) goes through `Game.unity`'s full Fusion flow now.

## Not built yet

See `Documentation/Roadmap.md` for the full breakdown. Short version: no bot AI, no
player-vs-player collision/contact resolution beyond default PhysX + Forecast Physics
correction, no game manager or win-condition flow, no scoring, no death/lives, no visual
distinction between your own car and another player's, and only placeholder art.

## Conventions carried over from Future_DEV

- No `namespace` blocks — everything global-namespace, matching this project's existing style.
- `Ok`/`Fail` static-factory result types (see `Future_DEV/Scripts/Models/GameModels.cs` for
  the original pattern this follows).
- A static composition root per feature — one field per service, one line to add a new one.
  Named `MatchmakingServices` in `Matchmaking/`, not `Services`, since `Future_DEV/Scripts/
  Core/Services.cs` already claims that name and both trees compile into the same
  Assembly-CSharp (no `.asmdef` boundary anywhere in this project).
- Small, single-purpose interfaces per consumer (Interface Segregation) backed by swappable
  implementations (Local for testing, Fusion for real networking).
