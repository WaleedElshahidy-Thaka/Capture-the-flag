# Networking Progress

Scene: `Assets/_DEV/Game/Scenes/Drive.unity` — the only accepted scene. Started as fully local
(non-networked) `PlayerMovement` + `PlayerCamera` on a real dynamic Rigidbody, PhysX-resolved
collisions; networking has been added on top of it in place, not in a copy.

## Topology: Shared Mode, no host/client

The game is quick-match style — players just find each other and play, with no visible "who's
hosting." That rules out Host/Client (where one player's own machine is silently the arbiter)
and points at Fusion's **Shared Mode**: no single player's device holds authority, Photon's
cloud relay coordinates the session, and each player has State Authority over their own car
because each player is the one who spawns it.

- `DriveNetworkBootstrap.cs` starts `GameMode.Shared` (fixed `SessionName = "DriveTest"` for
  now — no matchmaking/lobby flow yet, that was deliberately deleted along with the old
  Matchmaking system, see `../README.md`).
- On its own `OnPlayerJoined`, each peer calls `Runner.Spawn(playerPrefab, ...)` for itself only
  — spawning is what grants State Authority in Shared Mode, so this is also how
  `PlayerMovement.Spawned()` recognizes "this is my own car" (`Object.HasStateAuthority`) and
  claims Input Authority + wires the camera. No other peer's join ever triggers a spawn on your
  side, or everyone would end up spawning cars for everyone else.

## Physics replication: Forecast Physics, not the Physics Addon

Two ways Fusion can network a real Rigidbody-driven car:

- **Physics Addon's `NetworkRigidbody`** (full client-side prediction + resimulation) — explicitly
  refuses to run in Shared Mode (its own source despawns the object with a warning if you try).
  Not used.
- **Core `NetworkTransform` with Forecast Physics** (extrapolates from the Rigidbody's last known
  velocity instead of resimulating, with spring/damper correction on drift) — has explicit Shared
  Mode support built in. This is what's used: a plain `NetworkTransform` component with
  `PhysicsSettings.ForecastEnabled = true`, gated globally by
  `NetworkProjectConfig.PhysicsForecast = true` (`Assets/Photon/Fusion/Resources/NetworkProjectConfig.fusion`).

## Key files

- `Game/Player/PlayerMovement.cs` — `NetworkBehaviour`. `FixedUpdateNetwork()` applies
  throttle/steer/boost/drift forces from `GetInput<PlayerNetInput>`, same physics logic as the
  original local-only version. `Spawned()` claims Input Authority and wires the camera when
  `HasStateAuthority` is true (i.e. this is the local player's own car).
- `Game/Player/PlayerNetInput.cs` — the networked input struct.
- `Game/Player/DriveNetworkBootstrap.cs` — starts the `NetworkRunner`, implements
  `INetworkRunnerCallbacks` directly (not the `NetworkEvents` component — it doesn't expose
  `OnPlayerJoined`), handles `OnInput` (keyboard sampling) and `OnPlayerJoined` (per-player spawn).
  The other ~17 interface methods are required no-op stubs.
- `Game/Player/PlayerCamera.cs` — `SetTarget(Rigidbody)` lets the camera be wired at runtime,
  since the car it follows no longer exists at edit time (it's spawned, not scene-placed).
- `Game/Player/PlayerCar.prefab` — built and saved by `Game/Editor/DriveSceneSetup.cs`'s
  `BuildPlayerPrefab()` (menu: `Game → Setup Drive Scene`), not hand-authored. Re-running that
  menu command regenerates it from the same code path used originally.

## Status

Verified so far, each as its own step (smallest testable slice first):

1. `GameMode.Single` — networked `PlayerMovement` conversion drives correctly with no second peer.
2. `GameMode.Host`/`Client`, two real processes (Editor + a standalone build) — confirmed the
   same car's motion replicates from Host to Client.
3. `GameMode.Shared`, per-player spawned cars — implemented, compiles clean (two harmless
   `CS0618 SimulationMessagePtr` obsolete warnings, required by `INetworkRunnerCallbacks` itself,
   also present in the pre-existing `Future_DEV/Scripts/Services/Fusion/RunnerCallbackRelay.cs`).
   Not yet confirmed working end-to-end (each peer spawning + driving its own car, each seeing
   the other's car too) — that's the next thing to verify.

## Known gaps / not yet built

- No matchmaking/lobby flow — `SessionName` is hardcoded to `"DriveTest"`, everyone who runs the
  build joins the same fixed session. The old Matchmaking system was removed (see `../README.md`)
  rather than adapted, since it was built around Host/Client + a different vehicle model
  (`ChassisController`, also removed).
- No player-vs-player collision/contact resolution logic beyond what PhysX + Forecast Physics's
  correction heuristics give for free.
- No visual distinction between your own car and another player's yet.
