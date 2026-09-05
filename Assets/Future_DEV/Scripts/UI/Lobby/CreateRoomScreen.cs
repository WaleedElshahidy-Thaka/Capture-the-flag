using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CreateRoomScreen : MonoBehaviour
{
    [SerializeField] ScreenNavigator navigator;
    [SerializeField] GameObject entryScreen;
    [SerializeField] GameObject waitingRoomScreen;
    [SerializeField] WaitingRoomScreen waitingRoom;
    [SerializeField] TimedErrorBanner errorBanner;

    [SerializeField] TMP_InputField roomNameField;
    [SerializeField] Toggle players4Toggle;
    [SerializeField] Toggle players6Toggle;
    [SerializeField] Toggle players8Toggle;
    [SerializeField] Toggle visibleToggle;
    [SerializeField] Toggle invisibleToggle;
    [SerializeField] Button createButton;
    [SerializeField] Button backButton;

    void Awake()
    {
        createButton.onClick.AddListener(OnCreateClicked);
        backButton.onClick.AddListener(() => navigator.Show(entryScreen));
    }

    void OnEnable()
    {
        if (roomNameField != null) roomNameField.text = string.Empty;
        if (players4Toggle != null) players4Toggle.isOn = true;
        if (visibleToggle != null) visibleToggle.isOn = true;
    }

    int SelectedMaxPlayers()
    {
        if (players6Toggle != null && players6Toggle.isOn) return 6;
        if (players8Toggle != null && players8Toggle.isOn) return 8;
        return 4;
    }

    RoomVisibility SelectedVisibility()
    {
        return invisibleToggle != null && invisibleToggle.isOn ? RoomVisibility.Private : RoomVisibility.Public;
    }

    void OnCreateClicked()
    {
        string roomName = roomNameField != null ? roomNameField.text : string.Empty;
        var request = new CreateRoomRequest(roomName, SelectedMaxPlayers(), SelectedVisibility());

        createButton.interactable = false;
        StartCoroutine(Services.RoomCreator.CreateRoom(request, OnCreateResult));
    }

    void OnCreateResult(CreateRoomResult result)
    {
        createButton.interactable = true;

        if (!result.Success)
        {
            errorBanner.Show(result.Error);
            return;
        }

        waitingRoom.Bind(result.Room);
        navigator.Show(waitingRoomScreen);
    }
}
