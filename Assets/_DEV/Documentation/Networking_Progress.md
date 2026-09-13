# Networking Progress

Scene: `Assets/_DEV/Game/Scenes/Game.unity` — the one accepted scene, matchmaking lobby and
real gameplay together. `LocalDrive.unity` also exists, deliberately non-networked, as a
separate reference/testing sandbox — see `../README.md`.

## History

1. **`Drive.unity`** (retired) — proved the networked `PlayerMovement` conversion and Forecast
   Physics replication in isolation, no matchmaking involved.
2. **`Matchmaking.unity`** (retired) — the Quick Match lobby flow (restored from git history
   after being deleted during an earlier cleanup pass, then adapted from `GameMode.Host`/
   `Client` to `GameMode.Shared`).
3. **`Game.unity`** — both combined by `Game/Editor/GameSceneSetup.cs`, plus a real gameplay
   handoff neither predecessor had.
4. **Connect-on-start, Find Match as a flag** — connecting to the shared hub moved from "behind
   the Find Match button" to automatic on scene start. Find Match no longer triggers the
   connection; it just marks this player as actively searching (`PlayerMatchState.IsSearching`),
   which is what counts toward a match starting. Players connected-but-not-searching are visible
   to everyone but never get swept into a match.
5. **One networked object per player, not two** — originally, a separate `PlayerLobbyState`
   (ready/searching data, spawned at connect) and `PlayerCar` (spawned only once a match
   started) existed side by side, which needed active syncing and produced a real bug: whichever
   was visible depended on which one had (or hadn't) been cleaned up, so a stale lobby capsule
   and the real car could both be on screen at once. Fixed by spawning the real `PlayerCar` at
   connect time and gating driving with a `CanMove` flag instead of the car's existence. The old
   3D lobby placeholders (`LobbySlotView`, `LobbySeatAssigner`, `LobbySeatAnchors`) are deleted
   entirely — you see everyone's real car from the moment they connect.

## Flagged for next session — discuss before fixing

Found during testing step 5 below. Neither is fixed yet — both need a design decision first,
not just a patch.

**1. Search timer isn't synchronized between players in the same lobby.** Two players in the
same session see *different* "Finding player... Ns" numbers. This is a direct consequence of a
deliberate choice made earlier this same pass: the timer was moved from session-wide
(`MatchmakingSessionState.SearchTimer`, one clock for everyone) to per-player
(`PlayerMatchState.SearchTimer`, starts when *that specific player* clicks Find Match) — done to
fix an earlier complaint that the displayed time didn't reflect when *you* clicked Find Match.
Those two goals are in tension: "starts exactly when I click" and "shows the same number as
everyone else in the lobby" can't both be true unless everyone clicks at the same instant.
**Needs a decision**: keep per-player (not synchronized, by design — each player's number means
something different: their own search duration) vs. go back to a shared session clock
(synchronized, but a late clicker's timer won't start at 0 for them). Worth deciding what the
number is actually *for* before picking.

**2. Remote players' wheel-steer visual doesn't replicate — only your own car's wheels turn.**
Confirmed via a real build test: Player 1 sees Player 2's car move (position/rotation replicate
correctly — Forecast Physics is working), but Player 2's front wheels never visibly turn left/
right on Player 1's screen, only on Player 2's own. Root cause (diagnosed, not yet fixed):
`PlayerMovement.LateUpdate()`'s tire-turn animation reads `steerInput`, a plain (non-networked)
field only ever written inside `FixedUpdateNetwork()` — which returns early via
`GetInput(...) == false` for any car you don't have Input Authority over, i.e. every remote
player's car, from your own client. So `steerInput` simply never updates for anyone else's car
on your screen; it stays at its default. Purely cosmetic (doesn't affect real physics or
position), but a real visual gap. **Needs a decision** on the fix approach: network the steer
value explicitly (a `[Networked] float` set every tick, one more thing to replicate), or derive
an approximate visual steer from something that already replicates (e.g. the Rigidbody's
angular velocity via Forecast Physics) instead of the raw input.

## Topology: Shared Mode, no host/client

The game is quick-match style — players just find each other and play, with no visible "who's
hosting." That rules out Host/Client (where one player's own machine is silently the arbiter)
and points at Fusion's **Shared Mode**: no single player's device holds authority, Photon's
cloud relay coordinates the session, and each player has State Authority over their own objects
because each player is the one who spawns them.

- `Matchmaking/Services/QuickMatchFusionService.cs` starts `GameMode.Shared` on scene start (see
  `MatchmakingFlowController.Start()`), no explicit `SessionName` — an empty one is Fusion's own
  "quick join": first searcher opens a session, later searchers are placed into it.
- `Matchmaking/Services/PlayerLobbySpawner.cs`: each peer spawns its own `PlayerCar` only on its
  own `OnPlayerJoined` (`player == runner.LocalPlayer`), positioned by `PlayerId` (stable and
  identical on every peer — a locally-computed list index is not, which was the cause of the
  "both cars spawn in the same spot" bug). Spawning is what grants State Authority in Shared
  Mode. No manual despawn-on-leave; a peer can't despawn another peer's object anyway. Relies on
  `PlayerCar.prefab` having "Destroy When State Authority Leaves" enabled for cleanup — **verify
  this checkbox is on**.
- The singleton `MatchmakingSessionState` spawns via `runner.IsSharedModeMasterClient`
  (Shared Mode's closest analog to "the host") instead of the old `runner.IsServer`.
- `Matchmaking/Flow/MatchStarter.cs` flips `PlayerMatchState.Local.CanMove` once
  `MatchmakingSessionState.MatchStarting` fires — no spawning happens there anymore, the car
  already exists.

## Physics replication: Forecast Physics, not the Physics Addon

Two ways Fusion can network a real Rigidbody-driven car:

- **Physics Addon's `NetworkRigidbody`** (full client-side prediction + resimulation) —
  explicitly refuses to run in Shared Mode (its own source despawns the object with a warning
  if you try). Not used.
- **Core `NetworkTransform` with Forecast Physics** (extrapolates from the Rigidbody's last
  known velocity instead of resimulating, with spring/damper correction on drift) — has
  explicit Shared Mode support built in. This is what's used: a plain `NetworkTransform`
  component with `PhysicsSettings.ForecastEnabled = true`, gated globally by
  `NetworkProjectConfig.PhysicsForecast = true`
  (`Assets/Photon/Fusion/Resources/NetworkProjectConfig.fusion`).

## Key files

- `Game/Player/PlayerMovement.cs` — `NetworkBehaviour`, physics only. `FixedUpdateNetwork()`
  applies throttle/steer/boost/drift forces from `GetInput<PlayerNetInput>`, but only once its
  sibling `PlayerMatchState.CanMove` is true. `Spawned()` claims Input Authority and wires the
  camera when `HasStateAuthority` is true (this is the local player's own car).
- `Game/Player/PlayerMatchState.cs` — sibling `NetworkBehaviour` on the same `PlayerCar`
  object, ready/searching/timer/`CanMove` state. Deliberately a separate component, not merged
  into `PlayerMovement` — "am I allowed to move" and "how do I drive" are different concerns
  (see the SOLID note below). Owns the per-player search timer (`ElapsedSeconds`,
  `BotOptionUnlocked`) — starts from when *this* player clicked Find Match, not from session
  creation.
- `Game/Player/PlayerNetInput.cs` / `PlayerInputSampler.cs` — the networked input struct and
  its keyboard sampling, called from `QuickMatchFusionService`'s `RunnerCallbackRelay.OnInputAction`.
- `Game/Player/Resources/PlayerCar.prefab` — built by `GameSceneSetup.cs`'s
  `BuildPlayerCarPrefab()` (menu: `Game → Setup Game Scene`). The one networked object per
  player, spawned by `PlayerLobbySpawner` at connect time.
- `Matchmaking/Network/PlayerMatchState.Active` / `RosterChanged` — replaces the old
  `PlayerLobbyState.Active`; same role (single source of truth for who's connected), now on the
  real car's own state component.
- `Matchmaking/Flow/MatchmakingFlowController.cs` — the Idle → Searching →
  (WaitingSolo | WaitingReady) → Starting state machine, backend-agnostic. `Start()` connects
  automatically; `FindMatch()` just sets the searching flag; `Update()` notices
  `Session.MatchStarting` and calls `MatchStarter.StartMatch` exactly once.
- `Matchmaking/Flow/MatchStarter.cs` — the gameplay handoff: flips `CanMove`, hides the
  matchmaking canvas. Bots not handled yet (no bot AI exists).

## Design notes (SOLID / clean-code choices made along the way)

- **Single Responsibility**: `PlayerMovement` (physics) and `PlayerMatchState` (ready/searching/
  movement-gate) are separate components on the same `NetworkObject` rather than one merged
  class, even though merging would have meant one fewer file. `PlayerMovement` only ever reads
  one bool (`CanMove`) from its sibling; it owns none of the matchmaking concept.
- **Interface Segregation / Dependency Inversion**: `MatchmakingFlowController` and
  `MatchmakingScreen` depend only on `IActiveQuickMatchSession` / `IQuickMatchService`, never on
  Fusion types directly — `QuickMatchLocalService` (fake, solo) and `QuickMatchFusionService`
  (real) are swappable behind the same contract, unchanged by this pass.
- **DRY, avoiding a redundant parallel state object**: the original two-object design
  (`PlayerLobbyState` + `PlayerCar`) required keeping two `NetworkObject`s, two spawn paths, and
  two cleanup paths in sync for what was conceptually one player. Collapsed to one spawn, one
  object, two focused components.
- **No dead code carried forward**: `LobbySlotView`, `LobbySeatAssigner`, `LobbySeatAnchors`,
  `PlayerLobbyState` and their prefabs are deleted outright, not left disabled/unused.
- **Consistency over cleverness**: the `PlayerId`-based spawn positioning, the
  `HasStateAuthority`-based "is this mine" check, and the self-spawn-on-own-join pattern are
  now used identically in three places (`PlayerLobbySpawner`, `PlayerMovement.Spawned()`,
  `PlayerMatchState.Spawned()`) rather than each solving "which object is mine" its own way.

## Status

Verified so far, each as its own step (smallest testable slice first):

1. `GameMode.Single` (in the now-retired `Drive.unity`) — networked `PlayerMovement` conversion
   drives correctly with no second peer.
2. `GameMode.Host`/`Client`, two real processes — confirmed the same car's motion replicates
   from Host to Client.
3. `GameMode.Shared`, per-player spawned cars — **confirmed working end-to-end**: two real
   peers, each spawns and drives its own car, each sees the other player's car moving too.
4. Matchmaking lobby merged into the same scene, real `PlayerCar` wired into `MatchStarter` —
   tested; found and fixed two bugs (same-position spawning, stale lobby placeholders) and a
   UX mismatch (connection behind Find Match instead of automatic) — see History above.
5. One-object-per-player redesign + per-player search timer — tested with a real build (two
   peers). Core flow works: both connect automatically, see each other as real cars from the
   start, match starts, `CanMove` flips correctly. Surfaced the two issues above (timer sync,
   remote wheel-steer visual) — both need a decision before fixing, see "Flagged for next
   session".
6. Connecting/loading UX — `MatchmakingPhase.Connecting` added, Find Match disabled with a
   "Loading Game... Ns" message until the connection completes. Not yet re-tested since adding
   this (it landed after the step 5 build test above).

## Known gaps / not yet built

- No bot AI — `MatchStarter.StartMatch`'s `botCount` parameter is currently just logged, no
  bot-driven car gets spawned for it.
- No player-vs-player collision/contact resolution logic beyond what PhysX + Forecast Physics's
  correction heuristics give for free.
- No visual distinction between your own car and another player's.
- `Game.unity` needs to be added back to Build Settings manually after first running
  `Game → Setup Game Scene` in the Editor (the scene file — and its GUID — doesn't exist until
  then, so this couldn't be done from outside Unity).
