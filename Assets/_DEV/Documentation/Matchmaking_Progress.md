# Find Match — Matchmaking Progress Notes

Last updated: 2026-09-06

Written so work can continue in a fresh chat session (this machine or another) without
re-deriving everything. Covers what was decided, what's built, what was already tested and
fixed once, and what's still open.

## How we got here

The full v0.3/v0.4 Matchmaking & Lobby System (Create Room, Join by Code, room codes,
private/public visibility) was built once, then the manager cut it as too much for this
milestone. That entire system now lives in `Assets/Future_DEV/` — intact, kept for when that
scope comes back. Its own progress notes are at
`Future_DEV/Documentation/MultiplayerLobby_Progress.md` (moved there from `_DEV` since it
describes shelved work, not current work — `_DEV/Documentation` should only ever hold docs
for what's actually active).

A fresh `Assets/_DEV/` holds the current milestone: `Matchmaking/` (this doc's subject),
`Game/` and `AIBots/` (both just README stubs, not started — see their own READMEs for why).

## The actual spec (owner's own words are the source of truth)

Pressing "play" opens a waiting-room scene (the polished "robot stands in the middle"
presentation is explicitly deferred — everything here is placeholder shapes, no art):

1. One "Find Match" button.
2. On click: a search timer starts, displayed counting **up from 0 to a 120-second cap**.
   - If another real player is also searching within that window, both land in the same
     Fusion session. **Each client renders itself as the "center" slot** with other joined
     players positioned around it — PUBG Mobile-style. This is explicitly **local, per-client,
     no authority** — player A's screen and player B's screen legitimately disagree about who's
     centered, forever, by design. Up to 4 players (this game's current max).
   - If the room fills to 4 before the timer matters, the match starts immediately — no
     button, no vote, no bots.
3. **The bot-fill option unlocks at 30 seconds elapsed** (not the full 120s cap — those are
   two different numbers, see "Bugs found and fixed" below):
   - **Solo** (nobody else ever joined): a single "Don't wait, start with computer players"
     button appears. Clicking it starts immediately with bots filling the other 3 slots.
   - **Not solo** (2–3 real players present): each player gets what is functionally **one
     control** — a ready toggle labeled "Don't wait, play with computer players" — shown above
     their placeholder when active. The match starts once **every currently-present real
     player** has toggled ready (unanimous), filling whatever's left with bots at that moment,
     fixed and never renegotiated after.
4. The session stays open to new joins throughout the waiting state (per the reference docs'
   own framing) — so a solo player can still be joined by someone else after their own 30s
   mark, and a room that drops back to one player falls back to the solo path. Both handled in
   `MatchmakingFlowController.Update()`.

## Architecture (SOLID, same conventions as the shelved work)

Global namespace (no `namespace` blocks — matches this project's convention throughout,
including `Future_DEV`). `Ok`/`Fail` result types matching `GameModels.cs`'s pattern. Small,
single-purpose interfaces. One composition root.

```
Assets/_DEV/Matchmaking/
  Config/MatchmakingConfig.cs        MaxPlayers=4, SearchDurationSeconds=120 (display cap),
                                      BotOptionUnlockSeconds=30 (bot/ready option gate), GameId
  Models/MatchmakingModels.cs        StartQuickMatchResult (Ok/Fail), MatchmakingPhase enum
  Services/
    IQuickMatchService.cs            StartQuickMatch(gameId, onResult) / CancelQuickMatch()
    IActiveQuickMatchSession.cs      Everything MatchmakingFlowController needs — see below
    QuickMatchFusionService.cs       real backend: GameMode.AutoHostOrClient, no SessionName
    FusionActiveQuickMatchSession.cs IActiveQuickMatchSession over a live NetworkRunner
    QuickMatchLocalService.cs        no-network fake (PlayerCount fixed at 1) — solo-path-only
                                      UI iteration without needing two builds
    PlayerLobbySpawner.cs            host-only: Spawn/Despawn PlayerLobbyState per join/leave
  Network/
    PlayerLobbyState.cs              [Networked] IsReady; RPC_SetReady; RPC_RequestStartWithBots
    MatchmakingSessionState.cs       [Networked] SearchTimer (TickTimer)/MatchStarting/BotCount;
                                      host-only FixedUpdateNetwork win-condition check;
                                      ElapsedSeconds/BotOptionUnlocked computed here once,
                                      read by FusionActiveQuickMatchSession rather than
                                      duplicating the elapsed-time formula
  Presentation/
    LobbySeatAnchors.cs              scene Transforms: 1 center + 3 "other" anchors
    LobbySlotView.cs                 local-only placeholder: capsule + TMP name tag + ready label
    LobbySeatAssigner.cs             per-client: self -> center, others -> remaining anchors
  Flow/
    MatchmakingFlowController.cs     Idle -> Searching -> (WaitingSolo | WaitingReady) -> Starting
    MatchStarter.cs                  StartMatch(realPlayerCount, botCount) — logs/stub; Game/
                                      scene doesn't exist yet, this is the seam a future pass
                                      replaces with a real handoff
  UI/MatchmakingScreen.cs             pure binding, no logic of its own
  Core/MatchmakingServices.cs         composition root — named this, not "Services", because
                                       Future_DEV/Scripts/Core/Services.cs already claims that
                                       name and both compile into the same Assembly-CSharp
                                       (no .asmdef boundary anywhere in this project)
  Editor/MatchmakingSceneSetup.cs     [MenuItem("Multiplayer/Setup Matchmaking Scene")] —
                                       idempotent, builds the two networked prefabs (under
                                       Resources/, required for Resources.Load), the local
                                       placeholder prefab, and the whole scene, wiring every
                                       reference. Also calls Fusion's
                                       NetworkProjectConfigUtilities.RebuildPrefabTable() so
                                       the networked prefabs are registered automatically.
  Resources/PlayerLobbyState.prefab, MatchmakingSessionState.prefab
  Prefabs/LobbySlotView.prefab
  Scenes/Matchmaking.unity
```

**Key design point — what's networked vs. purely local** (the crux of "no one has
authority"): player count and the search timer are network-synchronized (`TickTimer` on
`MatchmakingSessionState`, so a player joining mid-search sees the true elapsed time, not a
fresh countdown); ready state is per-player networked data written only via RPC to the host.
Seat/anchor placement is **never** networked — `PlayerLobbyState`'s prefab deliberately has no
renderer and no `NetworkTransform`, because if position were synced, Fusion's own replication
would fight the requirement that every client positions everyone (including itself)
differently. `LobbySeatAssigner` handles that purely locally, per client.

**Reused from `Future_DEV` in place, not copied**: `RunnerCallbackRelay.cs` and
`RoomCodeGenerator.cs` — both zero-dependency Fusion utilities. Copying would require renaming
them anyway (duplicate-symbol, since both trees compile into the same assembly). If
`Future_DEV` is ever actually deleted, move these two files into `_DEV/Matchmaking/Core/`
first — they don't depend on anything else there.

## Fusion API facts used, verified against this project's installed Fusion.Runtime.dll (2.1.2)

Not recalled from general Fusion knowledge — checked directly against the compiled assembly
and its shipped XML docs, and cross-checked against two of Photon's own sample scripts pulled
from the still-zipped demo `.unitypackage`s, after this project got bitten twice by subtly
wrong signatures. Confirmed exact: `GameMode.AutoHostOrClient` (Photon's own "quick join" mode
— first searcher transparently becomes host), `NetworkRunner.Spawn(...)`/`.Despawn(...)`,
`NetworkRunner.IsServer`, `[Networked]`/`NetworkBool`, `TickTimer.CreateFromSeconds` /
`.RemainingTime` / `.Expired`, `[Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]`,
`NetworkBehaviour.{Spawned(), FixedUpdateNetwork(), Despawned(NetworkRunner, bool),
HasStateAuthority, HasInputAuthority}`, `NetworkObject.InputAuthority`,
`NetworkProjectConfigUtilities.RebuildPrefabTable()` (public, in `Fusion.Editor`).

**Open verification item, not yet confirmed**: whether `AutoHostOrClient` actually filters
candidate sessions by `SessionProperties[GameId]` or only tags the resulting session for
bookkeeping. Matters once a second game shares this Photon App ID.

## Bugs found during real two-instance testing (`.exe` + Editor Play), and the fixes

1. **Nothing happened between the two instances at all.** Root cause: `MatchmakingServices.cs`
   still pointed at `QuickMatchLocalService` (the no-network fake) — each instance was running
   its own isolated fake "solo" session, never actually connected. There was never any real
   networking happening in that test. **Fixed**: switched the composition root's `quickMatch`
   field to `new QuickMatchFusionService()`.
2. **The bot-fill option should appear at 30s, not the 120s display cap** — these had been a
   single value (`SearchDurationSeconds`). **Fixed**: split into `SearchDurationSeconds` (120,
   display cap / `TickTimer` duration) and `BotOptionUnlockSeconds` (30, gates the solo button
   / ready toggle). Renamed `IActiveQuickMatchSession.SearchExpired` →
   `BotOptionUnlocked` throughout, since its meaning diverged from "timer fully done" to
   "unlock threshold reached" — kept the old name would have been actively misleading. The
   elapsed-time/unlock computation now lives once, on `MatchmakingSessionState`, and
   `FusionActiveQuickMatchSession` reads through it rather than duplicating the formula.

Both fixes need a **fresh `.exe` build** to take effect — the existing build has the old logic
baked in.

## How to test

**Solo/no-network iteration**: flip `MatchmakingServices.cs`'s `quickMatch` field to
`new QuickMatchLocalService()`. Press Play, Find Match, watch the elapsed text climb, confirm
the solo bot button appears at 30s (not 120s) and `MatchStarter` logs `(1 real, 3 bots)`.

**Real two-instance test** (current state — backend is live Fusion): build the `.exe`
(`Assets/_DEV/Matchmaking/Scenes/Matchmaking.unity` needs to be scene 0 in Build Settings),
run it alongside Editor Play, click Find Match in both within a few seconds of each other.
Expect `PlayerCount == 2` on both, each client showing itself centered with the other's
placeholder to the side (and — this is the actual test of "no one has authority" — **the two
screens should genuinely differ**, not show the same arrangement). At 30s both should show the
ready toggle; toggling both should fire `MatchStarting` and log the correct bot count.

**Not yet visually confirmed** (no way to run Unity from this side): whether the scene's
default camera is actually framed to show the seat anchors — they sit roughly in front of a
default-positioned Main Camera, but this hasn't been eyeballed. If the waiting room appears
empty, this is the first thing to check.

## Not started

`Game/` (real gameplay scene, real player controller) and `AIBots/` (bot AI core) — both
explicitly deferred until the Matchmaking flow above is fully proven out. `MatchStarter.cs` is
the seam where a real scene-load/handoff replaces the current debug-log stub.
