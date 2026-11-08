using System;
using System.Collections;
using UnityEngine;

// Offline fake for UI work: always exactly one player, so it exercises the solo path
// (READY -> 120 s countdown -> start with bots) without any network.
// Select it in MatchmakingServices.
public class QuickMatchLocalService : IQuickMatchService
{
    public IEnumerator StartQuickMatch(string gameId, Action<StartQuickMatchResult> onResult)
    {
        yield return null;
        onResult?.Invoke(StartQuickMatchResult.Ok(new LocalActiveQuickMatchSession()));
    }

    public void CancelQuickMatch() { }

    // There is no host to lose.
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
        public float LobbyJoinSecondsRemaining => 0f;
        public bool InLobby => false;
        public int LobbyCount => 0;

        public void SetSearching(bool searching, float alreadyElapsedSeconds = 0f)
        {
            if (searching && !IsSearching) searchStartRealtime = Time.realtimeSinceStartup - alreadyElapsedSeconds;
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

        // Same rule as the host: alone, the match starts when the countdown reaches 0.
        public bool MatchStarting
        {
            get
            {
                if (!matchStarting && IsSearching && MatchStartRule.ShouldStart(ElapsedSeconds, 1))
                {
                    botCount = MaxPlayers - PlayerCount;
                    matchStarting = true;
                }
                return matchStarting;
            }
        }
        public int BotCount => botCount;

        public event Action Changed;

        public void Leave()
        {
            Changed?.Invoke();
        }
    }
}
