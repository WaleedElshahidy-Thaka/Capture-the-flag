# Multiplayer Lobby System — Progress Notes

Last updated: 2026-09-05

This document exists so work can continue in a fresh chat session (this machine or another
PC) without re-deriving everything from scratch. It covers what was decided, what was built,
what's still open, and how to pick the work back up.

## Origin / reference

This project implements a simplified, project-owner-specified version of the flow described
in the shared design doc **"Matchmaking & Lobby System v0.3."** Where the owner's own
instructions and the doc disagreed or the doc went further (Mode Select, Game Select,
matchmaking/Quick Match, skill-based matching, etc.), **the owner's instructions win** — this
project intentionally implements a subset. The doc's Photon Fusion technical sections (N1–N10)
were used to fill gaps the owner didn't specify (authority model, interest management, etc.).

## Scope actually being built here

This Unity project is **only the Capture the Flag game itself**. Mode Select (Singleplayer vs
Multiplayer) and Game Select (choosing among multiplayer games) belong to a **separate,
external "main/launcher" project** and are out of scope here — this project's own flow starts
directly at its own entry screen:

1. **Entry screen** — title "Capture the Flag" + **Create Room** / **Join Room**.
2. **Create Room** — room name, player count (4 / 6 / 8), Visible/Invisible choice, Create
   button. Validates the room name is unique before proceeding.
   - Visible → lands in the room as host; Start is present but disabled until the room is full.
   - Invisible → lands in the room as host; shows **Room Code: XXX** + a **Make it visible**
     button that disappears once clicked and makes the room findable.
3. **Join Room** → two sub-paths:
   - **Join by code** — text field + Join button; wrong code shows an inline error.
   - **Search for available seat** — list of visible rooms (name + current/max players);
     click to join.
4. Deliberately stops there — no gameplay, no match-start handoff yet. This was scoped as a
   "simple UI with texts so we can test the room/lobby system" pass.

Two eventual integration points are explicitly **not** built yet:
- Wiring this project's entry screen to be launched *from* the separate launcher project
  (this project already carries the launcher hand-off scaffold: `AppHandoff`,
  `ProcessLauncher`, `HandoffRunner`, `LaunchArgs`, `DataTransferHandler` — pre-existing,
  untouched).
- What happens after a match actually starts (gameplay, `NetworkObject`s, robot/flag logic).

## Architecture (SOLID)

Global namespace, no `namespace` blocks — matches this project's existing convention
(`Services.cs`, `PlayerHandler.cs`, etc. are all global-namespace).

```
Assets/_DEV/Scripts/
  Models/RoomModels.cs            RoomVisibility, CreateRoomRequest, RoomSummary,
                                   CreateRoomResult/JoinRoomResult/RoomListResult/
                                   MakeVisibleResult (Ok/Fail factories, matches this
                                   project's existing GameModels.cs convention)
  Services/
    IRoomCreator.cs                IEnumerator CreateRoom(request, onResult)
    IRoomJoiner.cs                 IEnumerator JoinByCode(code, onResult)
    IRoomBrowser.cs                IEnumerator GetAvailableRooms(onResult)
    IActiveRoomSession.cs          "the room I'm in": RoomName/RoomCode/IsVisible/
                                    PlayerCount/MaxPlayers/IsHost, event Changed,
                                    MakeVisible(...), Leave()
    IRoomServiceBackend.cs         aggregate of the three service interfaces
    LocalRoomService.cs            in-memory backend, no networking, true name-uniqueness.
                                    "Joining" your own created room simulates a 2nd seat
                                    filling — good for exercising UI states, not real
                                    concurrent multiplayer.
    FusionRoomService.cs           real Photon Fusion 2 backend (see below)
    Fusion/
      RunnerCallbackRelay.cs       one reusable INetworkRunnerCallbacks impl; every method
                                   is a no-op except the ones wired via Action fields
                                   (OnSessionListUpdated/OnPlayerJoined/OnPlayerLeft/
                                   OnShutdown/OnConnectFailed)
      FusionActiveRoomSession.cs   IActiveRoomSession backed by a live NetworkRunner
  Core/
    RoomCodeGenerator.cs           5-char code, excludes 0/O/1/I/l, per the reference doc
    Services.cs                   EXISTING composition root, EDITED — added RoomCreator/
                                   RoomJoiner/RoomBrowser fields alongside the existing
                                   Game field. Currently wired to `new LocalRoomService()`.
                                   Swap to `new FusionRoomService()` to go live.
  UI/Lobby/
    ScreenNavigator.cs             minimal show/hide-by-GameObject-reference, no animation
    TimedErrorBanner.cs            shared error panel + auto-hide, one instance reused by
                                   every screen that can fail
    CtfEntryScreen.cs / CreateRoomScreen.cs / WaitingRoomScreen.cs / JoinRoomScreen.cs /
    JoinByCodeScreen.cs / BrowseRoomsScreen.cs / RoomListCard.cs
  Editor/
    LobbyUISceneSetup.cs           [MenuItem("Multiplayer/Setup Lobby Scene")] — idempotent
                                   procedural scene builder. Builds the whole Canvas +
                                   6 screens into Assets/_DEV/CaptureTheFlag.unity and wires
                                   every [SerializeField] via SerializedObject, since there's
                                   no way to drive the Unity Editor GUI directly from a
                                   coding-agent session. Safe to re-run.
```

Every screen depends only on the narrow interface(s) it actually uses (Interface
Segregation); `LocalRoomService` and `FusionRoomService` are drop-in substitutes for each
other (Liskov) selected in exactly one place (`Services.cs`, Dependency Inversion). A second
future multiplayer game can reuse `JoinRoomScreen`/`BrowseRoomsScreen`/`TimedErrorBanner`/
`ScreenNavigator`/the whole Services layer as-is.

## Photon Fusion status

- **SDK**: Fusion 2.1.2 (Stable-2279) is imported (`Assets/Photon/Fusion`).
- **App ID**: configured by the project owner via Fusion Hub. `PhotonAppSettings.asset`
  (`Assets/Photon/Fusion/Resources/`) has a real `AppIdFusion` value and auto-select region —
  confirmed already correct, not modified further.
- **`NetworkProjectConfig.fusion`** (`Assets/Photon/Fusion/Resources/`): changed
  `Simulation.ReplicationFeatures` from `0` (None) to `1` (InterestManagement) — matches the
  reference doc's N3 "enable it with a generous radius." The existing AOI cell size (32) ×
  grid (1024³) was already generous, left unchanged.
- **Deliberately NOT touched, and why:**
  - **Tick rate (N2)** — `Simulation.TickRateSelection` is still all-zero/default. The doc
    doesn't itself mandate 60 vs 30 Hz ("ruling should be made before drift tuning begins," not
    a fixed answer), and the on-disk field encoding (`ClientSendInterval`/`ServerTickInterval`/
    etc., with an "obsolete index format" also present) couldn't be fully verified without
    running Unity. Rather than risk corrupting a connection that now works, this is left for
    the owner to set via the Fusion Inspector (or to explicitly say "set it to 60/30" so it can
    be done with confidence).
  - **N5 (rollback depth, interpolation delay, correction blend window, position/rotation
    thresholds)** — these are per-`NetworkTransform`/`NetworkRigidbody` component settings.
    There is **no networked player/robot object in the project yet** (out of scope for this
    pass), so there's nothing to attach them to until gameplay is built.
- **`FusionRoomService.cs`** was written against the **actual installed DLLs**, not guessed:
  types/methods/enum members were cross-checked directly against
  `Assets/Photon/Fusion/Assemblies/Fusion.Runtime.dll` (and `.Realtime`/`.Sockets`) and their
  shipped XML doc comments, plus two of Photon's own sample scripts extracted from the
  still-zipped demo `.unitypackage`s for real usage patterns (`StartGameArgs`, `GameMode`,
  `PhotonAppSettings.Global.AppSettings.AppIdFusion`, etc.).

### Compile errors hit and fixed so far
Found via `C:\Users\<user>\AppData\Local\Unity\Editor\Editor.log` (fastest way to check —
grep it for `error CS` rather than guessing):
1. `RunnerCallbackRelay.cs` — `NetAddress`/`NetConnectFailedReason`/`NetDisconnectReason`/
   `ReliableKey` actually live in `Fusion.Sockets`, not `Fusion` (missing `using`).
   `OnReliableDataReceived`'s last parameter is `ReadOnlySpan<byte>`, not `ArraySegment<byte>`.
2. `FusionRoomService.cs` — `SessionProperty` has no `IsNull` member (real members are
   `PropertyValue`, `PropertyType`, `IsInt`, `IsString`, `Isbool`); fixed both spots to check
   `prop.PropertyValue != null` instead.

**As of the last message from the project owner, these fixes had just been applied and not
yet re-verified in the Editor.** First thing to check when resuming: has Unity recompiled
clean (check the menu item `Multiplayer/Setup Lobby Scene` is visible, and/or re-grep
`Editor.log` for any remaining `error CS` after the timestamp of these fixes)?

## Known, deliberate limitation (flagged, not a bug)

Room **name** uniqueness (separate from the auto-generated room *code*) can only be checked
against **visible/public** rooms once the Fusion backend is active — private/invisible Fusion
sessions are never queryable by any client, by design (matches the doc's Design Goal 2). True
global uniqueness would need a custom name-registry backend, which the reference doc
explicitly avoids building. `LocalRoomService` (current default) enforces true uniqueness
since everything lives in one process — this gap only appears once `FusionRoomService` is live.

## How to resume / test

1. Open the project in Unity (6000.3.6f1), let it finish compiling.
2. If compiling clean: run **`Multiplayer/Setup Lobby Scene`** (idempotent — safe to re-run
   any time after script changes) to (re)build/wire the UI into
   `Assets/_DEV/CaptureTheFlag.unity`.
3. Press Play. Backend is `LocalRoomService` (see `Services.cs`) — the full UI flow (create,
   name-collision error, visible/invisible + "make it visible", join by code + wrong-code
   error, browse list) should be clickable end-to-end already.
4. To test real Photon networking: flip `Services.cs`'s `room` field to
   `new FusionRoomService()`, build/run two clients (or one Editor + one build), and verify
   create/join/browse over the actual Photon Cloud connection.
5. Still open: confirm tick rate (N2) and set it deliberately; once a networked player/robot
   prefab exists, revisit N5's per-object rollback/interpolation/correction settings.
