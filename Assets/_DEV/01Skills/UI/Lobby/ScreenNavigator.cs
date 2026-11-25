using UnityEngine;

// Minimal screen switcher: exactly one registered screen is active at a time. No animation
// coupling — screens show/hide via SetActive, matching the "simple UI with texts" scope of
// this pass. Can be layered with transitions later without touching screen logic.
public class ScreenNavigator : MonoBehaviour
{
    [SerializeField] GameObject[] screens;

    public void Show(GameObject screen)
    {
        for (int i = 0; i < screens.Length; i++)
            screens[i].SetActive(screens[i] == screen);
    }
}
