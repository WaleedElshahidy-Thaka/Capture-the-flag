using System;
using UnityEngine;

// Owns the Idle -> Searching -> (WaitingSolo | WaitingReady) -> Starting state machine. Talks
// only to IQuickMatchService / IActiveQuickMatchSession - never references Fusion types
// directly, so it drives identically whether MatchmakingServices is backed by
// QuickMatchLocalService or QuickMatchFusionService. MatchmakingScreen binds to PhaseChanged
// and reads Session for display; it has no logic of its own.
public class MatchmakingFlowController : MonoBehaviour
{
    public MatchmakingPhase Phase { get; private set; } = MatchmakingPhase.Idle;
    public IActiveQuickMatchSession Session { get; private set; }
    public event Action<MatchmakingPhase> PhaseChanged;

    public void FindMatch()
    {
        if (Phase != MatchmakingPhase.Idle) return;

        SetPhase(MatchmakingPhase.Searching);
        StartCoroutine(MatchmakingServices.QuickMatch.StartQuickMatch(MatchmakingConfig.GameId, OnStartResult));
    }

    void OnStartResult(StartQuickMatchResult result)
    {
        if (!result.Success)
        {
            Debug.LogError($"[Matchmaking] {result.Error}");
            SetPhase(MatchmakingPhase.Idle);
            return;
        }

        Session = result.Session;
    }

    public void RequestStartWithBots() => Session?.RequestStartWithBots();
    public void ToggleReady() => Session?.SetReady(!Session.IsReady);

    public void Cancel()
    {
        if (Session != null)
        {
            Session.Leave();
            Session = null;
        }
        else
        {
            MatchmakingServices.QuickMatch.CancelQuickMatch();
        }

        SetPhase(MatchmakingPhase.Idle);
    }

    void Update()
    {
        if (Session == null || Phase == MatchmakingPhase.Idle || Phase == MatchmakingPhase.Starting) return;

        if (Session.MatchStarting)
        {
            MatchStarter.StartMatch(Session.PlayerCount, Session.BotCount);
            SetPhase(MatchmakingPhase.Starting);
            return;
        }

        switch (Phase)
        {
            case MatchmakingPhase.Searching:
                if (Session.BotOptionUnlocked)
                    SetPhase(Session.PlayerCount == 1 ? MatchmakingPhase.WaitingSolo : MatchmakingPhase.WaitingReady);
                break;

            // The session stays open to new joins throughout the Waiting state, so a solo
            // player can still be joined by someone else after their own timer expired, and
            // a room that drops back to one player (someone left) falls back to the solo path.
            case MatchmakingPhase.WaitingSolo:
                if (Session.PlayerCount > 1) SetPhase(MatchmakingPhase.WaitingReady);
                break;
            case MatchmakingPhase.WaitingReady:
                if (Session.PlayerCount <= 1) SetPhase(MatchmakingPhase.WaitingSolo);
                break;
        }
    }

    void SetPhase(MatchmakingPhase next)
    {
        Phase = next;
        PhaseChanged?.Invoke(next);
    }
}
