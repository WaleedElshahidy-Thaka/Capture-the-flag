using System;
using System.Collections;
using UnityEngine;

// No-network fake: PlayerCount is fixed at 1, no other player can ever appear. Exercises the
// solo path (Searching -> 30s elapsed -> "start with computer players") end to end without
// Fusion or a real network connection involved. Swap for QuickMatchFusionService in
// MatchmakingServices to go live. Not meant to exercise the multi-player seat layout or the
// ready-toggle path - see the plan's Phase A / Phase B verification split.
public class QuickMatchLocalService : IQuickMatchService
{
    public IEnumerator StartQuickMatch(string gameId, Action<StartQuickMatchResult> onResult)
    {
        yield return null;
        onResult?.Invoke(StartQuickMatchResult.Ok(new LocalActiveQuickMatchSession()));
    }

    public void CancelQuickMatch()
    {
        // Nothing to tear down - no runner, no connection.
    }

    class LocalActiveQuickMatchSession : IActiveQuickMatchSession
    {
        readonly float startRealtime = Time.realtimeSinceStartup;
        bool matchStarting;
        int botCount;

        public int PlayerCount => 1;
        public int MaxPlayers => MatchmakingConfig.MaxPlayers;
        public bool IsHost => true;

        public float ElapsedSeconds => Mathf.Min(Time.realtimeSinceStartup - startRealtime, MatchmakingConfig.SearchDurationSeconds);
        public bool BotOptionUnlocked => ElapsedSeconds >= MatchmakingConfig.BotOptionUnlockSeconds;

        public bool IsReady { get; private set; }
        public void SetReady(bool ready)
        {
            IsReady = ready;
            Changed?.Invoke();
        }

        public int SearchingCount => IsSearching ? 1 : 0;
        public bool IsSearching { get; private set; }
        public void SetSearching(bool searching)
        {
            IsSearching = searching;
            Changed?.Invoke();
        }

        public void RequestStartWithBots()
        {
            if (matchStarting) return;
            botCount = MaxPlayers - PlayerCount;
            matchStarting = true;
            Changed?.Invoke();
        }

        public bool MatchStarting => matchStarting;
        public int BotCount => botCount;

        public event Action Changed;

        public void Leave()
        {
            Changed?.Invoke();
        }
    }
}
