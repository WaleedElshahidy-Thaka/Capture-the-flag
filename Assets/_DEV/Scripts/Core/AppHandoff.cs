using System;
using System.Collections;
using System.IO;
using UnityEngine;

// Cross-process handshake that makes app-to-app switches seamless.
//
// The problem: ProcessLauncher.TryStart() returns as soon as Windows has created the
// child process — long before that child's Unity player has booted, loaded its first
// scene and drawn anything. An app that calls Application.Quit() at that moment leaves
// the user staring at the desktop for the seconds the child spends loading.
//
// The fix: the outgoing app keeps rendering until the incoming app reports that it has
// presented its first frame, and only then quits. The incoming app reports readiness by
// creating a file named after the one-shot id it was handed on its command line
// (--handshake=<id>); the outgoing app polls for that file. Minting a fresh GUID per
// launch means a stale file left behind by a crashed run can never satisfy a later
// handshake.
//
// This file is duplicated verbatim in every app in the cycle (launcher, lobby, and each
// game). Keep the copies identical — the file name and the --handshake= key are the
// contract between processes.
public static class AppHandoff
{
    /// Command-line key the outgoing app uses to hand a one-shot id to the incoming app.
    public const string ArgKey = "handshake";

    /// How long the outgoing app stays alive waiting for the incoming app's first frame
    /// before giving up and quitting anyway. Generous: a cold start off a slow disk can
    /// take a while, and a few extra seconds on screen beats quitting to a blank desktop.
    public const float DefaultTimeoutSeconds = 25f;

    static string SignalDirectory => Path.Combine(Path.GetTempPath(), "ZeroOneHandoff");

    static string SignalPath(string handshakeId) => Path.Combine(SignalDirectory, handshakeId + ".ready");

    /// Outgoing side: mint the id to pass as --handshake= on the incoming app's command line.
    public static string NewId() => Guid.NewGuid().ToString("N");

    /// <summary>
    /// The whole outgoing half in one call: start the next app, stay on screen until it has
    /// drawn its first frame, then quit. Returns false and leaves this app running when the
    /// launch itself failed, so the caller can show an error instead of closing into nothing.
    ///
    /// The --handshake= argument is prepended here rather than by callers — forgetting it is
    /// silent, and the symptom (a desktop flash mid-switch) shows up far from the cause.
    /// </summary>
    public static bool SwitchTo(string exePath, string arguments, out string error,
                                float timeoutSeconds = DefaultTimeoutSeconds)
    {
        // Set before the child can steal focus, and deliberately not left to Player Settings.
        // A player built with "Run In Background" off has its whole loop paused by Unity the
        // moment the incoming window takes focus — which would freeze the wait below forever,
        // timeout included, leaving both windows on screen. Flipping it here scopes the
        // change to the handoff, so normal play still pauses on focus loss.
        Application.runInBackground = true;

        string handshakeId = NewId();
        string fullArguments = $"--{ArgKey}={handshakeId} {arguments}".Trim();

        if (!ProcessLauncher.TryStart(exePath, fullArguments, out error))
            return false;

        HandoffRunner.Begin(handshakeId, timeoutSeconds);
        return true;
    }

    /// Outgoing side: stay on screen until the incoming app reports its first frame, then
    /// quit. Start this as a coroutine right after ProcessLauncher.TryStart() succeeded.
    public static IEnumerator WaitForReadyThenQuit(string handshakeId, float timeoutSeconds = DefaultTimeoutSeconds)
    {
        yield return WaitForReady(handshakeId, timeoutSeconds);
        Quit();
    }

    /// Outgoing side: completes when the incoming app has presented its first frame, or
    /// when timeoutSeconds elapses — whichever happens first.
    public static IEnumerator WaitForReady(string handshakeId, float timeoutSeconds = DefaultTimeoutSeconds)
    {
        string path = SignalPath(handshakeId);
        float deadline = Time.realtimeSinceStartup + timeoutSeconds;

        // Polled on unscaled real time and a plain per-frame yield: these transitions are
        // usually triggered from a pause menu, where Time.timeScale is 0 and anything
        // built on scaled time (WaitForSeconds) would never resume.
        while (Time.realtimeSinceStartup < deadline)
        {
            if (FileExists(path))
            {
                TryDelete(path);
                yield break;
            }
            yield return null;
        }

        Debug.LogWarning($"[AppHandoff] Timed out after {timeoutSeconds:0}s waiting for the incoming app to report its first frame (handshake {handshakeId}). Quitting anyway.");
    }

    /// Incoming side: tell whoever launched us that we are on screen and they may quit.
    /// Called automatically by HandoffReadySignaller once the first frame has rendered;
    /// a no-op when this app was started directly and nobody is waiting on us.
    public static void SignalReady()
    {
        string handshakeId = LaunchArgs.Get(ArgKey);
        if (string.IsNullOrEmpty(handshakeId)) return;

        try
        {
            Directory.CreateDirectory(SignalDirectory);
            File.WriteAllText(SignalPath(handshakeId), DateTime.UtcNow.ToString("o"));
        }
        catch (Exception ex)
        {
            // Not fatal: the outgoing app falls back to its timeout and still closes, so
            // the worst case is a slower switch rather than two windows left open.
            Debug.LogWarning($"[AppHandoff] Could not write the ready signal: {ex.Message}");
        }
    }

    /// Quit that also works in the Editor, so transition code can be tested in Play mode.
    public static void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // The signal lives in the user's temp folder, which a locked-down machine or an
    // aggressive cleaner can make unreadable mid-wait. Treat any failure as "not ready
    // yet" and let the timeout decide, rather than throwing out of a coroutine.
    static bool FileExists(string path)
    {
        try { return File.Exists(path); }
        catch { return false; }
    }

    static void TryDelete(string path)
    {
        try { File.Delete(path); }
        catch { /* leaving one dead file in temp is harmless — ids are never reused */ }
    }
}
