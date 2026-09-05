using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class JoinByCodeScreen : MonoBehaviour
{
    [SerializeField] ScreenNavigator navigator;
    [SerializeField] GameObject joinRoomScreen;
    [SerializeField] GameObject waitingRoomScreen;
    [SerializeField] WaitingRoomScreen waitingRoom;
    [SerializeField] TimedErrorBanner errorBanner;

    [SerializeField] TMP_InputField codeField;
    [SerializeField] Button joinButton;
    [SerializeField] Button backButton;

    void Awake()
    {
        joinButton.onClick.AddListener(OnJoinClicked);
        backButton.onClick.AddListener(() => navigator.Show(joinRoomScreen));
    }

    void OnEnable()
    {
        if (codeField != null) codeField.text = string.Empty;
    }

    void OnJoinClicked()
    {
        string code = codeField != null ? codeField.text : string.Empty;

        joinButton.interactable = false;
        StartCoroutine(Services.RoomJoiner.JoinByCode(code, OnJoinResult));
    }

    void OnJoinResult(JoinRoomResult result)
    {
        joinButton.interactable = true;

        if (!result.Success)
        {
            errorBanner.Show(result.Error);
            return;
        }

        waitingRoom.Bind(result.Room);
        navigator.Show(waitingRoomScreen);
    }
}
