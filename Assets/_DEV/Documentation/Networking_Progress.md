# Networking

Scope: topology, authority and replication. The drive model has its own doc
(`Vehicle_Model.md`); phases and rulings live in `Roadmap.md`.

Scene: `Assets/_DEV/Game/Scenes/Game.unity` — matchmaking lobby and gameplay together, no second
scene and no scene load between the two phases.

## Topology: Host/Client, invisible to players

`GameMode.AutoHostOrClient`. The first searcher transparently becomes host, later searchers join
as clients. Players never see or choose this — from their side it's still "press Find Match, get
put in with people."

An earlier pass used `GameMode.Shared`, on a misreading of "no authority in our game" as an
architectural requirement rather than a UX one. Glowtag FD-05 rules directly against it:

> *"Glowtag's position: Host mode, with the host-disconnect risk accepted knowingly."*

Two reasons, both decisive for this game:

- **Contested steals.** Under Shared Mode each peer resolves a bump against its own stale proxy
  of the other car, so the two disagree about who won it — and that decides who holds the Crown.
  FD-05 calls this *"the exact case this mode cannot absorb."* Doc 05 requires one authority per
  chassis-to-chassis contact; Host/Client is what provides it.
- **Bots need an owner.** Shared Mode has no natural single authority to simulate them.

Host migration is explicitly not requested. Rounds are 90 seconds, so a host drop costs one short
round. Late join and rejoin are not supported — the session closes on StartMatch.

## What that means in code

| Concern | Where |
|---|---|
| Spawning | Host only. `PlayerLobbySpawner` spawns one `PlayerCar` per joining player, handing each Input Authority over their own car. Clients never call `Spawn` |
| "Is this my car?" | `Object.HasInputAuthority`. **Not** `HasStateAuthority` — the host holds State Authority over *every* car, so that means "I am the host" |
| "Am I the host?" | `runner.IsServer`, or `HasStateAuthority` on the session singleton |
| Match start | Host-side. `MatchmakingSessionState.TriggerStart` releases `CanMove` for every car — a client can't write networked state at all |
| Despawn on leave | Host only |
| Client → host requests | `[Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]`. Routes correctly and needed no change across the topology switch |

## Matchmaking flow (as of 2026-09-15)

This is the owner-specified flow; treat it as the spec.

```
open the game → Loading (black) — auto-connect, host spawns & seats your car
→ Idle: arena, your robot, [Quick Match]. Nothing happens until pressed.
→ Searching: "Searching for players… Ns" from 0, Cancel available
   ├─ nobody: at 30 s → WaitingSolo: [Start with computer players] → starts immediately
   └─ 2+ searching: you see each other + names; timer becomes SHARED = the highest one
        at 30 s shared → WaitingReady: [Play with computer players] marks you READY
        (tag above your robot, button → [Unready]); all searching players ready → start
        with bots; otherwise the timer keeps counting and searching continues.
   6 searching → immediate start, no bots.
```

Mechanics: connection is automatic on scene start (`MatchmakingFlowController.Start`), the
loading screen stays until `IsLocalPlayerSpawned`. Quick Match sends `RPC_SetSearching(true)`;
the host stamps `SearchStartTick`, so every peer can compute every searcher's elapsed time
and the shared timer is simply the highest (`MatchmakingSessionState.SharedElapsedSeconds`).
Cancel is `RPC_SetSearching(false)` — you stay connected and seated. The host decides the
start in `MatchmakingSessionState.FixedUpdateNetwork`; the solo start goes through
`RPC_RequestStartWithBots`. Values live in `MatchmakingConfig`.

**Visibility:** your own car is always visible; another player's car appears only once you are
*both* searching (`PlayerLobbyVisibility` toggles `Visual`/`NameTag`). Someone sitting in the
arena without searching is not shown to searchers, and vice versa.

Display names: `PlayerMatchState.DisplayName` (`NetworkString<_32>`), sent by the owning client
via RPC right after spawn from `PlayerIdentity.LocalDisplayName` (OS user name until the
launcher's account identity is wired in). Rendered by `PlayerNameTag`, a world-space canvas
child of `PlayerCar.prefab`.

Lobby layout and camera: `LobbyLayout` is the single source for both. Seats are a staggered row
(slot *i* at X = i·spacing, Z alternating 0 / stagger), cars facing the camera, and one fixed
isometric camera at the row's centre X — the same picture on every peer, shown from the first
frame with no target needed. `PlayerLobbySpawner` seats by `PlayerId`, `PlayerCamera` parks on
it in the lobby and switches to the third-person follow when `PlayerMatchState.CanMove` is
released — no explicit call.

## Physics stepping

`RunnerSimulatePhysics` (Fusion Physics addon) is attached to the runner in
`QuickMatchFusionService`, so PhysX steps once per Fusion tick instead of on Unity's own 50 Hz
FixedUpdate. Before this, the drive model wrote velocity at 60 Hz against a 50 Hz integrator —
some writes integrated twice, some never — which was a visible jitter source and broke doc 03's
"same input, same tick, same result" premise. `PlayerMovement` carries
`[DefaultExecutionOrder(-50)]` so its velocity write lands before the step in the same tick.
Rigidbody interpolation is off (NetworkTransform interpolates for rendering).

## Replicated simulation state

Anything that accumulates across ticks must be networked and restorable, per doc 03's
determinism contract — a rollback restores drive state, not PhysX internals.

- `PlayerMovement`: `YawRate`, `BrakeHoldSeconds` (accumulating), `SteerInput` (cosmetic, but
  every peer needs it to draw remote wheels turning)
- `PlayerMatchState`: `IsReady`, `IsSearching`, `CanMove`
- `MatchmakingSessionState`: `MatchStarting`, `BotCount`, `SearchStarted`, `SearchTimer`

## Physics replication

Core `NetworkTransform` with Forecast Physics (`PhysicsSettings.ForecastEnabled`), gated globally
by `NetworkProjectConfig.PhysicsForecast`.

Note: the Physics Addon's `NetworkRigidbody` refuses Shared Mode but *works* under Host/Client,
so that door reopened with the topology switch. It stays ruled out anyway — doc 03 rules out
PhysX-driven vehicle physics on determinism grounds regardless of topology.

## Verified

1. `GameMode.Single` — networked movement conversion drives correctly, no second peer.
2. `GameMode.Host`/`Client`, two real processes — car motion replicates.
3. `GameMode.Shared`, per-player spawned cars — confirmed working end to end before the switch
   away from it.
4. Matchmaking merged into one scene; connect-on-start with Find Match as a searching flag;
   shared lobby search timer; remote wheel-steer replication.

5. Host/Client with two real peers (2026-09-14 test report): both cars spawn under the host,
   both drive, the match starts for both.

**Untested since (2026-09-15 changes):** the found-countdown/lobby flow, name tags, lobby
camera, the physics-stepping switch, and the grip-rate change. Next two-peer test should
confirm: P2's timer starts at 0 → "Player found" on both after P2 reaches 5 s → lobby with both
names above the cars and a Ready button → Ready on both starts the match; no rotation jitter in
the inspector; Ctrl+steer visibly slides.

## Open

- **Tick rate (ruling N2)** — FD-05 says the mode can't be finally tuned until it's ruled on; at
  30Hz the drift release window's wall-clock duration doubles. `NetworkProjectConfig` currently
  uses Fusion defaults.
- **`ParticipantRef` vs `PlayerRef`** — bots have no `PlayerRef`, so Glowtag addresses every
  participant by a `ParticipantRef` (FD-05/FD-07). Current code is `PlayerRef` throughout. Needs
  an abstraction before bots land.
- **`Game.unity` in Build Settings** — must be added manually after first running
  `Game → Setup Game Scene`, since the scene and its GUID don't exist until then.
