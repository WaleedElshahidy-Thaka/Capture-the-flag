using System;
using UnityEngine;

// Owns the Loading -> Idle -> Searching -> (WaitingSolo | WaitingReady) -> Starting state
// machine. Talks only to IQuickMatchService / IActiveQuickMatchSession - never references
// Fusion types directly, so it drives identically whether MatchmakingServices is backed by
// QuickMatchLocalService or QuickMatchFusionService. MatchmakingScreen binds to PhaseChanged
// and reads Session for display; it has no logic of its own.
//
// The flow, as specified:
//   open the game  -> Loading (black) until connected and your own car is seated
//   Idle           -> arena, your robot, Quick Match button; nothing happens until pressed
//   Searching      -> timer from 0; a second searcher appears the moment there are two,
//                     and the timer becomes the shared (highest) one
//   WaitingSolo    -> 30s alone: "start with computer players" starts immediately
//   WaitingReady   -> 30s shared, 2+: ready toggle; everyone ready -> start with bots,
//                     otherwise keep searching (more players can still join)
public class MatchmakingFlowController : MonoBehaviour
{
    public MatchmakingPhase Phase { get; private set; } = MatchmakingPhase.Loading;
    public IActiveQuickMatchSession Session { get; private set; }
    public event Action<MatchmakingPhase> PhaseChanged;

    void Start()
    {
        StartCoroutine(MatchmakingServices.QuickMatch.StartQuickMatch(MatchmakingConfig.GameId, OnConnected));
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
        if (Session == null || Phase == MatchmakingPhase.Idle || Phase == MatchmakingPhase.Starting) return;

        Session.SetSearching(false);
        SetPhase(MatchmakingPhase.Idle);
    }

    void Update()
    {
        if (Session == null) return;

        if (Phase == MatchmakingPhase.Loading)
        {
            if (Session.IsLocalPlayerSpawned) SetPhase(MatchmakingPhase.Idle);
            return;
        }

        if (Phase == MatchmakingPhase.Idle || Phase == MatchmakingPhase.Starting) return;

        if (Session.MatchStarting)
        {
            MatchStarter.StartMatch(Session.SearchingCount, Session.BotCount);
            SetPhase(MatchmakingPhase.Starting);
            return;
        }

        bool unlocked = Session.BotOptionUnlocked;
        bool alone = Session.SearchingCount <= 1;

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

    void SetPhase(MatchmakingPhase next)
    {
        Phase = next;
        PhaseChanged?.Invoke(next);
    }
}
