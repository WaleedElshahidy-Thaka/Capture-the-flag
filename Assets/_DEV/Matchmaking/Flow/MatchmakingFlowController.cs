using System;
using UnityEngine;

// Owns the Idle -> Searching -> (WaitingSolo | WaitingReady) -> Starting state machine. Talks
// only to IQuickMatchService / IActiveQuickMatchSession - never references Fusion types
// directly, so it drives identically whether MatchmakingServices is backed by
// QuickMatchLocalService or QuickMatchFusionService. MatchmakingScreen binds to PhaseChanged
// and reads Session for display; it has no logic of its own.
//
// Connecting to the shared hub happens automatically on scene start, not behind Find Match -
// every player who opens the game is immediately visible to every other connected player, with
// their real PlayerCar already spawned and positioned (see PlayerLobbySpawner), just not
// drivable yet. Find Match no longer triggers the connection itself; it just flags this player
// as actively searching, which is what actually counts toward a match starting (see
// PlayerMatchState.IsSearching / MatchmakingSessionState.FixedUpdateNetwork). Phase starts at
// Connecting (Find Match disabled, MatchmakingScreen shows a loading message) until the
// connection actually completes, then moves to Idle for as long as you're connected-but-not-
// searching.
public class MatchmakingFlowController : MonoBehaviour
{
    public MatchmakingPhase Phase { get; private set; } = MatchmakingPhase.Connecting;
    public IActiveQuickMatchSession Session { get; private set; }
    public event Action<MatchmakingPhase> PhaseChanged;

    void Start()
    {
        StartCoroutine(MatchmakingServices.QuickMatch.StartQuickMatch(MatchmakingConfig.GameId, OnStartResult));
    }

    void OnStartResult(StartQuickMatchResult result)
    {
        if (!result.Success)
        {
            Debug.LogError($"[Matchmaking] {result.Error}");
            return;
        }

        Session = result.Session;
        SetPhase(MatchmakingPhase.Idle);
    }

    public void FindMatch()
    {
        if (Session == null || Phase != MatchmakingPhase.Idle) return;

        Session.SetSearching(true);
        SetPhase(MatchmakingPhase.Searching);
    }

    public void RequestStartWithBots() => Session?.RequestStartWithBots();
    public void ToggleReady() => Session?.SetReady(!Session.IsReady);

    // No longer leaves the session - cancelling means "stop searching", not "disconnect from
    // the hub". You stay connected and visible to other players either way.
    public void Cancel()
    {
        Session?.SetSearching(false);
        SetPhase(MatchmakingPhase.Idle);
    }

    void Update()
    {
        if (Session == null || Phase == MatchmakingPhase.Idle || Phase == MatchmakingPhase.Starting) return;

        if (Session.MatchStarting)
        {
            MatchStarter.StartMatch(Session.SearchingCount, Session.BotCount);
            SetPhase(MatchmakingPhase.Starting);
            return;
        }

        switch (Phase)
        {
            case MatchmakingPhase.Searching:
                if (Session.BotOptionUnlocked)
                    SetPhase(Session.SearchingCount == 1 ? MatchmakingPhase.WaitingSolo : MatchmakingPhase.WaitingReady);
                break;

            // The session stays open to new joins throughout the Waiting state, so a solo
            // player can still be joined by another searcher after their own timer expired, and
            // a room that drops back to one searcher (someone left or stopped searching) falls
            // back to the solo path.
            case MatchmakingPhase.WaitingSolo:
                if (Session.SearchingCount > 1) SetPhase(MatchmakingPhase.WaitingReady);
                break;
            case MatchmakingPhase.WaitingReady:
                if (Session.SearchingCount <= 1) SetPhase(MatchmakingPhase.WaitingSolo);
                break;
        }
    }

    void SetPhase(MatchmakingPhase next)
    {
        Phase = next;
        PhaseChanged?.Invoke(next);
    }
}
