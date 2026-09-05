using System.Collections;
using TMPro;
using UnityEngine;

// Reusable error banner: set the message, it shows itself, then auto-hides after a delay.
// Mirrors the errorPanel/errorText show-and-hide convention used for login/news errors in
// the sibling platform project — one shared instance, used by every lobby screen that can
// fail (Create Room, Join by Code, Browse Rooms).
public class TimedErrorBanner : MonoBehaviour
{
    [SerializeField] GameObject panel;
    [SerializeField] TMP_Text messageText;
    [SerializeField] float visibleSeconds = 2.5f;

    Coroutine hideCoroutine;

    public void Show(string message)
    {
        if (messageText != null) messageText.text = message;
        if (panel != null) panel.SetActive(true);

        if (hideCoroutine != null) StopCoroutine(hideCoroutine);
        hideCoroutine = StartCoroutine(HideAfterDelay());
    }

    IEnumerator HideAfterDelay()
    {
        yield return new WaitForSeconds(visibleSeconds);
        if (panel != null) panel.SetActive(false);
        hideCoroutine = null;
    }
}
