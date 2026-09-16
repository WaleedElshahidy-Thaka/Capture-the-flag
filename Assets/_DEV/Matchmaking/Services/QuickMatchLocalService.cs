using System;
using System.Collections;
using UnityEngine;

// No-network fake: PlayerCount is fixed at 1, no other player can ever appear. Exercises the
// solo path (Quick Match -> Searching -> 30s -> "start with computer players") end to end
// without Fusion or a real network connection involved. Swap for QuickMatchFusionService in
// MatchmakingServices to go live. Not meant to exercise the 2+ player ready path.
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

    // No host to lose. Explicit no-op accessors rather than unused-event warnings.
    public event Action HostLost { add { } remove { } }
    public event Action<IActiveQuickMatchSession> SessionRestored { add { } remove { } }
    public event Action<string> SessionFailed { add { } remove { } }

    class LocalActiveQuickMatchSession : IActiveQuickMatchSession
    {
        float searchStartRealtime;
        bool matchStarting;
        int botCount;

        public int PlayerCount => 1;
        public int MaxPlayers => MatchmakingConfig.MaxPlayers;
        public bool IsHost => true;
        public bool IsLocalPlayerSpawned => true;

        public bool IsSearching { get; private set; }
        public float LobbyJoinSecondsRemaining => 0f; // nobody to be found
        public bool InLobby => false;
        public int LobbyCount => 0;
        public void SetSearching(bool searching)
        {
            if (searching && !IsSearching) searchStartRealtime = Time.realtimeSinceStartup;
            IsSearching = searching;
            if (!searching) IsReady = false;
            Changed?.Invoke();
        }

        public float ElapsedSeconds => IsSearching
            ? Mathf.Min(Time.realtimeSinceStartup - searchStartRealtime, MatchmakingConfig.SearchDurationSeconds)
            : 0f;
        public bool BotOptionUnlocked => IsSearching && ElapsedSeconds >= MatchmakingConfig.BotOptionUnlockSeconds;

        public bool IsFrozen => false;
        public float ResumeSecondsRemaining => 0f;

        public bool IsReady { get; private set; }
        public void SetReady(bool ready)
        {
            IsReady = ready;
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
