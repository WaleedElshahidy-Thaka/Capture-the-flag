# Vehicle Drive Model

Layer B of the Driving Foundation — the per-tick physics of one robot chassis. Built to GDD
doc 03 (`Assets/GDD/Robot Games Core with Matchmaking & Lobby/03_Vehicle_Drive_Model_v0_3.docx`),
which is the source of truth where this disagrees with it.

## The three decisions that shape everything

All three come from doc 03, and each one is why an earlier attempt failed:

1. **Custom arcade controller — not `WheelCollider`, not "a general rigidbody with friction
   materials doing the work."** Boost has to carry the chassis *above* nominal top speed, which a
   modelled powertrain resists; and PhysX's internal solver state doesn't survive a rollback.
2. **One rigid body with four raycast ground probes** — not four simulated wheels.
3. **Velocity is composed and written per tick**, not accumulated through `AddForce`. A
   re-simulated tick from a restored state has to land on the same result.

## How we got here

Four attempts before reading the GDD, recorded so the same ground isn't re-covered:

| Attempt | Why it failed |
|---|---|
| Single `BoxCollider` + `AddForce` | Flat faces and sharp corners give PhysX bad geometry for ground contact; caught on slope edges |
| Chassis box + 4 `SphereCollider`s | Better contact shape, but ground support was still a collider problem, not a suspension one |
| 4 `WheelCollider`s | Widely reported as jittery and hard to tune; its solver state can't roll back; ruled out by doc 03 *by name* |
| Raycast suspension + `AddForce` | Right grounding idea (independently matched doc 03), wrong integration — force accumulation isn't deterministic |

The current model is the first one built to the doc rather than guessed.

## Current state — Phase 1 (M1, M2, M4)

**`GroundProbes.cs`** — four raycasts, grounded requires 2+ in contact, spring/damper response
averaged across grounded probes. Probe origins are **baked from the art** at prefab build time
(`GameSceneSetup.WireTires` measures the tire local positions and the wheel renderer's radius),
not read live from the tire transforms — those get rotated every frame by the cosmetic steer
animation, which would make the probe origins wobble.

**`HandlingValues.cs`** — every feel value in one `[Serializable]` class. Numbers are doc 03's
provisional table. Serialized fields by decision; lifting them into a ScriptableObject later is a
wrapper around this type, not a rewrite of everything that reads it.

**`PlayerMovement.cs`** — doc 03's fixed step order: probe → suspension → longitudinal →
steering → lateral → write velocity. Target-speed-and-approach-rate longitudinal with the
no-clamp overspeed rule. Steering sets a *target yaw rate* approached through
`steer_response × inertia`. Lateral grip as fraction-removed-per-tick. Chassis is
**frictionless** — the model owns grip, so a PhysicsMaterial would be a second unmodelled force.

`SimulateTick(input, deltaTime)` is public and input-struct-driven because Glowtag FD-07 requires
bots to be an input source feeding this same pipeline, and bots have no `PlayerRef` to arrive via
`GetInput`.

**Lateral grip is a per-second rate** (`GripRate`, `DriftGripRate`; sideways velocity decays as
`exp(-rate·dt)`), not a fraction-per-tick. The first version removed 86% of sideways velocity
*every tick* — 60 times a second — which is a rail: the car snapped to its heading in two ticks
and even the drift button (Left Ctrl) barely slid. The rate form is also tick-rate independent,
which matters for the still-open N2 ruling. Doc 04's real drift state machine (hop, charge,
release window, boost) is still Phase 6; `DriftGripRate` is only the grip half of it.

**Physics is stepped by Fusion**, once per tick, via the `RunnerSimulatePhysics` addon on the
runner (see `Networking_Progress.md`). The Rigidbody has **no rotation constraints** — an earlier
prefab build still froze X/Z, which made PhysX cancel `ResolveAttitude`'s levelling every step
(the rotation jitter seen in the inspector) and stopped the chassis conforming to the ramp.

## Attitude: levelled, not frozen

An earlier version froze pitch and roll on the Rigidbody to guarantee doc 03's *"the chassis
never lands upside down."* That also stopped it conforming to slopes, and it silently overwrote
whatever was set on the prefab in the Inspector.

It's now `ResolveAttitude`: the chassis aligns to the ground normal while grounded (slope and
banked-curve conformity) and levels toward world up while airborne. It rotates freely, it just
always comes back upright. Composed as an angular velocity alongside yaw rather than as a torque,
for the same reason the linear step writes velocity — the model owns its integration.

The no-rollover rule isn't optional: Glowtag FD-06 deletes respawn entirely (closed arena,
manual respawn disabled), so a car stuck on its roof is a dead player for the rest of a
90-second round. `GroundedLevelStrength` / `AirLevelStrength` are serialized — 0 gives fully free
tumbling, at the cost of reintroducing that case.

`IsGrounded` also requires being within `MaxDriveAngle` of the ground normal. Without it, a car
on its side still found "ground" with its sideways-pointing probes and drove along the floor.

## Colliders

A compound of primitives approximating the robot's actual silhouette — **not** one box, and not
mesh colliders (non-convex meshes on a moving Rigidbody are unreliable in PhysX):

- **Body box**, raised clear above the wheels. When it sat 5cm above ride height it won ground
  contact over the suspension, and the whole car moved as one rigid block over bumps.
- **Four wheel spheres** at the measured tire positions, so the car rests and tumbles on its
  wheels rather than a box edge.

Suspension rest height is set to the measured wheel radius, so the probes hold the chassis at
exactly the height the wheel spheres would touch — the two support systems agree instead of
fighting for the same contact.

## Not built yet

- **M3 — slope model.** Grade affects top speed and acceleration uphill/downhill. Small (~40
  lines), and directly relevant now that the arena has ramps.
- **Surface tags.** No Track Data pipeline; Glowtag FD-06 authors its own arena and untagged
  geometry resolves as Default.
- **Airborne polish.** Landing should project velocity onto the ground plane rather than
  reflecting it.
- **Layer C (drift & boost, doc 04).** Only the lateral-grip half of "output one" exists — hold
  drift, grip drops, chassis slides. Missing: the hop input entirely, the state machine
  (Neutral → Hop → Sliding → Charged → Release window → Boosting → Chained/Fizzle), reserve
  accumulation, charge stages, the release window, chaining, and `drift_rotation_authority`
  (hardcoded to `1f` in `ResolveSteering`).
- **The boost button is a placeholder that's wrong.** Left Shift currently raises target speed
  directly. Boost is supposed to be earned by releasing a drift inside the window — that code
  gets deleted, not extended.

## Known regression until Phase 2

Wall and car contact is **crude right now**, because writing velocity directly partly overwrites
PhysX's own collision response. That's the expected cost of the determinism requirement, and it's
what doc 05's authored resolution replaces — not something to patch here.

## Tuning

All values are provisional by doc 03's own admission — chosen to be legible rather than correct.
The doc's own warning: the relationship between top speed and arena scale is circular and can't
be resolved on paper. Fix top speed, build one arena against it, tune the arena.
`ArenaBounds.CrossingSeconds` reports where that currently stands against FD-06's 4–6 second
target.

First value likely to want attention: `LateralGrip` at 0.86 (fraction of sideways velocity
removed per tick's worth of time).
