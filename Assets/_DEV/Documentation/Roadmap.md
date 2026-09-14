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
- **Steal rule** — shipping as "any contact" for first playtest. Tightening it to bump-or-above
  or slam-only makes bots materially weaker (FD-07 links the two decisions).
- **Arena geometry** — FD-06 wants wall angles that favour scrapes over impacts; the current
  placeholder is a square box with 90° corners, which produces head-on impacts from nothing.
