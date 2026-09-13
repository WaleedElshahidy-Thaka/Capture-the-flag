using System;
using Fusion;
using Fusion.Editor;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

// The one accepted scene: matchmaking lobby and real gameplay together, no second scene, no
// scene load between the two phases. Every connected player's real PlayerCar is spawned and
// positioned from the moment they join (see PlayerLobbySpawner) - there's no separate lobby
// placeholder visual. MatchStarter just flips PlayerMatchState.CanMove on once a match starts.
// Combines what used to be two separate setup scripts: DriveSceneSetup (arena, PlayerCar,
// camera - now deleted, Drive.unity's job is done) and MatchmakingSceneSetup (lobby UI,
// networked prefabs - Matchmaking.unity deleted the same way). Idempotent - safe to re-run
// after script changes.
public static class GameSceneSetup
{
    const string ScenePath = "Assets/_DEV/Game/Scenes/Game.unity";
    const string MatchmakingResourcesPath = "Assets/_DEV/Matchmaking/Resources";
    const string PlayerResourcesPath = "Assets/_DEV/Game/Player/Resources";
    const string UndoLabel = "Setup Game Scene";

    static readonly Color PanelColor = new Color(0.08f, 0.09f, 0.12f, 0.96f);
    static readonly Color ButtonColor = new Color(0.18f, 0.22f, 0.30f, 1f);

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

        BuildFloor();
        BuildWalls();
        BuildCamera();

        BuildPlayerCarPrefab();
        BuildMatchmakingSessionStatePrefab();

        EnsureEventSystem();
        var canvas = GetOrCreateCanvasUI();
        BuildScreen(canvas, out var refs);

        var flowControllerGO = GetOrCreatePlain("MatchmakingFlowController", null, typeof(MatchmakingFlowController));

        WireScreen(canvas, refs, flowControllerGO.GetComponent<MatchmakingFlowController>());

        var activeScene = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);

        NetworkProjectConfigUtilities.RebuildPrefabTable();

        Debug.Log($"[GameSceneSetup] Done. Open {ScenePath} and press Play - click Find Match to start (real Fusion backend, see MatchmakingServices.cs).");
    }

    // ─── Scene ───────────────────────────────────────────────────────────

    static void OpenOrCreateScene()
    {
        if (System.IO.File.Exists(ScenePath))
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }
        else
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }
    }

    // ─── Arena (from the retired DriveSceneSetup) ───────────────────────

    static void BuildFloor()
    {
        if (GameObject.Find("Floor") != null) return;

        var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "Floor";
        floor.transform.position = Vector3.zero;
        floor.transform.localScale = new Vector3(5f, 1f, 5f);
        Undo.RegisterCreatedObjectUndo(floor, UndoLabel);
    }

    // Arena boundary so wall hits are actually testable - plain colliders, no Rigidbody, so
    // they're static/immovable to PhysX by default.
    static void BuildWalls()
    {
        if (GameObject.Find("Walls") != null) return;

        var wallsParent = new GameObject("Walls");
        Undo.RegisterCreatedObjectUndo(wallsParent, UndoLabel);

        BuildWall(wallsParent.transform, "Wall_North", new Vector3(0f, 1f, 25f), new Vector3(50f, 2f, 1f));
        BuildWall(wallsParent.transform, "Wall_South", new Vector3(0f, 1f, -25f), new Vector3(50f, 2f, 1f));
        BuildWall(wallsParent.transform, "Wall_East", new Vector3(25f, 1f, 0f), new Vector3(1f, 2f, 50f));
        BuildWall(wallsParent.transform, "Wall_West", new Vector3(-25f, 1f, 0f), new Vector3(1f, 2f, 50f));
    }

    static void BuildWall(Transform parent, string name, Vector3 position, Vector3 scale)
    {
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = name;
        wall.transform.SetParent(parent, false);
        wall.transform.position = position;
        wall.transform.localScale = scale;
    }

    // No target wired here - the car this should follow doesn't exist at edit time, only once
    // MatchStarter spawns it. PlayerMovement.Spawned() wires PlayerCamera.SetTarget at runtime
    // instead, for its own local car only. Until then the camera just sits here (PlayerCamera's
    // LateUpdate no-ops with no target set), giving a static lobby overview for free.
    static void BuildCamera()
    {
        var cameraGO = GameObject.Find("Main Camera");
        if (cameraGO == null)
        {
            Debug.LogWarning("[GameSceneSetup] No 'Main Camera' found - PlayerCamera not wired.");
            return;
        }

        if (cameraGO.GetComponent<PlayerCamera>() == null) Undo.AddComponent<PlayerCamera>(cameraGO);
        cameraGO.transform.position = new Vector3(0f, 12f, -14f);
        cameraGO.transform.rotation = Quaternion.Euler(35f, 0f, 0f);
    }

    // ─── PlayerCar (from the retired DriveSceneSetup) ───────────────────

    // Under Game/Player/Resources rather than Matchmaking/Resources - Unity's Resources.Load
    // searches every Resources/ folder in the project regardless of location, so this can stay
    // organized with PlayerMovement/PlayerCamera while still being loadable by MatchStarter
    // (Matchmaking/Flow) by name alone.
    const string PlayerCarPrefabPath = PlayerResourcesPath + "/PlayerCar.prefab";

    static void BuildPlayerCarPrefab()
    {
        var existing = GameObject.Find("PlayerCarBuildTemp");
        GameObject go = existing != null ? existing : new GameObject("PlayerCarBuildTemp", typeof(Rigidbody), typeof(BoxCollider));

        var body = go.GetComponent<Rigidbody>();
        body.mass = 50f;
        body.linearDamping = 1.5f;
        body.angularDamping = 3f;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        var collider = go.GetComponent<BoxCollider>();
        collider.size = new Vector3(1.2f, 0.8f, 1.6f);
        collider.center = new Vector3(0f, 0.4f, 0f);

        if (go.GetComponent<PlayerMovement>() == null) Undo.AddComponent<PlayerMovement>(go);
        if (go.GetComponent<PlayerMatchState>() == null) Undo.AddComponent<PlayerMatchState>(go);
        if (go.GetComponent<NetworkObject>() == null) Undo.AddComponent<NetworkObject>(go);

        var networkTransform = go.GetComponent<NetworkTransform>();
        if (networkTransform == null) networkTransform = Undo.AddComponent<NetworkTransform>(go);
        // Forecast Physics (extrapolates from the Rigidbody's velocity instead of resimulating)
        // - see Documentation/Networking_Progress.md. Requires
        // NetworkProjectConfig.PhysicsForecast enabled globally too.
        networkTransform.PhysicsSettings.ForecastEnabled = true;

        var existingVisual = go.transform.Find("Visual");
        if (existingVisual != null) UnityEngine.Object.DestroyImmediate(existingVisual.gameObject);

        var playerArt = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_DEV/Game/Art/Bolt/PlayerRobot.prefab");
        GameObject visual = playerArt != null
            ? (GameObject)PrefabUtility.InstantiatePrefab(playerArt)
            : GameObject.CreatePrimitive(PrimitiveType.Capsule);
        visual.name = "Visual";
        visual.transform.SetParent(go.transform, false);
        visual.transform.localPosition = playerArt != null ? Vector3.zero : new Vector3(0f, 0.4f, 0f);
        visual.transform.localRotation = Quaternion.identity;

        var visualCollider = visual.GetComponent<Collider>();
        if (visualCollider != null) UnityEngine.Object.DestroyImmediate(visualCollider);

        WireTires(go, visual);

        PrefabUtility.SaveAsPrefabAsset(go, PlayerCarPrefabPath);
        UnityEngine.Object.DestroyImmediate(go);
    }

    // PlayerRobot.prefab's own child names/positions - "Tire_Left"/"Tire_Right" sit further
    // forward (+Z) than the "(1)" pair, so those are the front (steered) wheels.
    static void WireTires(GameObject car, GameObject visual)
    {
        var movement = car.GetComponent<PlayerMovement>();
        if (movement == null) return;

        var so = new SerializedObject(movement);
        so.FindProperty("frontLeftTire").objectReferenceValue = visual.transform.Find("Tire_Left");
        so.FindProperty("frontRightTire").objectReferenceValue = visual.transform.Find("Tire_Right");
        so.FindProperty("rearLeftTire").objectReferenceValue = visual.transform.Find("Tire_Left (1)");
        so.FindProperty("rearRightTire").objectReferenceValue = visual.transform.Find("Tire_Right (1)");
        so.ApplyModifiedProperties();
    }

    // ─── Matchmaking prefabs (from the retired MatchmakingSceneSetup) ───

    static void BuildMatchmakingSessionStatePrefab()
    {
        string path = $"{MatchmakingResourcesPath}/MatchmakingSessionState.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;

        var go = new GameObject("MatchmakingSessionState", typeof(NetworkObject), typeof(MatchmakingSessionState));
        PrefabUtility.SaveAsPrefabAsset(go, path);
        UnityEngine.Object.DestroyImmediate(go);
    }

    // ─── Screen ──────────────────────────────────────────────────────────

    class ScreenRefs
    {
        public GameObject idlePanel, searchingPanel, soloBotPanel, readyPanel, startingPanel;
        public Button findMatchButton, startWithBotsButton, readyToggleButton, cancelButton;
        public TMP_Text searchingText, readyToggleLabel, loadingText;
    }

    static void BuildScreen(RectTransform canvas, out ScreenRefs refs)
    {
        refs = new ScreenRefs();

        var root = GetOrCreateUI("MatchmakingScreen", canvas, typeof(MatchmakingScreen));
        Stretch(root);

        var idle = Panel("IdlePanel", root);
        var idleContent = VList("Content", idle);
        refs.findMatchButton = Btn("FindMatchButton", idleContent, "Find Match");
        refs.loadingText = TextEl("LoadingText", idleContent, "Loading Game... 0s", 24);
        refs.idlePanel = idle.gameObject;

        var searching = Panel("SearchingPanel", root);
        var searchingContent = VList("Content", searching);
        refs.searchingText = TextEl("SearchingText", searchingContent, "Finding player... 0s", 32);

        var soloBot = GetOrCreateUI("SoloBotPanel", searchingContent, typeof(VerticalLayoutGroup));
        refs.startWithBotsButton = Btn("StartWithBotsButton", soloBot, "Don't wait, start with computer players");
        refs.soloBotPanel = soloBot.gameObject;

        var ready = GetOrCreateUI("ReadyPanel", searchingContent, typeof(VerticalLayoutGroup));
        refs.readyToggleButton = Btn("ReadyToggleButton", ready, "Don't wait, play with computer players");
        refs.readyToggleLabel = refs.readyToggleButton.GetComponentInChildren<TMP_Text>();
        refs.readyPanel = ready.gameObject;

        refs.cancelButton = Btn("CancelButton", searchingContent, "Cancel");
        refs.searchingPanel = searching.gameObject;

        var starting = Panel("StartingPanel", root);
        var startingContent = VList("Content", starting);
        TextEl("StartingText", startingContent, "Starting...", 40);
        refs.startingPanel = starting.gameObject;
    }

    static void WireScreen(RectTransform canvas, ScreenRefs refs, MatchmakingFlowController flowController)
    {
        var screen = canvas.Find("MatchmakingScreen").GetComponent<MatchmakingScreen>();

        SetField(screen, "flowController", flowController);
        SetField(screen, "idlePanel", refs.idlePanel);
        SetField(screen, "findMatchButton", refs.findMatchButton);
        SetField(screen, "loadingText", refs.loadingText);
        SetField(screen, "searchingPanel", refs.searchingPanel);
        SetField(screen, "searchingText", refs.searchingText);
        SetField(screen, "soloBotPanel", refs.soloBotPanel);
        SetField(screen, "startWithBotsButton", refs.startWithBotsButton);
        SetField(screen, "readyPanel", refs.readyPanel);
        SetField(screen, "readyToggleButton", refs.readyToggleButton);
        SetField(screen, "readyToggleLabel", refs.readyToggleLabel);
        SetField(screen, "startingPanel", refs.startingPanel);
        SetField(screen, "cancelButton", refs.cancelButton);
    }

    // ─── Low-level UI builders ───────────────────────────────────────────

    static RectTransform Panel(string name, Transform parent)
    {
        var rt = GetOrCreateUI(name, parent, typeof(Image));
        Stretch(rt);
        rt.GetComponent<Image>().color = PanelColor;
        return rt;
    }

    static RectTransform VList(string name, Transform parent, float width = 640, float spacing = 18)
    {
        var rt = GetOrCreateUI(name, parent, typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(width, 0);
        rt.anchoredPosition = Vector2.zero;

        var vlg = rt.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = spacing;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        rt.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return rt;
    }

    static TMP_Text TextEl(string name, Transform parent, string content, float size, float height = 48)
    {
        var rt = GetOrCreateUI(name, parent, typeof(TextMeshProUGUI), typeof(LayoutElement));
        var tmp = rt.GetComponent<TextMeshProUGUI>();
        tmp.text = content;
        tmp.fontSize = size;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;

        var le = rt.GetComponent<LayoutElement>();
        le.preferredHeight = height;
        le.minHeight = height;
        return tmp;
    }

    static Button Btn(string name, Transform parent, string label, float height = 56)
    {
        var rt = GetOrCreateUI(name, parent, typeof(Image), typeof(Button), typeof(LayoutElement));
        var img = rt.GetComponent<Image>();
        img.color = ButtonColor;

        var le = rt.GetComponent<LayoutElement>();
        le.preferredHeight = height;
        le.minHeight = height;

        var labelRt = GetOrCreateUI("Label", rt, typeof(TextMeshProUGUI));
        Stretch(labelRt);
        var tmp = labelRt.GetComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 24;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;

        var button = rt.GetComponent<Button>();
        button.targetGraphic = img;
        return button;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static RectTransform GetOrCreateUI(string name, Transform parent, params Type[] components)
    {
        var existing = parent != null ? parent.Find(name) : null;
        if (existing != null) return existing.GetComponent<RectTransform>();

        var types = new Type[components.Length + 1];
        types[0] = typeof(RectTransform);
        Array.Copy(components, 0, types, 1, components.Length);

        var go = new GameObject(name, types);
        go.transform.SetParent(parent, false);
        Undo.RegisterCreatedObjectUndo(go, UndoLabel);
        return go.GetComponent<RectTransform>();
    }

    static GameObject GetOrCreatePlain(string name, Transform parent, params Type[] components)
    {
        Transform existing = parent != null ? parent.Find(name) : GameObject.Find(name)?.transform;
        if (existing != null) return existing.gameObject;

        var go = new GameObject(name, components);
        if (parent != null) go.transform.SetParent(parent, false);
        Undo.RegisterCreatedObjectUndo(go, UndoLabel);
        return go;
    }

    static RectTransform GetOrCreateCanvasUI()
    {
        var existing = GameObject.Find("MatchmakingCanvas");
        if (existing != null) return existing.GetComponent<RectTransform>();

        var go = new GameObject("MatchmakingCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(go, UndoLabel);

        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        return go.GetComponent<RectTransform>();
    }

    static void EnsureEventSystem()
    {
        if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() != null) return;

        var go = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
        go.AddComponent<InputSystemUIInputModule>();
#else
        go.AddComponent<StandaloneInputModule>();
#endif
        Undo.RegisterCreatedObjectUndo(go, UndoLabel);
    }

    static void EnsureFolders()
    {
        EnsureFolder("Assets/_DEV/Matchmaking", "Resources");
        EnsureFolder("Assets/_DEV/Matchmaking", "Prefabs");
        EnsureFolder("Assets/_DEV/Game", "Player");
        EnsureFolder("Assets/_DEV/Game/Player", "Resources");
    }

    static void EnsureFolder(string parent, string name)
    {
        string path = $"{parent}/{name}";
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, name);
    }

    static void SetField(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
    {
        var so = new SerializedObject(target);
        var prop = so.FindProperty(propertyName);
        if (prop == null)
        {
            Debug.LogError($"[GameSceneSetup] Field '{propertyName}' not found on {target.GetType().Name}.");
            return;
        }
        prop.objectReferenceValue = value;
        so.ApplyModifiedProperties();
    }
}
