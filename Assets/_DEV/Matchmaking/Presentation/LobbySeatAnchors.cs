using UnityEngine;

// Scene-placed positions for the waiting-room placeholder layout: one "center" anchor (always
// this client's own placeholder) and MatchmakingConfig.MaxPlayers - 1 "other" anchors for
// everyone else, arranged however suits a PUBG-Mobile-style matchmaking screen (e.g. a
// shallow arc or line around/behind center). Purely local/cosmetic - never networked.
public class LobbySeatAnchors : MonoBehaviour
{
    [SerializeField] Transform centerAnchor;
    [SerializeField] Transform[] otherAnchors; // sized MatchmakingConfig.MaxPlayers - 1 in the scene

    public Transform CenterAnchor => centerAnchor;
    public Transform[] OtherAnchors => otherAnchors;
}
