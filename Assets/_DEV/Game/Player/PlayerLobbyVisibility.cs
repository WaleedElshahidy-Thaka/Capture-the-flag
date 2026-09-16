using Fusion;
using UnityEngine;

// Who you can see, and when. Your own car is always visible. Another player's car appears
// only once you are both searching - that's the moment "a player was found" - or once the
// match is starting. Anyone just sitting in the arena, not searching, stays hidden.
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
        var session = MatchmakingSessionState.Local;
        var own = PlayerMatchState.Local;

        bool shouldShow = networkObject.HasInputAuthority
                          || (session != null && session.MatchStarting)
                          || (own != null && own.IsSearching && state.IsSearching);

        if (shouldShow == visible) return;
        visible = shouldShow;

        for (int i = 0; i < hiddenUntilMatched.Length; i++)
            if (hiddenUntilMatched[i] != null) hiddenUntilMatched[i].SetActive(shouldShow);
    }
}
