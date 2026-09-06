# _DEV — Current Milestone

This folder holds active work for the current milestone only. `Future_DEV` holds the
shelved v0.3-style Create/Join Room system (room codes, private/public rooms) — kept intact
for when that scope comes back, not part of this build.

## Layout

- **Matchmaking/** — Find Match flow: Quick Match via Photon Fusion, a 0-120s search timer
  (bot/ready option unlocks at 30s), the waiting-room placeholder visualization (fixed 4-slot
  layout, self-centered per client), ready-toggle + bot-fill start logic. Real Fusion backend
  wired and under active two-instance testing — see `Documentation/Matchmaking_Progress.md`
  for full detail, current status, and bugs already found/fixed.
- **Game/** — the actual gameplay scene and robot/flag logic. Not started — depends on the
  Matchmaking flow reaching `StartMatch` first.
- **AIBots/** — bot AI core (movement, targeting, difficulty). Not started — comes after the
  real player controller exists, per the two-phase plan: (1) matchmaking logic with
  placeholder shapes first, (2) real player controller, (3) bots that can play against/with
  the player, then test together.
- **Documentation/** — progress notes for whatever's currently active here. Docs for shelved
  work move to `Future_DEV/Documentation/` instead, so this folder never goes stale.

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
