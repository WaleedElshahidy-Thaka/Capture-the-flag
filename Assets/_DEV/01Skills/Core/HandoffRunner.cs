using UnityEngine;

// Hosts the wait-then-quit coroutine for AppHandoff.SwitchTo.
//
// It exists because the app closing itself is not always a MonoBehaviour: an exit button can
// live on a panel that gets destroyed by the very transition it started, and a coroutine dies
// with its host. This runner is its own DontDestroyOnLoad object, so the wait survives scene
// loads and disabled panels alike.
//
// Duplicated verbatim in the launcher, the lobby and every game. Keep the copies identical.
public class HandoffRunner : MonoBehaviour
{
    public static void Begin(string handshakeId, float timeoutSeconds = AppHandoff.DefaultTimeoutSeconds)
    {
        var go = new GameObject("~AppHandoffRunner");
        DontDestroyOnLoad(go);
        go.AddComponent<HandoffRunner>()
          .StartCoroutine(AppHandoff.WaitForReadyThenQuit(handshakeId, timeoutSeconds));
    }
}
