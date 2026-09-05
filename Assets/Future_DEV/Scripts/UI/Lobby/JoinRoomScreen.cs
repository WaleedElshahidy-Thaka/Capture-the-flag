using UnityEngine;
using UnityEngine.UI;

public class JoinRoomScreen : MonoBehaviour
{
    [SerializeField] ScreenNavigator navigator;
    [SerializeField] GameObject entryScreen;
    [SerializeField] GameObject joinByCodeScreen;
    [SerializeField] GameObject browseRoomsScreen;
    [SerializeField] Button joinByCodeButton;
    [SerializeField] Button browseRoomsButton;
    [SerializeField] Button backButton;

    void Awake()
    {
        joinByCodeButton.onClick.AddListener(() => navigator.Show(joinByCodeScreen));
        browseRoomsButton.onClick.AddListener(() => navigator.Show(browseRoomsScreen));
        backButton.onClick.AddListener(() => navigator.Show(entryScreen));
    }
}
