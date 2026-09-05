using UnityEngine;
using UnityEngine.UI;

public class BrowseRoomsScreen : MonoBehaviour
{
    [SerializeField] ScreenNavigator navigator;
    [SerializeField] GameObject joinRoomScreen;
    [SerializeField] GameObject waitingRoomScreen;
    [SerializeField] WaitingRoomScreen waitingRoom;
    [SerializeField] TimedErrorBanner errorBanner;

    [SerializeField] Transform listContent;
    [SerializeField] RoomListCard cardTemplate;
    [SerializeField] Button refreshButton;
    [SerializeField] Button backButton;

    void Awake()
    {
        refreshButton.onClick.AddListener(Refresh);
        backButton.onClick.AddListener(() => navigator.Show(joinRoomScreen));
    }

    void OnEnable() => Refresh();

    void Refresh()
    {
        StartCoroutine(Services.RoomBrowser.GetAvailableRooms(OnRoomsResult));
    }

    void OnRoomsResult(RoomListResult result)
    {
        foreach (Transform child in listContent)
            Destroy(child.gameObject);

        if (!result.Success)
        {
            errorBanner.Show(result.Error);
            return;
        }

        foreach (var room in result.Rooms)
        {
            var card = Instantiate(cardTemplate, listContent);
            card.gameObject.SetActive(true);
            card.Setup(room, OnRoomCardClicked);
        }
    }

    void OnRoomCardClicked(RoomSummary room)
    {
        StartCoroutine(Services.RoomJoiner.JoinByCode(room.RoomCode, OnJoinResult));
    }

    void OnJoinResult(JoinRoomResult result)
    {
        if (!result.Success)
        {
            errorBanner.Show(result.Error);
            return;
        }

        waitingRoom.Bind(result.Room);
        navigator.Show(waitingRoomScreen);
    }
}
