using Fusion;
using UnityEngine;

// Hides a real car until the match starts. Real cars spawn straight onto their match start spot
// (StartAnchors) and wait there, invisible, while the lobby shows the six stand-in robots
// (LobbySeatPreview). When the match starts they all appear at once, bots included.
// Only cosmetic children are toggled; the networked object keeps simulating underneath.
//
// Show In Lobby (old behaviour): also show a car once both you and it are in the lobby.
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(PlayerMatchState))]
public class PlayerLobbyVisibility : MonoBehaviour
{
    [SerializeField] GameObject[] hiddenUntilMatched;
    [Tooltip("Old behaviour: show real cars in the lobby once both players are in it. Off = real cars only " +
             "appear when the match starts (the lobby shows the stand-in robots instead).")]
    [SerializeField] bool showInLobby = false;

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

        if (!showInLobby) return false;
        var own = PlayerRoster.Local;
        return own != null && own.InLobby && state.InLobby;
    }
}
