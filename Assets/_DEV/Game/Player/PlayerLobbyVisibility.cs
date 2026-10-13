using Fusion;
using UnityEngine;

// Shows a car only once both you and it are in the lobby, or once the match is starting.
// Until then LobbyPreviewCar stands in for your own. Only cosmetic children are toggled;
// the networked object keeps simulating underneath.
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(PlayerMatchState))]
public class PlayerLobbyVisibility : MonoBehaviour
{
    [SerializeField] GameObject[] hiddenUntilMatched;

    PlayerMatchState state;
    bool visible = true;

    void Awake()
    {
        state = GetComponent<PlayerMatchState>();
    }

    void LateUpdate()
    {
        if (!state.HasState) return;

        bool shouldShow = ShouldShow();
        if (shouldShow == visible) return;
        visible = shouldShow;

        for (int i = 0; i < hiddenUntilMatched.Length; i++)
            if (hiddenUntilMatched[i] != null) hiddenUntilMatched[i].SetActive(shouldShow);
    }

    bool ShouldShow()
    {
        var session = MatchmakingSessionState.Local;
        if (session != null && session.MatchStarting) return true;

        var own = PlayerRoster.Local;
        return own != null && own.InLobby && state.InLobby;
    }
}
