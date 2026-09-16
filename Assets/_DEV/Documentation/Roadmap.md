# Roadmap

Ordered by **risk**, not dependency: the game-defining unknown is "does a body-check feel good
and agree across the network", so that gets proven early while it's still cheap to change.

Source of truth is the GDD under `Assets/GDD/`. Two sets: the **Driving Foundation**
(`Robot Games Core...`, docs 01-08) which is platform-level, and **Glowtag** (FD-00 to FD-07)
which is this game on top of it. Where they disagree with anything here, they win.

## The game, in one line

One Data Crown in a closed arena. Whoever holds it scores continuously; everyone else drives
into them to take it. Highest score at 90 seconds wins. Six participants, bots filling any slot
a real player didn't. Possession is the verb, score is the win condition.

## Rulings made

| Question | Ruling |
|---|---|
| Network topology | **Host/Client** (`AutoHostOrClient`), invisible to players. Confirmed by Glowtag FD-05: *"Glowtag's position: Host mode"* — Shared mode can't resolve contested steals on one authority, and gives bots no owner |
| Participants | **6**, per FD-07. Bots fill every empty slot, so the field is always full |
| Handling values | **Serialized fields first**, lifted to ScriptableObject profiles once tuned |
| Arena scale | **Serialized** on `ArenaBounds`, editable in the inspector, until the art team's arena arrives |
| Host disconnect | **Host migration** — freeze, hand over, shared "resumes in 3" — owner's ruling 2026-09-16 after the first two-PC test lost a match to the host quitting. FD-05's "accepted knowingly" is superseded. Options brief given to design; their rulings on round-timer pause and bot naming are pending |
| Player leaves mid-match | **Car becomes a bot in place** (FD-07: the field stays at six). Keeps name, position, later score and Crown |

## Phase 0 — Network topology ✅

`GameMode.Shared` → `AutoHostOrClient`. Host spawns every car and holds State Authority over
all of them, so `HasStateAuthority` no longer means "mine" — ownership checks moved to
`HasInputAuthority`. `CanMove` is released host-side in `MatchmakingSessionState.TriggerStart`,
since a client can't write networked state.

## Phase 1 — Drive model core ✅

GDD doc 03, milestones M1 / M2 / M4. Full detail in `Vehicle_Model.md`.

Built to the doc's fixed step order, with velocity composed and written per tick rather than
accumulated through `AddForce`. Probe positions and wheel radius are measured from the art at
prefab build time instead of guessed. Attitude is **levelled, not frozen** — the chassis conforms
to slopes and always returns upright, which is what doc 03 asks for ("force-levelled", not
"unable to rotate"). Colliders are a compound of primitives approximating the robot silhouette:
body box raised clear of ride height, plus a sphere per wheel.

Deliberately **not** in Phase 1: slope/grade (M3), surface tags, airborne polish, drift & boost.

Kept untouched, by request: spawning, `PlayerCar.prefab`, `PlayerMatchState`, matchmaking flow,
camera wiring.

**Known regression until Phase 2:** wall and car contact is crude, because a direct velocity
write partly overwrites PhysX's collision response. That is what Phase 2 replaces with authored
resolution — not something to patch here.

## Phase 1.5 — Matchmaking flow & simulation stepping (2026-09-15) ✅ built, ⏳ untested

Not a GDD phase — a pass over what Phase 0/1 exposed once two people actually sat in the lobby.
Full spec in `Networking_Progress.md` ("Matchmaking flow"), physics detail in `Vehicle_Model.md`.

**Matchmaking flow.** Connects on scene start behind a black `Loading` panel (new first
`MatchmakingPhase`) that lifts when your own car is seated (`IsLocalPlayerSpawned`). Quick Match
is a flag, not a connect. Timer is per-player (`SearchStartTick`) and the shown value becomes
the **highest** of everyone searching (`MatchmakingSessionState.SharedElapsedSeconds`) — the
first fix synced everyone to the first searcher's clock, which restarted nobody's timer but
started late joiners at whatever the first searcher had reached. 30 s unlocks the bot option:
solo → immediate; 2+ → "Play with computer players" is the ready toggle; 6 → immediate start.

**Lobby presentation.** `PlayerIdentity` + `PlayerNameTag` (networked `DisplayName`, world-space
tag). `LobbyLayout` seats cars in a row by `PlayerId` and places the lobby camera; other cars
are hidden until both of you are searching (`PlayerLobbyVisibility`). `RunnerCallbackRelay`
moved from `Future_DEV` into `Matchmaking/Network/` so live code has no `Future_DEV` dependency.

**Simulation stepping.** `RunnerSimulatePhysics` (Fusion Physics addon — the stepper only, no
`NetworkRigidbody`) now runs PhysX once per Fusion tick. Before, the 60 Hz velocity write raced
Unity's 50 Hz `FixedUpdate`; a tick with no physics step behind it moved nothing, then the next
one moved double. `PlayerMovement` is `[DefaultExecutionOrder(-50)]` so it runs before the
stepper; Rigidbody interpolation is off (Fusion's own interpolation owns visual smoothing).

**Two Phase 1 corrections.** Lateral grip was a fraction-per-tick (`0.86` × 60/s — a rail; the
drift button barely slid) and is now a per-second rate (`GripRate` 20, `DriftGripRate` 3, decay
`exp(-rate·dt)`). And `constraints = None` is set **in code** as well as on the prefab: the
levelling in `ResolveAttitude` fights a frozen X/Z, and a stale prefab can't be allowed to
reintroduce that silently.

**2026-09-16 test report + fixes.** Two peers: lobby, name tags, camera, stepping all ran.
The client's own car flickered heavily while driving (host slightly). Cause: `NetworkTransform`
+ Forecast Physics on a Rigidbody — rollback restored position but not velocity, and Forecast
and `RunnerSimulatePhysics` both stepped the body. Replaced with the Physics addon's
`NetworkRigidbody` (position + rotation + velocity restored on rollback; render interpolation
on a `View` child, so PhysX never sees a render write), Forecast off. Flow edits from the same
session: a **Found** phase — a second searcher triggers "Player found! Joining lobby in 3…2…1"
on both from one host-stamped tick, and only then do they see each other and share the timer;
and the READY tag clears on match start.

**Confirmed:** no flicker with `NetworkRigidbody`; Found countdown and READY clear work. Two
follow-ups from that test — a parked car creeping by itself on a slope edge, and no yaw at a
standstill — traced to three drive-model defects (`Vehicle_Model.md`): gravity applied twice
(PhysX + model), the suspension's equilibrium 39% below rest height with the wheel spheres
pressed into the floor, and the `CanMove` gate missing since the Phase 1 rewrite. Fixed:
preloaded suspension, model-owned gravity (world-space while airborne), gate restored.

**Confirmed later that day:** no flicker, Found flow, READY clear. Steering at a standstill was
then ruled the *other* way — no pivoting in place, like a real car — so `SpeedScalingCurve` now
starts at zero and reverse steers with the nose swinging the other way.

## Phase 1.6 — Host migration & leaver bots (2026-09-16) ✅ built, ⏳ untested

Pulled forward from "not requested" after the first two-PC test: the friend's .exe was the host,
he closed it, the match died for everyone (`ServerLogic: Server has disconnected`). Design
brief with the three options (accept / hand over / dedicated servers) was written; owner ruled
hand-over. Full mechanism in `Networking_Progress.md`, "Host migration".

In one line: Fusion elects a new host from a 2-second cloud snapshot; players are re-matched to
their cars by a per-install connection token; the new host freezes the match until everyone is
back (10 s window) then counts all peers down to one shared resume tick; whoever doesn't return
— the old host always — is driven by `BotDriver`, whose placeholder brain parks. Same bot
takeover serves a player who simply quits mid-match. Also in this pass: the session closes to
matchmaking on start (mid-match joins were landing in running rounds), and Rigidbody damping is
zero (it was capping speed at ~9 m/s regardless of `TopSpeed`).

Built now rather than after Phase 5 on purpose: the only state to hand over today is cars and a
few flags; every feature from here on (Crown, scores, round timer) is written migration-aware
instead of retrofitted.

**Verified 2026-09-16 (2 and 3 peers):** lobby and mid-match migration, shared resume countdown,
old host's car parked as a bot, leaver bot, mid-match join refused, second migration in a row.
Two leftovers fixed after: reads of networked state from `LateUpdate` on a car whose runner was
already gone (now gated by a `HasState` flag set in `Spawned`/`Despawned` — `NetworkObject.IsValid`
itself throws on a runner mid-shutdown), and the "reconnecting" notice flashing for a few frames
on a fast hand-over (now held ≥ 1.5 s).

**2026-09-17 — connect on Quick Match, not on open.** The three-peer test surfaced the scaling
flaw in connect-on-open: sessions hold six *connected* players, so with more than six online,
sessions filled by arrival order regardless of who was searching. Sessions now hold searchers
only; the timer still starts at the click (local clock, then back-dated on the host so it never
jumps). Presentation with it: your robot alone, centred, same isometric angle (a local
`LobbyPreviewCar` is "you" until the lobby — your own networked car stays hidden like the
others'), "Player found" countdown, then a `ScreenFade` cut to the row shot.
`MatchmakingFlowController.IsSoloView` is the one source for solo-vs-group. Untested — checklist
in `Networking_Progress.md`.

## Phase 2 — Contact system ← the actual game, next up

GDD doc 05, milestones M1-M4. Split into four testable steps, in this order:

**2a — Wall behaviour.** Project velocity along the wall rather than reflecting it, speed cost
scaled by incidence angle, `min_exit_speed` floor. First because it fixes the Phase 1 regression,
because doc 05 explicitly allows wall contact to resolve **locally** (no authority plumbing), and
because it's the only part testable **solo** — everything car-vs-car needs two peers per
iteration.

**2b — Detection & classification.** Glance / bump / slam by angle within the cone, not speed
alone. Aggressor determination and contact quality. Classify and log only — confirm it labels
hits correctly before it starts changing physics.

**2c — Chassis-to-chassis resolution.** Host-authoritative momentum exchange using mass, plus the
authored arcade bonus and aggressor speed cost, with `mass_ratio_cap`.

**2d — Spin-out.** The only recovery state: networked, steering reduced but never zero, settles
facing direction of travel, cannot be re-spun while active.

*Done when:* two peers deliberately body-check and both see the same outcome, no snap.

## Phase 3 — Impact feedback

Doc 05 M7. Camera shake scaled by impulse, distinct audio per class, squash on the receiver, a
positive cue for the aggressor specifically, comic spin-out motif. Roughly half of perceived hit
quality, and doc 05 makes it a spec requirement rather than polish.

## Phase 4 — The Crown

Glowtag FD-02. One networked object with a holder. Spawn from authored candidate points with the
asymmetry rule, telegraphed. **Steal = any classified contact against the holder** — consumes
Phase 2's classification and aggressor determination directly. A spun-out holder *keeps* the
Crown; spin-out is recovery, not dispossession.

## Phase 5 — Round & scoring

Glowtag FD-01. 3s countdown → crown spawn delay → telegraph → 90s round → standings. Flat
`score_rate` per second while holding, banked per tick, no multipliers. Score is
authority-accumulated, never client-accumulated.

## Phase 6 — Completion

Bots (FD-07 — input source only, no second movement system) · drift & boost economy (doc 04,
which Glowtag elevates: `boost_bump_multiplier` 1.6× makes drift **the primary earned steal
tool**) · power-ups (FD-04) · handling profiles extracted to ScriptableObjects · art and VFX.

## Scope deleted by Glowtag FD-06

Recorded so nobody builds them: no out-of-bounds detection, no drivable volume, no respawn
anchors, no splines, no checkpoints, no racing line, no boost pads, no moving geometry. The arena
is fully closed, and manual respawn is disabled.

## Open, not blocking

- **Tick rate (ruling N2)** — FD-05 says the mode can't be finally tuned until it's ruled on; at
  30Hz the drift release window's wall-clock duration doubles.
- **Round timer during a migration freeze** — pause (fair) or keep running (simpler)? And does
  a bot keep the leaver's name? Both with design; the code currently keeps the name and has no
  round timer yet.
- **Steal rule** — shipping as "any contact" for first playtest. Tightening it to bump-or-above
  or slam-only makes bots materially weaker (FD-07 links the two decisions).
- **Arena geometry** — FD-06 wants wall angles that favour scrapes over impacts; the current
  placeholder is a square box with 90° corners, which produces head-on impacts from nothing.
- **`Assets/_Recovery/0 (1).unity`** — a Unity auto-recovery scene got committed on 2026-09-15.
  Not referenced by anything; candidate for deletion (and `.gitignore`).
