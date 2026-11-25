using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// One row in the "search for available seat" list: room name + current/max players.
// Setup(data, onClicked) mirrors the destroy-children/Instantiate-per-item card pattern
// used elsewhere in this game family (e.g. news cards) — self-contained, no manual
// button-listener plumbing from the parent.
public class RoomListCard : MonoBehaviour
{
    [SerializeField] TMP_Text roomNameText;
    [SerializeField] TMP_Text playerCountText;
    [SerializeField] Button joinButton;

    RoomSummary room;
    Action<RoomSummary> onJoinClicked;

    void Awake()
    {
        joinButton.onClick.AddListener(() => onJoinClicked?.Invoke(room));
    }

    public void Setup(RoomSummary summary, Action<RoomSummary> onJoin)
    {
        room = summary;
        onJoinClicked = onJoin;
        roomNameText.text = summary.RoomName;
        playerCountText.text = $"{summary.PlayerCount}/{summary.MaxPlayers}";
    }
}
