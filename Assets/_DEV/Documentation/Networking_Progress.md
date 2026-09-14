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

**Untested since:** the Host/Client switch itself, and everything in Phase 1. Next real test
should confirm both cars spawn under the host, both drive, the timer is synced, and the match
starts for both.

## Open

- **Tick rate (ruling N2)** — FD-05 says the mode can't be finally tuned until it's ruled on; at
  30Hz the drift release window's wall-clock duration doubles. `NetworkProjectConfig` currently
  uses Fusion defaults.
- **`ParticipantRef` vs `PlayerRef`** — bots have no `PlayerRef`, so Glowtag addresses every
  participant by a `ParticipantRef` (FD-05/FD-07). Current code is `PlayerRef` throughout. Needs
  an abstraction before bots land.
- **`Game.unity` in Build Settings** — must be added manually after first running
  `Game → Setup Game Scene`, since the scene and its GUID don't exist until then.
