# _DEV — Current Milestone

This folder holds active work for the current milestone only. `Future_DEV` holds the
shelved v0.3-style Create/Join Room system (room codes, private/public rooms) — kept intact
for when that scope comes back, not part of this build.

## Layout

- **Game/Scenes/Game.unity** — the one accepted scene: matchmaking lobby and real gameplay
  together, no second scene, no scene load between the two phases. Built by
  `Game/Editor/GameSceneSetup.cs` (`Game/Setup Game Scene` menu item). See
  `Documentation/Networking_Progress.md` for the full networking approach and current status.
- **Game/Player/** — `PlayerMovement.cs` (driving physics only), `PlayerMatchState.cs`
  (ready/searching/`CanMove` — a sibling component, kept separate from PlayerMovement on
  purpose, see its own doc comment), `PlayerCamera.cs`, `PlayerNetInput.cs`,
  `PlayerInputSampler.cs` (keyboard → networked input, called from
  `Matchmaking/Services/QuickMatchFusionService.cs`'s `OnInputAction`), and
  `Player/Resources/PlayerCar.prefab` (the one networked object per player — spawned and
  positioned the moment they connect, by `Matchmaking/Services/PlayerLobbySpawner.cs`; not
  drivable until `Matchmaking/Flow/MatchStarter.cs` flips `PlayerMatchState.CanMove`).
- **Game/Scenes/LocalDrive.unity** — a separate, deliberately non-networked reference/testing
  scene: no Fusion, no NetworkRunner, just the car movement and camera on their own. For tuning
  the driving feel itself without any networking in the way.
- **Game/LocalDrive/** — `LocalPlayerMovement.cs`, `LocalPlayerCamera.cs`, used only by
  LocalDrive.unity. Separate classes from `Game/Player/`'s networked versions (same project,
  no namespaces, so names can't collide) - not a fork to keep in sync, just the same simple
  driving logic with no Fusion dependency, kept intentionally frozen.
- **Game/Art/Bolt/** — the player robot model (`PlayerRobot.prefab`) used by both scenes.
- **Game/Editor/LocalDriveSceneSetup.cs** — builds/resets LocalDrive.unity
  (`Game/Setup Local Drive Scene` menu item).
- **Matchmaking/** — Find Match flow: Quick Match via Photon Fusion (`GameMode.Shared`, no
  visible host — see `Documentation/Networking_Progress.md`), ready-toggle + bot-fill start
  logic (bot AI itself not built yet). Connecting happens automatically on scene start, not
  behind a button — Find Match only flags this player as actively searching. No 3D lobby
  placeholder visuals (deleted; see Networking_Progress.md's history section) — you see the
  real `PlayerCar`s of everyone connected, from the moment they connect.
- **Documentation/** — `Networking_Progress.md` (what's built, how, and current status) and
  `Roadmap.md` (what's next — collision, game manager, scoring, lives, art/polish — none of it
  started yet). Docs for shelved work move to `Future_DEV/Documentation/` instead, so this
  folder never goes stale.

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
