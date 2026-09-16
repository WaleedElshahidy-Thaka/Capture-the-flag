using Fusion;
using UnityEngine;

// Who you can see, and when. A car appears only once you are both in the lobby - after the
// "Player found! Joining lobby in 3" countdown both of you counted from the same host-stamped
// tick - or once the match is starting. That includes your own: until the lobby, the local
// LobbyPreviewCar stands in for it, so connecting never visibly swaps one robot for another.
// Anyone still counting down stays hidden.
//
// Only the cosmetic children are toggled (Visual, NameTag) - the NetworkObject, colliders and
// simulation keep running underneath, so nothing about replication changes. Toggles only on a
// state change; no per-frame SetActive churn.
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(PlayerMatchState))]
public class PlayerLobbyVisibility : MonoBehaviour
{
    [SerializeField] GameObject[] hiddenUntilMatched;

    NetworkObject networkObject;
    PlayerMatchState state;
    bool visible = true;

    void Awake()
    {
        networkObject = GetComponent<NetworkObject>();
        state = GetComponent<PlayerMatchState>();
    }

    void LateUpdate()
    {
        if (!state.HasState) return;

        var session = MatchmakingSessionState.Local;
        var own = PlayerMatchState.Local;

        bool shouldShow = (session != null && session.MatchStarting)
                          || (own != null && own.InLobby && state.InLobby);

        if (shouldShow == visible) return;
        visible = shouldShow;

        for (int i = 0; i < hiddenUntilMatched.Length; i++)
            if (hiddenUntilMatched[i] != null) hiddenUntilMatched[i].SetActive(shouldShow);
    }
}
