using System;
using System.Collections;
using System.Threading.Tasks;
using Fusion;
using Fusion.Photon.Realtime;
using UnityEngine;

// Real Photon Fusion 2 Quick Match backend. GameMode.AutoHostOrClient: the first searcher becomes
// host, later ones join as clients; players never see the difference. Host/Client (not Shared)
// because car-vs-car contact needs a single authority to resolve it (GDD doc 05).
//
// Host migration: when the host drops, every survivor gets a token, shuts its runner down and
// starts a new one - the elected peer as host restoring the snapshot, the rest reconnecting.
//
// OPEN QUESTION: verify that matchmaking without a SessionName really filters by the GameId
// session property before relying on it to separate two games sharing one App ID.
public class QuickMatchFusionService : IQuickMatchService
{
    const int MigrationAttempts = 5;
    const int MigrationRetryMilliseconds = 1500;

    readonly QuickMatchRunnerFactory runnerFactory;

    NetworkRunner connectingRunner;
    FusionActiveQuickMatchSession activeSession;
    string gameId;

    public event Action HostLost;
    public event Action<IActiveQuickMatchSession> SessionRestored;
    public event Action<string> SessionFailed;

    public QuickMatchFusionService()
    {
        runnerFactory = new QuickMatchRunnerFactory(new PlayerLobbySpawner(),
            (runner, token) => _ = HandleHostMigration(runner, token));
    }

    static bool HasAppId
    {
        get
        {
            var settings = PhotonAppSettings.Global;
            return settings != null && settings.AppSettings != null && !string.IsNullOrEmpty(settings.AppSettings.AppIdFusion);
        }
    }

    public IEnumerator StartQuickMatch(string gameId, Action<StartQuickMatchResult> onResult)
    {
        if (!HasAppId)
        {
            onResult?.Invoke(StartQuickMatchResult.Fail("Photon Fusion App ID isn't configured yet."));
            yield break;
        }

        this.gameId = gameId;
        var runner = runnerFactory.CreateRunner();
        connectingRunner = runner;

        var args = runnerFactory.BaseArgs(gameId);
        args.GameMode = GameMode.AutoHostOrClient;
        args.IsVisible = true;
        args.IsOpen = true;

        var startTask = runner.StartGame(args);
        while (!startTask.IsCompleted) yield return null;

        bool cancelled = connectingRunner != runner;
        connectingRunner = null;

        if (!startTask.Result.Ok || cancelled || !runner.IsRunning)
        {
            if (runner != null) UnityEngine.Object.Destroy(runner.gameObject);
            onResult?.Invoke(StartQuickMatchResult.Fail(cancelled ? "Cancelled." : $"Could not find or open a match: {startTask.Result.ShutdownReason}"));
            yield break;
        }

        if (runner.IsServer) SpawnSessionState(runner);

        activeSession = new FusionActiveQuickMatchSession(runner);
        onResult?.Invoke(StartQuickMatchResult.Ok(activeSession));
    }

    public void CancelQuickMatch()
    {
        if (connectingRunner == null) return;

        var runner = connectingRunner;
        connectingRunner = null;
        if (runner.IsRunning) _ = runner.Shutdown();
        else UnityEngine.Object.Destroy(runner.gameObject);
    }

    // Host only: the single session-state object every client receives by replication.
    static void SpawnSessionState(NetworkRunner runner)
    {
        var prefab = Resources.Load<NetworkObject>("MatchmakingSessionState");
        if (prefab == null)
            Debug.LogError("[QuickMatchFusionService] Resources/MatchmakingSessionState.prefab not found.");
        else
            runner.Spawn(prefab, inputAuthority: null);
    }

    // Runs on every surviving peer. async (not a coroutine) because no MonoBehaviour survives
    // the old runner to host one.
    async Task HandleHostMigration(NetworkRunner deadRunner, HostMigrationToken token)
    {
        try
        {
            HostLost?.Invoke();
            activeSession?.Detach();
            activeSession = null;

            await deadRunner.Shutdown(true, ShutdownReason.HostMigration);

            // Photon may not have the new room ready yet, so retry a few times.
            for (int attempt = 1; attempt <= MigrationAttempts; attempt++)
            {
                var runner = runnerFactory.CreateRunner();
                var args = runnerFactory.BaseArgs(gameId);
                args.HostMigrationToken = token;
                args.HostMigrationResume = QuickMatchRunnerFactory.RestoreSnapshot;
                args.IsVisible = false; // reconnects come by token, not matchmaking
                args.IsOpen = true;

                var result = await runner.StartGame(args);
                if (result.Ok)
                {
                    activeSession = new FusionActiveQuickMatchSession(runner);
                    SessionRestored?.Invoke(activeSession);
                    return;
                }

                Debug.LogWarning($"[QuickMatchFusionService] Host migration attempt {attempt}/{MigrationAttempts} failed: {result.ShutdownReason}");
                if (runner != null) UnityEngine.Object.Destroy(runner.gameObject);
                await Task.Delay(MigrationRetryMilliseconds);
            }

            SessionFailed?.Invoke("Could not rejoin the match after the host left.");
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            SessionFailed?.Invoke(e.Message);
        }
    }
}
