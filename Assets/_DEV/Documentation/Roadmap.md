# Roadmap — Upcoming Development

Forward-looking work, separate from `Networking_Progress.md` (which covers what's already
built and how). Everything here is **not started** — none of it exists in the project yet.
Several items depend on game-design decisions that aren't made yet, flagged explicitly below
rather than assumed.

## The 4 next tasks

### 1. Player contact & collision resolution

**Now**: PhysX + Forecast Physics already handle raw physical response (cars bounce/push off
each other and the arena walls) — but nothing *means* anything when it happens. There's no
concept of a "hit" beyond the physical bump.

**Needed**: detect meaningful player-vs-player contact (Unity collision/trigger callbacks on
`PlayerCar`, evaluated on the state-authority side so the result is authoritative, not guessed
locally by an observer), and decide what a hit actually *does* in this game — feeds directly
into tasks 3 and 4 below.

**Open question — needs a decision before building**: what does a hit mean for Capture the
Flag specifically? A tag/elimination? A knockback-only bump with no consequence? A trigger for
stealing a carried flag? This is a rules decision, not an implementation detail.

### 2. Game Manager — match lifecycle & win condition

**Now**: nothing owns "the match" once it starts. `MatchStarter.StartMatch()` flips
`PlayerMatchState.CanMove` and that's it — no match timer, no win-condition check, no way for a
match to actually *end*, no path back to the lobby afterward.

**Needed**: a networked match-state object, following the same pattern already established by
`MatchmakingSessionState` (spawned once via `IsSharedModeMasterClient`, self-authoritative) —
tracking match phase/timer, checking win conditions every tick, and handling match end (results
display, return-to-lobby flow — note there currently *is no* return-to-lobby path once
`MatchStarting` fires; that's new scope, not a gap in what exists today).

**Open question**: what ends a match — a time limit, a score threshold, last-player-standing?
Depends on the scoring and lives design below, so this task is naturally sequenced after (or
alongside) tasks 3 and 4, not fully before them.

### 3. Flag capture & scoring

**Now**: no flag object exists. No capture zones, no score state, nothing.

**Needed**: a flag `GameObject` (or one per team) with pickup/carry/return/capture logic —
carried-flag ownership needs to be authoritative and replicate correctly (same State Authority
model already used for `PlayerCar`), per-player or per-team score state, and a HUD element to
show it.

**Open question — this changes the model significantly, decide first**: solo free-for-all
(everyone for themselves) or team-based (2v2, matching `MatchmakingConfig.MaxPlayers = 4`)?

### 4. Player elimination — die & lives

**Now**: `PlayerCar` never dies. `CanMove` only ever transitions false → true, once, and never
back.

**Needed**: define what causes death (tagged while carrying the flag? knocked out of the arena?
some hit-point/contact threshold from task 1?), a lives model (limited lives → permanent
elimination for the match, vs. a respawn-after-cooldown loop), and how death interacts with the
existing `PlayerMatchState`/`CanMove` gate — likely a new `[Networked] IsAlive` (or
`LivesRemaining`), plus respawn positioning (the `PlayerId`-based spawn-offset scheme already
used in `PlayerLobbySpawner` generalizes directly to this).

## Also worth tracking (not one of the 4 above — sequence after core mechanics)

**Art & polish pass** — replace the single placeholder "Bolt" robot model (currently identical
for every player — this is the same gap already noted in `Networking_Progress.md`'s "no visual
distinction between your own car and another player's") with real per-player visuals, and add
visual effects: boost trail, drift smoke, impact/collision feedback, flag pickup/capture,
elimination. Deliberately sequenced *after* tasks 1-4 — polishing collision/scoring/death
visuals before those systems' actual behavior is locked risks redoing the art once the rules
settle.

## Already-known gaps this roadmap doesn't duplicate

See `Networking_Progress.md`'s "Known gaps" section for networking-specific items (bot AI,
`Game.unity` Build Settings step) not repeated here.
