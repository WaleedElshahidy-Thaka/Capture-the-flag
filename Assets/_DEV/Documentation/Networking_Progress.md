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

Late join is not supported — the session goes invisible and closed on `TriggerStart`
(`MatchmakingSessionState.CloseSession`). Before that existed, a player opening the game
mid-round was dropped straight into it: visible to everyone, unable to drive, no UI.

**Host migration is built** (2026-09-16, owner's decision after the first two-PC test lost a
match to the host closing his .exe). FD-05 accepted the host-drop risk knowingly; the ruling now
is to survive it. See "Host migration" below.

## What that means in code

| Concern | Where |
|---|---|
| Spawning | Host only. `PlayerLobbySpawner` spawns one `PlayerCar` per joining player, handing each Input Authority over their own car. Clients never call `Spawn` |
| "Is this my car?" | `Object.HasInputAuthority`. **Not** `HasStateAuthority` — the host holds State Authority over *every* car, so that means "I am the host" |
| "Am I the host?" | `runner.IsServer`, or `HasStateAuthority` on the session singleton |
| Match start | Host-side. `MatchmakingSessionState.TriggerStart` releases `CanMove` for every car — a client can't write networked state at all |
| Despawn on leave | Host only |
| Client → host requests | `[Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]`. Routes correctly and needed no change across the topology switch |

## Matchmaking flow (as of 2026-09-16)

This is the owner-specified flow; treat it as the spec.

```
open the game → Loading (black) — auto-connect, host spawns & seats your car
→ Idle: arena, your robot, [Quick Match]. Nothing happens until pressed.
→ Searching: "Searching for players… Ns" from 0 (your own), Cancel available
   ├─ nobody: at 30 s → WaitingSolo: [Start with computer players] → starts immediately
   └─ a second searcher appears → Found, on BOTH: "Player found! Joining lobby in 3…2…1"
        (you don't see each other yet; Cancel still works)
        → lobby: you see each other + names; timer becomes SHARED = the highest one
        at 30 s shared → WaitingReady: [Play with computer players] marks you READY
        (tag above your robot, button → [Unready]); all lobby players ready → start
        with bots; otherwise the timer keeps counting and searching continues.
   6 searching → immediate start, no bots.
```

Mechanics: connection is automatic on scene start (`MatchmakingFlowController.Start`), the
loading screen stays until `IsLocalPlayerSpawned`. Quick Match sends `RPC_SetSearching(true)`;
the host stamps `SearchStartTick`. The moment two or more are searching, the host stamps
`FoundTick` on every searcher who doesn't have one (`PlayerMatchState.MarkFound`); each peer
counts `LobbyJoinCountdownSeconds` (3 s) down from it, and `InLobby` flips on the same tick
everywhere — that's what makes both cars appear at once. A later joiner gets their own
countdown; the players already in the lobby just see them appear when it ends. If the partner
cancels mid-countdown the host aborts it (`AbortFoundCountdown`) and you drop back to plain
searching. The shared timer is the highest among `InLobby` players
(`MatchmakingSessionState.SharedElapsedSeconds`); before you're in the lobby you see your own.
Cancel is `RPC_SetSearching(false)` — you stay connected and seated. The host decides the
start in `MatchmakingSessionState.FixedUpdateNetwork`; the solo start goes through
`RPC_RequestStartWithBots`. Values live in `MatchmakingConfig`.

**Visibility:** your own car is always visible; another player's car appears only once you are
*both* `InLobby` (`PlayerLobbyVisibility` toggles `Visual`/`NameTag`). Someone sitting in the
arena without searching, or still counting down, is not shown. The READY line on the name tag
comes off when the match starts (`CanMove`).

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

## Host migration

**What the player sees.** Host drops → every car freezes, "Host disconnected — reconnecting…"
(no number: this part is Photon's — host-loss detection, election, reconnects, typically
4–10 s) → "Waiting for players…" → "Game resumes in 3… 2… 1" (this part is ours, counted to one
shared tick so everyone unfreezes together) → play continues from the last snapshot. The old
host's car is a bot from then on; anyone who doesn't make it back inside the reconnect window
is a bot too. In the lobby the same hand-over happens without the freeze.

**Mechanism** (Fusion 2.1 host migration, `NetworkProjectConfig > HostMigration`, snapshot
every 2 s):

1. `OnHostMigration` reaches every survivor with a `HostMigrationToken`
   (`RunnerCallbackRelay` → `QuickMatchFusionService.HandleHostMigration`). The service raises
   `HostLost`, detaches the session object, shuts the dead runner down, and starts a fresh one
   with the token — invisible to matchmaking, open for the reconnects. Up to 5 attempts; then
   `SessionFailed` and the flow controller starts over as if the game had just been opened.
2. On the elected peer only, `HostMigrationResume` re-spawns every object from the old host's
   snapshot: same prefab, `CopyStateFrom` for all networked state, snapshot position/rotation
   from `NetworkTRSP.Data`. Nothing is owned yet.
3. `IAfterHostMigration` on the new host: `PlayerMatchState` re-bases its tick-based timers
   (search timers restart, a found player goes straight to `InLobby`);
   `MatchmakingSessionState` marks `Migrating`, sets `Frozen` if a match was on, hands the old
   host's car (`WasHost`) to the bot driver, and raises `HostMigrationRestored`.
4. Players reconnect as **new `PlayerRef`s**. Identity is the **connection token**
   (`PlayerIdentity.InstallId`, a per-install GUID in PlayerPrefs, sent on every `StartGame`
   and stamped on the car as `OwnerToken`). `PlayerLobbySpawner` matches a joiner's token to an
   ownerless car and `AssignInputAuthority`s it back; a join that lands before the resume has
   spawned the cars (the new host's own, typically) waits in `pendingReclaims` until
   `HostMigrationRestored`.
5. Ownership changes have no Fusion callback, so `PlayerMatchState.Render` and
   `PlayerMovement.Render` poll `HasInputAuthority` — that's what re-points `Local`, the roster
   and the camera. `PlayerCamera` holds its last view while the car is gone.
6. `MatchmakingSessionState.TickMigration` (host): mid-match, once every car with an
   `OwnerToken` has an owner again — or `ReconnectWindowSeconds` (10 s) passes — it sets
   `ResumeAtTick` = now + `ResumeCountdownSeconds` (3 s); at that tick it un-freezes, hands any
   still-ownerless car to the bot driver, and closes the session again. In the lobby it just
   despawns cars whose players never returned once the window closes.
7. `PlayerMovement.FixedUpdateNetwork` while `Frozen` writes zero velocity and skips the model;
   a car with no input authority on the host is driven by `BotDriver.Think` (placeholder: it
   parks — the real AI is Phase 6 and replaces only that method).

Flow side: `IQuickMatchService` gained `HostLost` / `SessionRestored` / `SessionFailed`;
`MatchmakingPhase` gained `Reconnecting` and `Resuming`; `MatchmakingFlowController` derives
the phase after any (re)connect from replicated state (`PhaseFromSessionState`) instead of
assuming Idle. The matchmaking canvas is no longer disabled on match start — the overlays live
on it — `Starting` simply shows no panel.

**Leaver → bot** (same mechanism, no migration needed): `PlayerLobbySpawner.HandlePlayerLeft`
despawns a car in the lobby but hands it to the bot driver mid-match, keeping name, position,
and later score and Crown (FD-07: the field stays at six).

**Known limits.** Play rewinds to the last snapshot (≤ 2 s). Physics internals aren't restored —
only what `NetworkRigidbody` carries, which is all the drive model needs. There is no round
timer yet; when Phase 5 adds one it must be migration-aware (design's ruling on pause-vs-run
during the freeze is pending). Bot name display (keep the leaver's name or not) is also
design's call — currently the name stays.

## Physics stepping

`RunnerSimulatePhysics` (Fusion Physics addon) is attached to the runner in
`QuickMatchFusionService`, so PhysX steps once per Fusion tick instead of on Unity's own 50 Hz
FixedUpdate. Before this, the drive model wrote velocity at 60 Hz against a 50 Hz integrator —
some writes integrated twice, some never — which was a visible jitter source and broke doc 03's
"same input, same tick, same result" premise. `PlayerMovement` carries
`[DefaultExecutionOrder(-50)]` so its velocity write lands before the step in the same tick.
Rigidbody interpolation is off (`NetworkRigidbody` interpolates the `View` child for rendering).

## Replicated simulation state

Anything that accumulates across ticks must be networked and restorable, per doc 03's
determinism contract — a rollback restores drive state, not PhysX internals.

- `PlayerMovement`: `YawRate`, `BrakeHoldSeconds` (accumulating), `SteerInput` (cosmetic, but
  every peer needs it to draw remote wheels turning)
- `PlayerMatchState`: `IsReady`, `IsSearching`, `CanMove`
- `MatchmakingSessionState`: `MatchStarting`, `BotCount`, `SearchStarted`, `SearchTimer`

## Physics replication

`NetworkRigidbody` (Fusion Physics addon) on `PlayerCar.prefab`, with the `View` child as its
interpolation target. It networks position, rotation, linear and angular velocity, restores all
four before a resimulation, and moves only `View` for rendering — the physics root is never
touched by a render write. `NetworkProjectConfig.PhysicsForecast` is **off**.

**Why not `NetworkTransform` + Forecast (what was here until 2026-09-16).** `NetworkTransform`
knows only the transform. On a rollback it teleported the chassis to the host's position but
PhysX kept the client's *predicted* velocity, so every resimulation ran from a mismatched state
and the client's own car snapped between where it predicted and where the host said it was —
the heavy flicker seen in the .exe. Forecast on top of `RunnerSimulatePhysics` meant two Fusion
physics strategies stepping the same body. And `NetworkTransform`'s render interpolation wrote
the root transform every frame, which Unity pushes into PhysX before the next step — the small
wobble seen on the host. The earlier note that `NetworkRigidbody` was "ruled out by doc 03"
misread the doc: it rules out PhysX *driving the vehicle*, not networking the body's state.
Doc 03's determinism contract — restore state, re-simulate, land on the same result — is
exactly what `NetworkRigidbody` supplies and `NetworkTransform` can't, since linear velocity is
accumulating state the model reads back each tick.

Consequences: `PlayerCamera` follows `View` (position/facing) and reads velocity from the
Rigidbody; `Visual` and `NameTag` sit under `View`; colliders stay on the root.
`NetworkRigidbody` also replicates the Rigidbody's constraints from the host, which is one more
reason `constraints = None` is set in code on every peer.

## Verified

1. `GameMode.Single` — networked movement conversion drives correctly, no second peer.
2. `GameMode.Host`/`Client`, two real processes — car motion replicates.
3. `GameMode.Shared`, per-player spawned cars — confirmed working end to end before the switch
   away from it.
4. Matchmaking merged into one scene; connect-on-start with Find Match as a searching flag;
   shared lobby search timer; remote wheel-steer replication.

5. Host/Client with two real peers (2026-09-14 test report): both cars spawn under the host,
   both drive, the match starts for both.

6. Two real peers (2026-09-16 test report): lobby flow, name tags, lobby camera, physics
   stepping all run. Found: heavy position flicker on the client's own car, slight on the host
   — diagnosed as `NetworkTransform` + Forecast on a Rigidbody (see Physics replication).

7. Two real peers, second pass (2026-09-16): no flicker with `NetworkRigidbody`; Found
   countdown and READY clear work. The other-PC-hosts direction works once both builds are
   current (an older .exe on the other side has a different networked-state layout).

**Untested since (2026-09-16, host migration):** everything in "Host migration" above, plus the
session closing on start and the damping fix (`TopSpeed` is now actually reachable — expect to
retune `Acceleration`/`CoastDrag`). Test plan, three PCs ideal, two workable:
- Lobby migration: A hosts, B joins, both searching → A quits → B sees "reconnecting…", then is
  back in the lobby alone, timer restarted, A's car gone after ~10 s.
- Match migration: A hosts, A+B start (bots) → A quits → B: freeze → "reconnecting…" →
  "Waiting for players…" → "Game resumes in 3, 2, 1" → B drives; A's car sits parked (bot).
- With C as a client too: after A quits, both B and C come back, both see the countdown at the
  same moment, both cars keep their names and positions.
- Leaver bot: A hosts, B joins a match, B quits → B's car stays, parked, name on it.
- Mid-match join: A starts with bots, B opens the game → B gets his own fresh session, never
  sees A. Rebuild scene + prefab (`Game/Setup Game Scene`) and both builds first.

## Open

- **Tick rate (ruling N2)** — FD-05 says the mode can't be finally tuned until it's ruled on; at
  30Hz the drift release window's wall-clock duration doubles. `NetworkProjectConfig` currently
  uses Fusion defaults.
- **`ParticipantRef` vs `PlayerRef`** — bots have no `PlayerRef`, so Glowtag addresses every
  participant by a `ParticipantRef` (FD-05/FD-07). Current code is `PlayerRef` throughout. Needs
  an abstraction before bots land.
- **`Game.unity` in Build Settings** — must be added manually after first running
  `Game → Setup Game Scene`, since the scene and its GUID don't exist until then.
