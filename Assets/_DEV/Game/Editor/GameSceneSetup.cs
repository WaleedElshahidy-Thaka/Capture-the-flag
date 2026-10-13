using Fusion;
using Fusion.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Menu: Game > Setup Game Scene. Builds (or rebuilds) Game.unity - arena, lobby UI and the
// networked prefabs - from code. Idempotent: safe to re-run after script changes.
// The single scene holds both the matchmaking lobby and gameplay; there is no scene load between them.
public static class GameSceneSetup
{
    const string ScenePath = "Assets/_DEV/Game/Scenes/Game.unity";
    const string SessionStatePrefabPath = "Assets/_DEV/Matchmaking/Resources/MatchmakingSessionState.prefab";

    [MenuItem("Game/Setup Game Scene")]
    public static void SetupScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[GameSceneSetup] Exit Play Mode before running this.");
            return;
        }

        EnsureFolders();
        OpenOrCreateScene();

        ArenaBuilder.Build();
        PlayerCarPrefabBuilder.Build();
        BuildSessionStatePrefab();

        var flowController = SetupUtils.GetOrCreatePlain("MatchmakingFlowController", null, typeof(MatchmakingFlowController))
            .GetComponent<MatchmakingFlowController>();
        MatchmakingUIBuilder.Build(flowController);

        var activeScene = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);

        NetworkProjectConfigUtilities.RebuildPrefabTable();

        Debug.Log($"[GameSceneSetup] Done. Open {ScenePath} and press Play - click Quick Match to start (backend chosen in MatchmakingServices.cs).");
    }

    static void OpenOrCreateScene()
    {
        if (System.IO.File.Exists(ScenePath))
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            return;
        }

        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        EditorSceneManager.SaveScene(scene, ScenePath);
    }

    static void BuildSessionStatePrefab()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(SessionStatePrefabPath) != null) return;

        var go = new GameObject("MatchmakingSessionState", typeof(NetworkObject), typeof(MatchmakingSessionState));
        PrefabUtility.SaveAsPrefabAsset(go, SessionStatePrefabPath);
        Object.DestroyImmediate(go);
    }

    static void EnsureFolders()
    {
        SetupUtils.EnsureFolder("Assets/_DEV/Matchmaking", "Resources");
        SetupUtils.EnsureFolder("Assets/_DEV/Matchmaking", "Prefabs");
        SetupUtils.EnsureFolder("Assets/_DEV/Game", "Player");
        SetupUtils.EnsureFolder("Assets/_DEV/Game/Player", "Resources");
    }
}
