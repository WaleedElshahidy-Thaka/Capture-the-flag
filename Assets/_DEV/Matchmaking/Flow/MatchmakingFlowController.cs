using System;
using UnityEngine;

// Owns the Idle -> Searching -> Found -> (WaitingSolo | WaitingReady) -> Starting state
// machine, plus the Reconnecting -> Resuming detour a host migration takes from any of them.
// Talks only to IQuickMatchService / IActiveQuickMatchSession - never references Fusion types
// directly, so it drives identically whether MatchmakingServices is backed by
// QuickMatchLocalService or QuickMatchFusionService. MatchmakingScreen binds to PhaseChanged
// and reads Session for display; it has no logic of its own.
//
// Connection happens on Quick Match, not on scene start. A session holds only players who are
// searching, so the host is always a searcher, a seventh searcher opens a new session, and
// someone sitting in the menu costs nobody a seat. (An earlier version connected on scene
// start; with sessions capped at six that filled sessions by arrival order, searching or not,
// so two people who wanted to play could sit in different sessions and never meet.)
//
// The timer starts at the click, not at the connection. While connecting it runs off a local
// clock; once the car is seated, the searching request carries the seconds already elapsed and
// the host back-dates the networked timer to match - so the number never restarts or jumps.
//
// The flow, as specified:
//   Idle           -> arena, a local preview of your robot, Quick Match; not connected
//   Searching      -> timer from 0 at the click; connecting behind it, then the real search
//   Found          -> a second searcher exists: "Player found! Joining lobby in 3..2..1" on
//                     both, then both see each other and the timer becomes the shared
//                     (highest) one; back to Searching, now in the lobby
//   WaitingSolo    -> 30s alone: "start with computer players" starts immediately
//   WaitingReady   -> 30s shared, 2+: ready toggle; everyone ready -> start with bots,
//                     otherwise keep searching (more players can still join)
//   Cancel         -> leaves the session entirely; back to Idle, offline
//   Reconnecting   -> the host dropped; "Host disconnected - reconnecting..." until we're in
//                     the new session and our car is ours again
//   Resuming       -> mid-match only: cars held still, "Game resumes in N" once the new host
//                     has everyone back; then straight back to Starting
public class MatchmakingFlowController : MonoBehaviour
{
    public MatchmakingPhase Phase { get; private set; } = MatchmakingPhase.Idle;
    public IActiveQuickMatchSession Session { get; private set; }
    public event Action<MatchmakingPhase> PhaseChanged;

    // The one timer the UI shows. Local until the session has confirmed our search, then the
    // session's (own, or shared once in the lobby) - continuous across the hand-off.
    public float SearchElapsedSeconds =>
        Session != null && Session.IsSearching
            ? Session.ElapsedSeconds
            : Mathf.Min(Time.realtimeSinceStartup - searchStartRealtime, MatchmakingConfig.SearchDurationSeconds);

    // Solo view = your robot alone (the local preview), the state of things until a match is
    // found. Read by PlayerCamera and LobbyPreviewCar so the two can't disagree.
    public bool IsSoloView
    {
        get
        {
            bool lobbySide = Phase == MatchmakingPhase.Idle || Phase == MatchmakingPhase.Searching
                             || Phase == MatchmakingPhase.Found || Phase == MatchmakingPhase.WaitingSolo
                             || Phase == MatchmakingPhase.WaitingReady;
            return lobbySide && (Session == null || !Session.InLobby);
        }
    }

    float searchStartRealtime;
    float reconnectingSince;
    bool searchRequested;
    // Cancel-then-Quick-Match inside the connect window leaves the first attempt's result
    // arriving after the second has started; each result names its attempt so a stale one is
    // dropped instead of being read as the current search failing.
    int connectAttempt;

    IQuickMatchService Service => MatchmakingServices.QuickMatch;

    void Start()
    {
        Service.HostLost += OnHostLost;
        Service.SessionRestored += OnSessionRestored;
        Service.SessionFailed += OnSessionFailed;
        SetPhase(MatchmakingPhase.Idle);
    }

    void OnDestroy()
    {
        Service.HostLost -= OnHostLost;
        Service.SessionRestored -= OnSessionRestored;
        Service.SessionFailed -= OnSessionFailed;
    }

    public void FindMatch()
    {
        if (Phase != MatchmakingPhase.Idle) return;

        searchStartRealtime = Time.realtimeSinceStartup;
        searchRequested = false;
        SetPhase(MatchmakingPhase.Searching);

        int attempt = ++connectAttempt;
        StartCoroutine(Service.StartQuickMatch(MatchmakingConfig.GameId, result => OnConnected(attempt, result)));
    }

    void OnConnected(int attempt, StartQuickMatchResult result)
    {
        // Cancelled while connecting, or superseded by a newer attempt: whatever came back is
        // not wanted. (Fusion itself logs the aborted connect as "DisconnectByClientLogic" -
        // that's the cancel, not a fault.)
        if (attempt != connectAttempt || Phase != MatchmakingPhase.Searching)
        {
            if (result.Success) result.Session.Leave();
            return;
        }

        if (!result.Success)
        {
            Debug.LogError($"[Matchmaking] {result.Error}");
            SetPhase(MatchmakingPhase.Idle);
            return;
        }

        Session = result.Session;
        // The searching request goes out once our car is seated - see Update.
    }

    public void RequestStartWithBots() => Session?.RequestStartWithBots();
    public void ToggleReady() => Session?.SetReady(!Session.IsReady);

    // Stop searching = leave the session. Sessions hold searchers only, so there is nothing to
    // stay connected for.
    public void Cancel()
    {
        bool cancellable = Phase == MatchmakingPhase.Searching || Phase == MatchmakingPhase.Found
                           || Phase == MatchmakingPhase.WaitingSolo || Phase == MatchmakingPhase.WaitingReady;
        if (!cancellable) return;

        if (Session != null)
        {
            Session.Leave();
            Session = null;
        }
        else
        {
            Service.CancelQuickMatch();
        }

        searchRequested = false;
        SetPhase(MatchmakingPhase.Idle);
    }

    void OnHostLost()
    {
        Session = null;
        searchRequested = false;
        reconnectingSince = Time.unscaledTime;
        SetPhase(MatchmakingPhase.Reconnecting);
    }

    // Stays Reconnecting until our car has been handed back to us - Update moves on from there.
    void OnSessionRestored(IActiveQuickMatchSession session)
    {
        Session = session;
    }

    // Couldn't rejoin. Back to the menu; the player presses Quick Match again when they like.
    void OnSessionFailed(string error)
    {
        Debug.LogWarning($"[Matchmaking] {error}");
        Session = null;
        searchRequested = false;
        SetPhase(MatchmakingPhase.Idle);
    }

    void Update()
    {
        // Offline (Idle) or still connecting (Searching on the local clock): nothing to drive.
        if (Session == null) return;

        if (Phase == MatchmakingPhase.Reconnecting)
        {
            bool noticeShown = Time.unscaledTime - reconnectingSince >= MatchmakingConfig.ReconnectingNoticeSeconds;
            if (Session.IsLocalPlayerSpawned && noticeShown) SetPhase(PhaseFromSessionState());
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

        // Connected but the host doesn't know we're searching yet: tell it, once our car is
        // seated, carrying the time already on the clock. Until it echoes back, hold here.
        if (!Session.IsSearching)
        {
            if (Session.IsLocalPlayerSpawned && !searchRequested)
            {
                searchRequested = true;
                Session.SetSearching(true, Time.realtimeSinceStartup - searchStartRealtime);
            }
            return;
        }

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

    // Where a rejoined peer belongs after a host migration, read from replicated state. Anyone
    // in a session is a searcher, so it's the match if one is on, else Searching - and if the
    // copied state somehow lost the searching flag, Update re-requests it with the local clock.
    MatchmakingPhase PhaseFromSessionState()
    {
        if (Session.MatchStarting) return MatchmakingPhase.Starting;
        if (!Session.IsSearching) searchRequested = false;
        return MatchmakingPhase.Searching;
    }

    void SetPhase(MatchmakingPhase next)
    {
        Phase = next;
        PhaseChanged?.Invoke(next);
    }
}
