using System.Collections;
using UnityEngine;

// Reports this app's first rendered frame back to whichever app launched it, so that app
// can stay on screen until we are actually visible instead of quitting into a blank
// desktop. See AppHandoff for the protocol.
//
// Self-installing on purpose: it hooks itself in via RuntimeInitializeOnLoadMethod rather
// than living in a scene, so adding a new game to the cycle needs no prefab, no scene
// wiring and nothing to forget. Dropping the file into the project is the whole install.
public class HandoffReadySignaller : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        // No --handshake= means we were started directly rather than handed off to, so
        // there is no outgoing app waiting on us and nothing to report.
        if (string.IsNullOrEmpty(LaunchArgs.Get(AppHandoff.ArgKey))) return;

        var go = new GameObject("~AppHandoffReadySignaller");
        DontDestroyOnLoad(go);
        go.AddComponent<HandoffReadySignaller>();
    }

    IEnumerator Start()
    {
        // WaitForEndOfFrame resumes after this frame's rendering has been submitted; the
        // second one guarantees a frame has actually been presented to the window, which
        // is the moment the outgoing app can disappear without the user noticing a gap.
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();

        AppHandoff.SignalReady();
        Destroy(gameObject);
    }
}
