using System;
using UnityEngine;

// Owns the Loading -> Idle -> Searching -> Found -> (WaitingSolo | WaitingReady) -> Starting
// state machine, plus the Reconnecting -> Resuming detour a host migration takes from any of
// them. Talks only to IQuickMatchService / IActiveQuickMatchSession - never references
// Fusion types directly, so it drives identically whether MatchmakingServices is backed by
// QuickMatchLocalService or QuickMatchFusionService. MatchmakingScreen binds to PhaseChanged
// and reads Session for display; it has no logic of its own.
//
// The flow, as specified:
//   open the game  -> Loading (black) until connected and your own car is seated
//   Idle           -> arena, your robot, Quick Match button; nothing happens until pressed
//   Searching      -> own timer from 0
//   Found          -> a second searcher exists: "Player found! Joining lobby in 3..2..1" on
//                     both, then both see each other and the timer becomes the shared
//                     (highest) one; back to Searching, now in the lobby
//   WaitingSolo    -> 30s alone: "start with computer players" starts immediately
//   WaitingReady   -> 30s shared, 2+: ready toggle; everyone ready -> start with bots,
//                     otherwise keep searching (more players can still join)
//   Reconnecting   -> the host dropped; "Host disconnected - reconnecting..." until we're in
//                     the new session and our car is ours again
//   Resuming       -> mid-match only: cars held still, "Game resumes in N" once the new host
//                     has everyone back; then straight back to Starting
public class MatchmakingFlowController : MonoBehaviour
{
    public MatchmakingPhase Phase { get; private set; } = MatchmakingPhase.Loading;
    public IActiveQuickMatchSession Session { get; private set; }
    public event Action<MatchmakingPhase> PhaseChanged;

    void Start()
    {
        var service = MatchmakingServices.QuickMatch;
        service.HostLost += OnHostLost;
        service.SessionRestored += OnSessionRestored;
        service.SessionFailed += OnSessionFailed;
        Connect();
    }

    void OnDestroy()
    {
        var service = MatchmakingServices.QuickMatch;
        service.HostLost -= OnHostLost;
        service.SessionRestored -= OnSessionRestored;
        service.SessionFailed -= OnSessionFailed;
    }

    void Connect()
    {
        StartCoroutine(MatchmakingServices.QuickMatch.StartQuickMatch(MatchmakingConfig.GameId, OnConnected));
    }

    void OnHostLost()
    {
        Session = null;
        SetPhase(MatchmakingPhase.Reconnecting);
    }

    // Stays Reconnecting until our car has been handed back to us - Update moves on from there.
    void OnSessionRestored(IActiveQuickMatchSession session)
    {
        Session = session;
    }

    // Couldn't rejoin. Start over exactly as if the game had just been opened: a fresh session
    // with whoever is out there.
    void OnSessionFailed(string error)
    {
        Debug.LogWarning($"[Matchmaking] {error} - reconnecting to a new session.");
        Session = null;
        SetPhase(MatchmakingPhase.Loading);
        Connect();
    }

    void OnConnected(StartQuickMatchResult result)
    {
        if (!result.Success)
        {
            Debug.LogError($"[Matchmaking] {result.Error}");
            return;
        }

        Session = result.Session;
        // Stays in Loading until the car is actually seated - see Update.
    }

    public void FindMatch()
    {
        if (Session == null || Phase != MatchmakingPhase.Idle) return;

        Session.SetSearching(true);
        SetPhase(MatchmakingPhase.Searching);
    }

    public void RequestStartWithBots() => Session?.RequestStartWithBots();
    public void ToggleReady() => Session?.SetReady(!Session.IsReady);

    // Stop searching. You stay connected and in your seat.
    public void Cancel()
    {
        bool cancellable = Phase == MatchmakingPhase.Searching || Phase == MatchmakingPhase.Found
                           || Phase == MatchmakingPhase.WaitingSolo || Phase == MatchmakingPhase.WaitingReady;
        if (Session == null || !cancellable) return;

        Session.SetSearching(false);
        SetPhase(MatchmakingPhase.Idle);
    }

    void Update()
    {
        if (Session == null) return;

        if (Phase == MatchmakingPhase.Loading || Phase == MatchmakingPhase.Reconnecting)
        {
            if (Session.IsLocalPlayerSpawned) SetPhase(PhaseFromSessionState());
            return;
        }

        // Frozen after a hand-over: nothing else moves until the shared resume tick.
        if (Session.IsFrozen)
        {
            if (Phase != MatchmakingPhase.Resuming) SetPhase(MatchmakingPhase.Resuming);
            return;
        }
        if (Phase == MatchmakingPhase.Resuming) SetPhase(PhaseFromSessionState());

        if (Phase == MatchmakingPhase.Idle || Phase == MatchmakingPhase.Starting) return;

        if (Session.MatchStarting)
        {
            MatchStarter.StartMatch(Session.LobbyCount, Session.BotCount);
            SetPhase(MatchmakingPhase.Starting);
            return;
        }

        // The countdown overrides whatever else the timer would say; the moment it ends we
        // fall back to Searching and the normal rules pick the lobby state up from there.
        if (Session.LobbyJoinSecondsRemaining > 0f)
        {
            if (Phase != MatchmakingPhase.Found) SetPhase(MatchmakingPhase.Found);
            return;
        }
        if (Phase == MatchmakingPhase.Found) SetPhase(MatchmakingPhase.Searching);

        bool unlocked = Session.BotOptionUnlocked;
        bool alone = !Session.InLobby || Session.LobbyCount <= 1;

        switch (Phase)
        {
            case MatchmakingPhase.Searching:
                if (unlocked) SetPhase(alone ? MatchmakingPhase.WaitingSolo : MatchmakingPhase.WaitingReady);
                break;

            // The shared timer is the highest searcher's, so it can drop if that player leaves -
            // in which case the bot option goes away again until the timer catches back up.
            case MatchmakingPhase.WaitingSolo:
                if (!unlocked) SetPhase(MatchmakingPhase.Searching);
                else if (!alone) SetPhase(MatchmakingPhase.WaitingReady);
                break;

            case MatchmakingPhase.WaitingReady:
                if (!unlocked) SetPhase(MatchmakingPhase.Searching);
                else if (alone) SetPhase(MatchmakingPhase.WaitingSolo);
                break;
        }
    }

    // Where a freshly (re)joined peer belongs, read straight from replicated state - the same
    // answer whether this is the first connect or the far side of a host migration. Found and
    // the Waiting phases are derived from Searching by the normal rules on the next Update.
    MatchmakingPhase PhaseFromSessionState()
    {
        if (Session.MatchStarting) return MatchmakingPhase.Starting;
        return Session.IsSearching ? MatchmakingPhase.Searching : MatchmakingPhase.Idle;
    }

    void SetPhase(MatchmakingPhase next)
    {
        Phase = next;
        PhaseChanged?.Invoke(next);
    }
}
