using System;
using UnityEngine;

// The Quick Match state machine (see MatchmakingPhase for the states). Talks only to
// IQuickMatchService / IActiveQuickMatchSession, never to Fusion, so it runs the same against
// the local fake or the real network backend. MatchmakingScreen displays it.
//
// Connecting happens on the Quick Match click (sessions hold searchers only). The timer starts
// at the click on a local clock; once our car is seated the host back-dates the networked timer
// to match, so the number never jumps.
public class MatchmakingFlowController : MonoBehaviour
{
    public MatchmakingPhase Phase { get; private set; } = MatchmakingPhase.Idle;
    public IActiveQuickMatchSession Session { get; private set; }
    public event Action<MatchmakingPhase> PhaseChanged;

    // The timer the UI shows: local until the session confirms our search, then the session's.
    public float SearchElapsedSeconds =>
        Session != null && Session.IsSearching
            ? Session.ElapsedSeconds
            : Mathf.Min(Time.realtimeSinceStartup - searchStartRealtime, MatchmakingConfig.SearchDurationSeconds);

    // True while you should see only your own robot (before entering the lobby).
    // Read by PlayerCamera and LobbyPreviewCar so they always agree.
    public bool IsSoloView => Phase.IsPreMatch() && (Session == null || !Session.InLobby);

    float searchStartRealtime;
    float reconnectingSince;
    bool searchRequested;
    // Each connect gets a number so a late result from a cancelled attempt is ignored.
    int connectAttempt;
    // True once the match clock has run out. The session is over: host-loss / restore / failure
    // events are ignored so they can't cover the results screen, and Play Again starts fresh.
    bool matchFinished;

    IQuickMatchService Service => MatchmakingServices.QuickMatch;

    void Start()
    {
        Service.HostLost += OnHostLost;
        Service.SessionRestored += OnSessionRestored;
        Service.SessionFailed += OnSessionFailed;
        MatchTimerState.MatchFinished += OnMatchFinished;
        SetPhase(MatchmakingPhase.Idle);
    }

    void OnDestroy()
    {
        Service.HostLost -= OnHostLost;
        Service.SessionRestored -= OnSessionRestored;
        Service.SessionFailed -= OnSessionFailed;
        MatchTimerState.MatchFinished -= OnMatchFinished;
    }

    // ─── Commands (UI buttons) ──────────────────────────────────────────

    public void FindMatch()
    {
        if (Phase != MatchmakingPhase.Idle) return;

        searchStartRealtime = Time.realtimeSinceStartup;
        searchRequested = false;
        SetPhase(MatchmakingPhase.Searching);

        int attempt = ++connectAttempt;
        StartCoroutine(Service.StartQuickMatch(MatchmakingConfig.GameId, result => OnConnected(attempt, result)));
    }

    public void RequestStartWithBots() => Session?.RequestStartWithBots();
    public void ToggleReady() => Session?.SetReady(!Session.IsReady);

    // Leaving the search means leaving the session entirely.
    public void Cancel()
    {
        if (!Phase.IsCancellable()) return;

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

    // Results screen button: leave the finished session and search for a new match.
    public void PlayAgain()
    {
        if (!matchFinished) return;
        matchFinished = false;
        connectAttempt++; // ignore any late result from the old connection

        if (Session != null)
        {
            Session.Leave();
            Session = null;
        }

        searchRequested = false;
        SetPhase(MatchmakingPhase.Idle);
        FindMatch();
    }

    // ─── Service callbacks ──────────────────────────────────────────────

    void OnConnected(int attempt, StartQuickMatchResult result)
    {
        bool stale = attempt != connectAttempt || Phase != MatchmakingPhase.Searching;
        if (stale)
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

        Session = result.Session; // the searching request goes out from Update once our car is seated
    }

    void OnHostLost()
    {
        // Match is over (e.g. the host pressed Play Again): keep showing the results.
        if (matchFinished)
        {
            Session = null;
            return;
        }

        Session = null;
        searchRequested = false;
        reconnectingSince = Time.unscaledTime;
        SetPhase(MatchmakingPhase.Reconnecting);
    }

    // Stays Reconnecting until our car is ours again - see UpdateReconnecting.
    void OnSessionRestored(IActiveQuickMatchSession session)
    {
        if (matchFinished)
        {
            session?.Leave(); // nothing to resume after the final whistle
            return;
        }

        Session = session;
    }

    void OnSessionFailed(string error)
    {
        Debug.LogWarning($"[Matchmaking] {error}");
        Session = null;

        if (matchFinished) return; // results stay up; Play Again starts a fresh search

        searchRequested = false;
        SetPhase(MatchmakingPhase.Idle);
    }

    // The timer ran out: from here the session only exists to show results.
    void OnMatchFinished(MatchResult _) => matchFinished = true;

    // ─── Per-frame transitions ──────────────────────────────────────────

    void Update()
    {
        if (Session == null) return; // offline, or still connecting
        if (matchFinished) return;   // results screen owns the UI now

        if (Phase == MatchmakingPhase.Reconnecting)
        {
            UpdateReconnecting();
            return;
        }

        if (UpdateFrozen()) return;
        if (Phase == MatchmakingPhase.Idle || Phase == MatchmakingPhase.Starting) return;
        if (!EnsureSearchRequested()) return;

        if (Session.MatchStarting)
        {
            if (Session is FusionActiveQuickMatchSession fusionSession)
            {
                MatchStarter.StartMatch(
                    fusionSession.Runner,
                    Session.LobbyCount,
                    Session.BotCount);
            }
            SetPhase(MatchmakingPhase.Starting);
            return;
        }

        if (UpdateFoundCountdown()) return;
        UpdateWaitingPhase();
    }

    void UpdateReconnecting()
    {
        // The notice stays up a minimum time so a fast hand-over doesn't just flicker it.
        bool noticeShown = Time.unscaledTime - reconnectingSince >= MatchmakingConfig.ReconnectingNoticeSeconds;
        if (Session.IsLocalPlayerSpawned && noticeShown) SetPhase(PhaseFromSessionState());
    }

    // Returns true while the session is frozen after a host migration.
    bool UpdateFrozen()
    {
        if (Session.IsFrozen)
        {
            if (Phase != MatchmakingPhase.Resuming) SetPhase(MatchmakingPhase.Resuming);
            return true;
        }
        if (Phase == MatchmakingPhase.Resuming) SetPhase(PhaseFromSessionState());
        return false;
    }

    // Tells the host we're searching (once our car is seated). Returns true once the host has confirmed.
    bool EnsureSearchRequested()
    {
        if (Session.IsSearching) return true;

        if (Session.IsLocalPlayerSpawned && !searchRequested)
        {
            searchRequested = true;
            Session.SetSearching(true, Time.realtimeSinceStartup - searchStartRealtime);
        }
        return false;
    }

    // Returns true while the "Player found! Joining lobby in N" countdown runs.
    bool UpdateFoundCountdown()
    {
        if (Session.LobbyJoinSecondsRemaining > 0f)
        {
            if (Phase != MatchmakingPhase.Found) SetPhase(MatchmakingPhase.Found);
            return true;
        }
        if (Phase == MatchmakingPhase.Found) SetPhase(MatchmakingPhase.Searching);
        return false;
    }

    // Searching <-> WaitingSolo <-> WaitingReady, from the bot-option timer and lobby size.
    // The shared timer can drop if its owner leaves, which takes the bot option away again.
    void UpdateWaitingPhase()
    {
        bool unlocked = Session.BotOptionUnlocked;
        bool alone = !Session.InLobby || Session.LobbyCount <= 1;
        MatchmakingPhase waiting = alone ? MatchmakingPhase.WaitingSolo : MatchmakingPhase.WaitingReady;

        switch (Phase)
        {
            case MatchmakingPhase.Searching:
            case MatchmakingPhase.WaitingSolo:
            case MatchmakingPhase.WaitingReady:
                MatchmakingPhase next = unlocked ? waiting : MatchmakingPhase.Searching;
                if (next != Phase) SetPhase(next);
                break;
        }
    }

    // Where a rejoined peer belongs after a host migration.
    MatchmakingPhase PhaseFromSessionState()
    {
        if (Session.MatchStarting) return MatchmakingPhase.Starting;
        if (!Session.IsSearching) searchRequested = false; // Update will re-request it
        return MatchmakingPhase.Searching;
    }

    void SetPhase(MatchmakingPhase next)
    {
        Phase = next;
        PhaseChanged?.Invoke(next);
    }
}