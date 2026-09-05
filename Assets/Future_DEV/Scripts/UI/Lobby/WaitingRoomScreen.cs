using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The "simple UI with texts" stopping point for this pass: shows the room's state and lets
// the host reveal a private room, but doesn't start a match yet.
public class WaitingRoomScreen : MonoBehaviour
{
    [SerializeField] ScreenNavigator navigator;
    [SerializeField] GameObject entryScreen;

    [SerializeField] TMP_Text roomNameText;
    [SerializeField] TMP_Text playerCountText;
    [SerializeField] GameObject roomCodePanel;
    [SerializeField] TMP_Text roomCodeText;
    [SerializeField] Button makeVisibleButton;
    [SerializeField] Button startButton;
    [SerializeField] Button leaveButton;

    IActiveRoomSession room;

    void Awake()
    {
        makeVisibleButton.onClick.AddListener(OnMakeVisibleClicked);
        leaveButton.onClick.AddListener(OnLeaveClicked);
    }

    void OnDisable()
    {
        if (room != null) room.Changed -= Refresh;
    }

    public void Bind(IActiveRoomSession session)
    {
        if (room != null) room.Changed -= Refresh;

        room = session;
        room.Changed += Refresh;
        Refresh();
    }

    void Refresh()
    {
        if (room == null) return;

        roomNameText.text = room.RoomName;
        playerCountText.text = $"Players: {room.PlayerCount}/{room.MaxPlayers}";

        bool showCode = !room.IsVisible;
        roomCodePanel.SetActive(showCode);
        if (showCode) roomCodeText.text = $"Room Code: {room.RoomCode}";
        makeVisibleButton.gameObject.SetActive(showCode && room.IsHost);

        // Host-only; "completed" is read literally as full, per the user's own room-creation spec.
        startButton.gameObject.SetActive(room.IsHost);
        startButton.interactable = room.IsHost && room.PlayerCount >= room.MaxPlayers;
    }

    void OnMakeVisibleClicked()
    {
        if (room != null) StartCoroutine(room.MakeVisible(_ => Refresh()));
    }

    void OnLeaveClicked()
    {
        room?.Leave();
        navigator.Show(entryScreen);
    }
}
