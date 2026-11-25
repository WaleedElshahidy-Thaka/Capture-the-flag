using UnityEngine;
using UnityEngine.UI;

// The first screen this project shows. Mode Select and Game Select happen upstream, in the
// separate launcher project — this project starts directly here per the game's own spec.
public class CtfEntryScreen : MonoBehaviour
{
    [SerializeField] ScreenNavigator navigator;
    [SerializeField] GameObject createRoomScreen;
    [SerializeField] GameObject joinRoomScreen;
    [SerializeField] Button createRoomButton;
    [SerializeField] Button joinRoomButton;

    void Awake()
    {
        createRoomButton.onClick.AddListener(() => navigator.Show(createRoomScreen));
        joinRoomButton.onClick.AddListener(() => navigator.Show(joinRoomScreen));
    }
}
