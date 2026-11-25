using UnityEngine;

// Game-Arena's link to the 01Skills backend and lobby. Does not touch the match, scoring or UI:
//   - when the match finishes, reports the local player's Score to POST /games/end (the response's
//     new progress goes into PlayerDataStore, which refreshes the level everywhere),
//   - ReturnToLobby() for buttons (the matchmaking Back button's onBack event).
// Put it on one scene object.
public class LobbyBridge : MonoBehaviour
{
    [SerializeField] bool reportScoreOnMatchEnd = true;

    void OnEnable() => MatchTimerState.MatchFinished += OnMatchFinished;
    void OnDisable() => MatchTimerState.MatchFinished -= OnMatchFinished;

    // Raised once per peer per match.
    void OnMatchFinished(MatchResult result)
    {
        if (!reportScoreOnMatchEnd) return;
        if (!AuthTokenStore.Instance.HasToken)
        {
            Debug.Log("[LobbyBridge] No token - match result not reported.");
            return;
        }

        var local = PlayerRoster.Local;
        int score = local != null && local.HasState ? local.Score : 0;
        StartCoroutine(Services.Game.EndGame(AuthTokenStore.Instance.Token, score, r =>
            Debug.Log($"[LobbyBridge] /games/end score={score} -> {(r != null && r.Success ? "ok" : "failed")}")));
    }

    public void ReturnToLobby()
    {
        Time.timeScale = 1f;
        if (LobbyReturn.TrySwitch()) return;
        AppHandoff.Quit();   // no lobby to return to: leave, as the button did before
    }
}
