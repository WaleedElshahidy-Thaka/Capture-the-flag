# Capture-the-Flag — Code Architecture

Start here if you are new to the project. This covers the game code in `Assets/_DEV`.
(`Assets/Future_DEV` is a separate, not-yet-shipped room-code lobby + launcher hand-off; see `ADDING_A_GAME.md` / `APP_CYCLE.md`.)

- **Engine:** Unity 6 (6000.3), URP, new Input System, C# 9
- **Networking:** Photon Fusion 2, **Host/Client** (`GameMode.AutoHostOrClient`) with host migration
- **Only scene in the build:** `Assets/_DEV/Game/Scenes/Game.unity` (lobby and gameplay in one scene)

---

## 1. Folder map

```
_DEV/
├─ Game/
│  ├─ Player/          Per-car network components + local player helpers
│  │   PlayerMovement      NetworkBehaviour: replicated drive state, input source, calls VehicleDriveModel
│  │   PlayerMatchState    NetworkBehaviour: searching/ready/CanMove/owner + client→host RPCs
│  │   PlayerRoster        Static registry of all cars on this peer + roster queries
│  │   PlayerCamera        Solo / Lobby / Follow camera, binds itself to PlayerRoster.Local
│  │   PlayerNameTag, PlayerLobbyVisibility, CameraFacing   cosmetic
│  │   PlayerNetInput, PlayerInputSampler, BotDriver        input sources
│  │   PlayerIdentity      display name + install id (connection token)
│  ├─ Vehicle/         The drive model — plain C#, no networking
│  │   VehicleDriveModel   runs one physics tick (the pipeline below)
│  │   Motion/             IMotionMode + Grounded/Airborne/Spinning, SteeringModel, LongitudinalDrive
│  │   GroundProbes, WallSensor, WallContact, SpinoutRecovery, ChassisAttitude
│  │   HandlingValues      every tuning number (serialized on the PlayerCar prefab)
│  │   DriveState          the state that carries between ticks
│  │   VehicleVisuals      tire roll/steer + body lean (cosmetic)
│  ├─ Arena/           ArenaBounds, LobbyPreviewCar, LobbySeatMarker
│  └─ Editor/          "Game > Setup Game Scene" tool (GameSceneSetup + builders)
└─ Matchmaking/
   ├─ Flow/            MatchmakingFlowController (state machine), MatchStarter
   ├─ Models/          MatchmakingPhase (+ helpers), StartQuickMatchResult
   ├─ Services/        IQuickMatchService / IActiveQuickMatchSession + Fusion and offline implementations
   ├─ Network/         MatchmakingSessionState (host-side start rules + migration freeze), RunnerCallbackRelay
   ├─ Config/          MatchmakingConfig (timings), LobbyLayout (seat + camera positions)
   ├─ Core/            MatchmakingServices (composition root: pick the backend here)
   └─ UI/              MatchmakingScreen (view), ScreenFade
```

## 2. Layers and dependency direction

```
UI (MatchmakingScreen)  ──►  Flow (MatchmakingFlowController)  ──►  IQuickMatchService / IActiveQuickMatchSession
                                                                         ▲               ▲
                                                     QuickMatchFusionService     QuickMatchLocalService (offline fake)
                                                                         │
                                       Fusion network objects: PlayerMatchState, MatchmakingSessionState, PlayerMovement
                                                                                                   │
                                                                         Vehicle drive model (plain C#)
```

- The **flow controller never touches Fusion.** It only knows the two interfaces, so it runs the same against the real backend or the offline fake. Switch backends in `MatchmakingServices.cs`.
- **The vehicle code doesn't know about networking.** `PlayerMovement` copies the `[Networked]` fields into a `DriveState`, calls `VehicleDriveModel.Simulate`, and copies the result back.
- **Nothing reaches into the camera.** `PlayerCamera` follows `PlayerRoster.Local` by itself.

## 3. One network tick of a car

`PlayerMovement.FixedUpdateNetwork` (execution order −50, before Fusion steps PhysX):

1. Session frozen (host migration)? Zero velocity and stop.
2. Pick input: `GetInput` (a player) → else `BotDriver.Think` (host, ownerless car) → else skip.
3. Before the match (`!CanMove`) the input is neutral, so the car only settles on its suspension.
4. `VehicleDriveModel.Simulate`:
   1. `GroundProbes.Cast`: four raycasts give suspension compression, ground normal, and grounded (≥2 probes and tilt ≤ `MaxDriveAngle`).
   2. `SpinoutRecovery.Tick`: counts the spin down, scales input, returns the spin's extra yaw.
   3. Pick an `IMotionMode`: **Spinning** if spinning out, else **Grounded** / **Airborne**.
   4. `ResolveWalls`: `WallSensor` looks ahead; `WallContact` turns the velocity along the wall. A hard impact enters a spin-out.
   5. Write `linearVelocity`, and `angularVelocity` from `ChassisAttitude`.
5. `LateUpdate`: `VehicleVisuals.Animate` (cosmetic only).

## 4. Design patterns used (and why only these)

| Pattern | Where | Why it's there |
|---|---|---|
| **Strategy** | `IMotionMode` → `GroundedMotion`, `AirborneMotion`, `SpinningMotion` | The car's motion rules really do switch by state. A new mode (drift, boost pad, ice) is a new class, not an `if` in a 600-line file. |
| **State machine** (enum-based) | `MatchmakingFlowController` + `MatchmakingPhase` | 8 phases with simple transitions. A class per state would add files without adding clarity. |
| **Adapter** | `RunnerCallbackRelay`, `FusionActiveQuickMatchSession` | Turns Fusion's large interfaces into the small surface our code needs. |
| **Factory** | `QuickMatchRunnerFactory` | The first connect and every host-migration reconnect build runners the same way. |
| **Composition root / Service locator** | `MatchmakingServices` | The one place the backend is chosen. |
| **Registry** | `PlayerRoster` | One owner for "all cars on this peer" and its queries (it used to be duplicated in 4 classes). |
| **Observer** (C# events) | `PhaseChanged`, `PlayerRoster.Changed`, `HostLost`/`SessionRestored`/`SessionFailed` | UI and sessions react to changes without polling each other. |

## 5. Rules that keep the project working

1. **Never rename** a MonoBehaviour/NetworkBehaviour class or its file. Scenes and prefabs reference scripts by the `.meta` GUID and the file name. If you must move a file, move its `.meta` with it.
2. **Never rename serialized fields** (`[SerializeField]`, `HandlingValues` fields) without `[FormerlySerializedAs("oldName")]`. The tuned values live in `PlayerCar.prefab`.
3. **Don't reorder `[Networked]` properties.** Declaration order is the network memory layout. Add new ones at the end.
4. **The drive model must stay deterministic:** compose the velocity and write it each tick. No `AddForce`, no PhysX gravity/drag/friction (see `ChassisPhysicsSetup`), and no state outside `DriveState` that changes the result of a tick.
5. **Host-only code** is guarded by `HasStateAuthority` / `runner.IsServer`. Clients change state only through the RPCs on `PlayerMatchState`.
6. **Tick-loop code must not allocate.** Use index `for` loops over `PlayerRoster.Active`, not LINQ or `foreach` over the interface.
7. Prefab and scene structure come from code: change the builders in `Game/Editor/`, then run **Game > Setup Game Scene**.

## 6. Key design decisions (the "why")

**Vehicle (GDD doc 03 / 05)**
- **Custom arcade controller, not WheelCollider.** Boost must exceed top speed, and PhysX solver state doesn't survive a Fusion rollback. Velocity is *composed and written* every tick, so re-simulating a tick from restored state gives the same result.
- **The Rigidbody has gravity, damping and friction switched off.** Each one was a second, unmodelled force: double gravity made parked cars creep downhill, and damping capped speed at ~9 m/s. Gravity is `GravityScale` and grip is `GripRate`.
- **The suspension is preloaded around the wheel radius**, so ride height doesn't depend on `SpringStrength`. The wheel sphere colliders just touch the ground at rest, so the colliders and the suspension agree instead of fighting.
- **Pitch/roll are levelled, not frozen.** Freezing fought PhysX and stopped the car following slopes. Active levelling also means the car always lands on its wheels (there is no respawn).
- **Grip decays per second (`exp(-rate·dt)`), not per tick**, so the handling feels the same at any tick rate.
- **Walls project, never reflect:** a speed cost proportional to the angle, a minimum exit speed, and no dead stops. Wall rays are used instead of a box sweep because a sweep that starts inside its own collider returns garbage normals.
- **Spin-out is networked state**, not a local effect: a local spin the host disagrees with is the most visible desync.
- **`RunnerSimulatePhysics`**: Fusion steps PhysX once per network tick. Without it Unity's 50 Hz physics and Fusion's 60 Hz writes drift apart and the car jitters.
- **`NetworkRigidbody`, not `NetworkTransform`:** it replicates and restores velocity too, and interpolates the `View` child for rendering.

**Matchmaking / networking**
- **Host/Client, not Shared mode:** car-vs-car contact needs one authority to decide who won a bump.
- **You connect when you press Quick Match, not at startup.** Sessions contain only searchers, so the host is always a searcher.
- **One timer, from the click:** a local clock until our car is seated, then the host back-dates the networked timer, so the number never jumps.
- **"Player found" countdown:** every peer counts from the same host-stamped tick, so cars appear to each other on the same frame everywhere.
- **No late joins:** the session closes when the match starts. The only exception is reconnecting after a host migration.
- **Host migration:** each car carries its owner's install id (`OwnerToken`). The new host restores the snapshot, freezes play, hands each car back as its player reconnects, then resumes everyone on one tick. Players who don't return become bots (the field stays at six).

## 7. Common tasks

| Task | Where |
|---|---|
| Tune handling | `PlayerCar.prefab` → PlayerMovement → Handling (changes apply live in Play Mode) |
| Change matchmaking timings / player count | `MatchmakingConfig.cs` |
| Test the UI without a network | `MatchmakingServices.cs` → `new QuickMatchLocalService()` |
| Add a motion behaviour (e.g. drift) | New `IMotionMode` class; select it in `VehicleDriveModel.CurrentMode` |
| Real bot AI | Replace the body of `BotDriver.Think` (it returns a `PlayerNetInput`) |
| Car-vs-car slam → spin-out | Call `PlayerMovement.EnterSpinout(direction, sign)` on the host |
| Rebuild the scene/prefabs | Menu **Game > Setup Game Scene** |

## 8. Known gaps / next steps

- Bot cars are not spawned for empty seats yet (`MatchStarter`), and `BotDriver` just parks.
- Car-vs-car contact resolution (GDD doc 05) is not implemented; PhysX handles it for now.
- Not yet verified: whether matchmaking without a session name filters by the `GameId` property (see `QuickMatchFusionService`).
- Possible improvements: namespaces + an `.asmdef` per feature, a ScriptableObject handling profile around `HandlingValues`, and EditMode unit tests for the pure classes (`WallContact`, `LongitudinalDrive`, `SteeringModel`, `SpinoutRecovery`). These are already plain C#, so they are easy to test.
