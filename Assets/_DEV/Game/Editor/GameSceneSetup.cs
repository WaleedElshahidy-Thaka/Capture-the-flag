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

        var arena = GetOrCreateArenaBounds();
        BuildFloor(arena);
        BuildWalls(arena);
        BuildRamp();
        BuildCurve();
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

    // Sized from the scene's ArenaBounds component rather than hardcoded here, so the size can
    // be changed in the inspector and survives re-running this command. Created with defaults
    // on a fresh scene; an existing one keeps whatever was set on it.
    static ArenaBounds GetOrCreateArenaBounds()
    {
        var go = GetOrCreatePlain("ArenaBounds", null, typeof(ArenaBounds));
        return go.GetComponent<ArenaBounds>();
    }

    // Rebuilt from ArenaBounds every run rather than skipped-if-present, so changing the size in
    // the inspector and re-running actually resizes the arena.
    static void BuildFloor(ArenaBounds arena)
    {
        var existing = GameObject.Find("Floor");
        if (existing != null) UnityEngine.Object.DestroyImmediate(existing);

        var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "Floor";
        floor.transform.position = Vector3.zero;
        // Unity's plane primitive is 10 units across at scale 1.
        float planeScale = arena.FloorSize / 10f;
        floor.transform.localScale = new Vector3(planeScale, 1f, planeScale);
        Undo.RegisterCreatedObjectUndo(floor, UndoLabel);
    }

    // Arena boundary so wall hits are actually testable - plain colliders, no Rigidbody, so
    // they're static/immovable to PhysX by default. Closed on all four sides, per Glowtag FD-06
    // ("fully closed, no out-of-bounds volume, no falls, no gaps").
    static void BuildWalls(ArenaBounds arena)
    {
        var existing = GameObject.Find("Walls");
        if (existing != null) UnityEngine.Object.DestroyImmediate(existing);

        var wallsParent = new GameObject("Walls");
        Undo.RegisterCreatedObjectUndo(wallsParent, UndoLabel);

        float e = arena.HalfExtent;
        float h = arena.WallHeight;
        float t = arena.WallThickness;
        float span = arena.FloorSize + t;

        BuildWall(wallsParent.transform, "Wall_North", new Vector3(0f, h * 0.5f, e), new Vector3(span, h, t));
        BuildWall(wallsParent.transform, "Wall_South", new Vector3(0f, h * 0.5f, -e), new Vector3(span, h, t));
        BuildWall(wallsParent.transform, "Wall_East", new Vector3(e, h * 0.5f, 0f), new Vector3(t, h, span));
        BuildWall(wallsParent.transform, "Wall_West", new Vector3(-e, h * 0.5f, 0f), new Vector3(t, h, span));
    }

    static void BuildWall(Transform parent, string name, Vector3 position, Vector3 scale)
    {
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = name;
        wall.transform.SetParent(parent, false);
        wall.transform.position = position;
        wall.transform.localScale = scale;
    }

    // Terrain to actually test driving physics against, not just a flat plate - player spawns
    // (see PlayerLobbySpawner.SpawnOffsets) cluster near the arena center, so both features sit
    // well clear of them, out toward the +Z and -X edges of the 50x50 arena.
    static void BuildRamp()
    {
        if (GameObject.Find("Ramp") != null) return;

        var parent = new GameObject("Ramp");
        Undo.RegisterCreatedObjectUndo(parent, UndoLabel);

        var incline = GameObject.CreatePrimitive(PrimitiveType.Cube);
        incline.name = "Incline";
        incline.transform.SetParent(parent.transform, false);
        incline.transform.position = new Vector3(0f, 1.7f, 15f);
        incline.transform.rotation = Quaternion.Euler(-22f, 0f, 0f);
        incline.transform.localScale = new Vector3(6f, 0.5f, 9f);

        var platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
        platform.name = "Platform";
        platform.transform.SetParent(parent.transform, false);
        platform.transform.position = new Vector3(0f, 3.6f, 21.5f);
        platform.transform.localScale = new Vector3(7f, 0.5f, 6f);
    }

    // A banked curved section, built from overlapping straight segments (this project only
    // ever builds scenes from primitives, no custom meshes) following an arc - something to
    // actually test steering/drift/grip on besides straight flat ground.
    static void BuildCurve()
    {
        if (GameObject.Find("Curve") != null) return;

        var parent = new GameObject("Curve");
        Undo.RegisterCreatedObjectUndo(parent, UndoLabel);

        const int segmentCount = 14;
        const float radius = 11f;
        const float arcDegrees = 150f;
        const float trackWidth = 6f;
        const float bankAngle = 10f; // degrees, tilts each segment toward the inside of the turn
        Vector3 center = new Vector3(-18f, 0f, -6f);

        float segAngle = arcDegrees / segmentCount;
        // Slight overlap so consecutive segments don't leave a gap a wheel could drop into.
        float segLength = 2f * radius * Mathf.Sin(segAngle * Mathf.Deg2Rad * 0.5f) * 1.1f;

        for (int i = 0; i < segmentCount; i++)
        {
            float angle = (i + 0.5f) * segAngle;
            float rad = angle * Mathf.Deg2Rad;
            Vector3 pos = center + new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad)) * radius;

            var segment = GameObject.CreatePrimitive(PrimitiveType.Cube);
            segment.name = $"CurveSegment_{i}";
            segment.transform.SetParent(parent.transform, false);
            segment.transform.position = pos + Vector3.up * 0.25f;
            segment.transform.rotation = Quaternion.Euler(0f, angle, bankAngle);
            segment.transform.localScale = new Vector3(trackWidth, 0.5f, segLength);
        }
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
        GameObject go = existing != null ? existing : new GameObject("PlayerCarBuildTemp", typeof(Rigidbody));

        var body = go.GetComponent<Rigidbody>();
        body.mass = 50f;
        body.linearDamping = 1.5f;
        body.angularDamping = 3f;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

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
        BuildColliders(go, visual);

        PrefabUtility.SaveAsPrefabAsset(go, PlayerCarPrefabPath);
        UnityEngine.Object.DestroyImmediate(go);
    }

    // A compound of primitives that approximates the robot's actual shape: one box for the body
    // sitting above the wheels, plus a sphere at each wheel. Not one box around everything, and
    // not mesh colliders (non-convex meshes on a moving Rigidbody are unreliable in PhysX).
    //
    // Why the shape matters now that the Rigidbody's rotation is no longer frozen: a single box
    // tumbles and rests like a box, which is what made the car slide over ground obstacles
    // instead of riding them. With the body raised clear of ride height, the raycast probes own
    // ground contact while upright - and if the car does end up on its side, it rests on its
    // real silhouette instead of a crate.
    //
    // The body box is deliberately raised well above the wheels so it never touches the floor in
    // normal driving. When it did (bottom 5cm above ride height), PhysX's box contact won over
    // the suspension and the whole car moved as one rigid block.
    static void BuildColliders(GameObject car, GameObject visual)
    {
        // Idempotent: drop whatever colliders exist from a previous run before rebuilding,
        // rather than accumulating duplicates each time this menu command re-runs.
        foreach (var existingCollider in car.GetComponents<Collider>())
            UnityEngine.Object.DestroyImmediate(existingCollider);

        float wheelRadius = ResolveWheelRadius(visual);

        var chassisBody = Undo.AddComponent<BoxCollider>(car);
        chassisBody.size = new Vector3(0.9f, 0.7f, 1.1f);
        chassisBody.center = new Vector3(0f, wheelRadius * 2f + 0.15f, 0f);

        AddWheelSphere(car, visual, "Tire_Left", wheelRadius);
        AddWheelSphere(car, visual, "Tire_Right", wheelRadius);
        AddWheelSphere(car, visual, "Tire_Left (1)", wheelRadius);
        AddWheelSphere(car, visual, "Tire_Right (1)", wheelRadius);
    }

    // Wheels are real colliders so the car rests and tumbles on its wheels rather than on a box
    // edge. In normal upright driving the raycast suspension holds the chassis at exactly the
    // height where these just touch, so the two systems agree instead of fighting - see
    // PlayerMovement's suspension rest length, which is set to this same radius.
    static void AddWheelSphere(GameObject car, GameObject visual, string tireName, float radius)
    {
        var tire = visual.transform.Find(tireName);
        if (tire == null) return;

        var wheel = Undo.AddComponent<SphereCollider>(car);
        wheel.center = car.transform.InverseTransformPoint(tire.position);
        wheel.radius = radius;
    }

    // Measured from the art rather than assumed: the wheel's own renderer bounds give the real
    // radius, so probe rest height and collider size follow the model instead of a guess.
    static float ResolveWheelRadius(GameObject visual)
    {
        var tire = visual.transform.Find("Tire_Left");
        if (tire == null) return 0.3f;

        var renderer = tire.GetComponentInChildren<Renderer>();
        if (renderer == null) return 0.3f;

        // Half the wheel's vertical extent, in the car's own scale.
        return Mathf.Max(0.05f, renderer.bounds.extents.y);
    }

    // PlayerRobot.prefab's own child names/positions - "Tire_Left"/"Tire_Right" sit further
    // forward (+Z) than the "(1)" pair, so those are the front (steered) wheels. Also wires
    // "visual" itself (the tires' parent) - PlayerMovement rotates that whole transform for the
    // cosmetic lean-into-curves/slopes effect, never the Rigidbody.
    //
    // Probe origins are baked here as fixed local positions taken from where the wheels actually
    // are in the art, rather than read live from the tire transforms at runtime: those get
    // rotated every frame by the cosmetic steer/roll animation, which would make the suspension
    // origins wobble. Measuring them once, here, means the probes sit exactly under the wheels
    // without inheriting the animation.
    static void WireTires(GameObject car, GameObject visual)
    {
        var movement = car.GetComponent<PlayerMovement>();
        if (movement == null) return;

        var frontLeft = visual.transform.Find("Tire_Left");
        var frontRight = visual.transform.Find("Tire_Right");
        var rearLeft = visual.transform.Find("Tire_Left (1)");
        var rearRight = visual.transform.Find("Tire_Right (1)");

        var so = new SerializedObject(movement);
        so.FindProperty("visual").objectReferenceValue = visual.transform;
        so.FindProperty("frontLeftTire").objectReferenceValue = frontLeft;
        so.FindProperty("frontRightTire").objectReferenceValue = frontRight;
        so.FindProperty("rearLeftTire").objectReferenceValue = rearLeft;
        so.FindProperty("rearRightTire").objectReferenceValue = rearRight;

        // Order matches GroundProbes' contract: front-left, front-right, rear-left, rear-right.
        SetProbeOffset(so, car, 0, frontLeft);
        SetProbeOffset(so, car, 1, frontRight);
        SetProbeOffset(so, car, 2, rearLeft);
        SetProbeOffset(so, car, 3, rearRight);

        so.FindProperty("wheelRadius").floatValue = ResolveWheelRadius(visual);
        so.ApplyModifiedProperties();
    }

    static void SetProbeOffset(SerializedObject so, GameObject car, int index, Transform tire)
    {
        var array = so.FindProperty("probeOffsets");
        if (array.arraySize < 4) array.arraySize = 4;
        if (tire == null) return;

        array.GetArrayElementAtIndex(index).vector3Value = car.transform.InverseTransformPoint(tire.position);
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
