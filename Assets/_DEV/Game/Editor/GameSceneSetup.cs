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

    static readonly Color PanelColor = new Color(0.08f, 0.09f, 0.12f, 0.78f);
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
        BuildSeatMarkers();

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

    // Terrain to actually test driving physics against, not just a flat plate - the lobby seat
    // row (see LobbyLayout) sits near the arena center, so both features sit well clear of it,
    // out toward the +Z and -X edges of the arena.
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
        // Parked on the lobby shot at edit time too, so the scene view previews what players see.
        cameraGO.transform.position = LobbyLayout.CameraPosition;
        cameraGO.transform.rotation = LobbyLayout.CameraRotation;
    }

    // Visible markers for the six lobby seats, rebuilt from LobbyLayout every run (destroy and
    // recreate, not find-or-keep) so the scene always shows the layout PlayerLobbySpawner is
    // actually using. Also clears the leftovers of the earlier four-seat design - "SeatAnchors"
    // with CenterAnchor/OtherAnchor1-3 and a "SeatAssigner" whose LobbySeatAssigner script was
    // deleted in commit 0d5bbc3 - which survived in Game.unity as dead objects because this tool
    // only ever found-or-created by name and never removed anything.
    static void BuildSeatMarkers()
    {
        foreach (var legacyName in new[] { "SeatAnchors", "SeatAssigner" })
        {
            var legacy = GameObject.Find(legacyName);
            if (legacy != null) UnityEngine.Object.DestroyImmediate(legacy);
        }

        var parent = new GameObject("SeatAnchors");
        Undo.RegisterCreatedObjectUndo(parent, UndoLabel);

        for (int slot = 0; slot < LobbyLayout.SlotCount; slot++)
        {
            var marker = new GameObject($"Seat_{slot}", typeof(LobbySeatMarker));
            marker.transform.SetParent(parent.transform, false);
            marker.transform.position = LobbyLayout.SlotPosition(slot);
            marker.transform.rotation = LobbyLayout.SlotRotation;
            marker.GetComponent<LobbySeatMarker>().SetSlot(slot);
        }
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
        // No engine damping - the model owns drag (CoastDrag / AirDrag). PhysX damping capped
        // the car at ~9 m/s regardless of TopSpeed; see PlayerMovement.Awake.
        body.linearDamping = 0f;
        body.angularDamping = 0f;
        // No rotation constraints. PlayerMovement.ResolveAttitude actively levels the chassis
        // (writes X/Z angular velocity every tick to conform to slopes and come back upright) -
        // freezing X/Z here made PhysX cancel that every step, which was the rotation jitter
        // seen in the inspector, and stopped the car conforming to the ramp and banked curve.
        body.constraints = RigidbodyConstraints.None;
        // The drive model applies gravity itself (HandlingValues.GravityScale) - PhysX adding its
        // own was a second, unmodelled force that slid the frictionless chassis down slopes.
        body.useGravity = false;
        // None, not Interpolate: physics is stepped from Fusion's tick (RunnerSimulatePhysics),
        // not Unity's FixedUpdate, and NetworkRigidbody interpolates the View child for
        // rendering. Rigidbody interpolation on top would be a second writer fighting it.
        body.interpolation = RigidbodyInterpolation.None;

        if (go.GetComponent<PlayerMovement>() == null) Undo.AddComponent<PlayerMovement>(go);
        if (go.GetComponent<PlayerMatchState>() == null) Undo.AddComponent<PlayerMatchState>(go);
        if (go.GetComponent<NetworkObject>() == null) Undo.AddComponent<NetworkObject>(go);

        // NetworkRigidbody (Fusion Physics addon), not NetworkTransform. NetworkTransform only
        // knows the transform: on a rollback it teleported the chassis but left PhysX holding
        // the *predicted* velocity, so every resimulation ran from a mismatched state and the
        // client's car snapped between where it predicted and where the host said. Its render
        // interpolation also wrote the root transform every frame, which Unity pushes into
        // PhysX - the small wobble seen on the host. NetworkRigidbody networks position,
        // rotation AND velocity, restores all of them before a resimulation (doc 03's
        // "restore then re-simulate" contract), and interpolates a render-only child instead
        // of the physics root.
        var legacyTransform = go.GetComponent<NetworkTransform>();
        if (legacyTransform != null) UnityEngine.Object.DestroyImmediate(legacyTransform);
        var networkRigidbody = go.GetComponent<Fusion.Addons.Physics.NetworkRigidbody>();
        if (networkRigidbody == null) networkRigidbody = Undo.AddComponent<Fusion.Addons.Physics.NetworkRigidbody>(go);

        // View: the interpolation target. Everything cosmetic lives under it (the robot art, the
        // name tag); the colliders stay on the root with the Rigidbody. NetworkRigidbody moves
        // this child between ticks for rendering and recentres it before each simulation step.
        var existingView = go.transform.Find("View");
        if (existingView != null) UnityEngine.Object.DestroyImmediate(existingView.gameObject);
        var existingVisual = go.transform.Find("Visual");
        if (existingVisual != null) UnityEngine.Object.DestroyImmediate(existingVisual.gameObject);
        var existingTag = go.transform.Find("NameTag");
        if (existingTag != null) UnityEngine.Object.DestroyImmediate(existingTag.gameObject);

        var view = new GameObject("View");
        view.transform.SetParent(go.transform, false);
        var nrbSo = new SerializedObject(networkRigidbody);
        nrbSo.FindProperty("_interpolationTarget").objectReferenceValue = view.transform;
        nrbSo.ApplyModifiedProperties();

        var playerArt = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_DEV/Game/Art/Bolt/PlayerRobot.prefab");
        GameObject visual = playerArt != null
            ? (GameObject)PrefabUtility.InstantiatePrefab(playerArt)
            : GameObject.CreatePrimitive(PrimitiveType.Capsule);
        visual.name = "Visual";
        visual.transform.SetParent(view.transform, false);
        visual.transform.localPosition = playerArt != null ? Vector3.zero : new Vector3(0f, 0.4f, 0f);
        visual.transform.localRotation = Quaternion.identity;

        var visualCollider = visual.GetComponent<Collider>();
        if (visualCollider != null) UnityEngine.Object.DestroyImmediate(visualCollider);

        WireTires(go, visual);
        BuildColliders(go, visual);
        var nameTag = BuildNameTag(go, view.transform);
        WireLobbyVisibility(go, visual, nameTag);

        PrefabUtility.SaveAsPrefabAsset(go, PlayerCarPrefabPath);
        UnityEngine.Object.DestroyImmediate(go);
    }

    // Other players' robots are hidden until you're both searching (see PlayerLobbyVisibility)
    // - the cosmetic children are what it toggles.
    static void WireLobbyVisibility(GameObject car, GameObject visual, GameObject nameTag)
    {
        var visibility = car.GetComponent<PlayerLobbyVisibility>();
        if (visibility == null) visibility = Undo.AddComponent<PlayerLobbyVisibility>(car);

        var so = new SerializedObject(visibility);
        var array = so.FindProperty("hiddenUntilMatched");
        array.arraySize = 2;
        array.GetArrayElementAtIndex(0).objectReferenceValue = visual;
        array.GetArrayElementAtIndex(1).objectReferenceValue = nameTag;
        so.ApplyModifiedProperties();
    }

    // A small world-space canvas above the chassis showing the player's name (PlayerNameTag reads
    // PlayerMatchState.DisplayName and billboards it). Under View (so it follows the
    // interpolated car, not the tick-stepped physics root), but a sibling of Visual, so the
    // cosmetic body lean doesn't tilt the text.
    static GameObject BuildNameTag(GameObject car, Transform view)
    {
        var tagGO = new GameObject("NameTag", typeof(RectTransform), typeof(Canvas), typeof(PlayerNameTag));
        tagGO.transform.SetParent(view, false);
        tagGO.transform.localPosition = new Vector3(0f, 1.6f, 0f);
        tagGO.transform.localScale = Vector3.one * 0.01f;

        var canvas = tagGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        var canvasRt = tagGO.GetComponent<RectTransform>();
        canvasRt.sizeDelta = new Vector2(300f, 110f);

        // Name on top, READY line beneath it (hidden until the player readies up).
        var nameLabel = WorldLabel("Name", tagGO.transform, "Player", 36, Color.white, new Vector2(0f, 20f), 60f);
        var readyLabel = WorldLabel("Ready", tagGO.transform, "READY", 30, new Color(0.3f, 0.9f, 0.4f), new Vector2(0f, -30f), 40f);
        readyLabel.gameObject.SetActive(false);

        var tag = tagGO.GetComponent<PlayerNameTag>();
        SetField(tag, "nameLabel", nameLabel);
        SetField(tag, "readyLabel", readyLabel);
        SetField(tag, "matchState", car.GetComponent<PlayerMatchState>());
        return tagGO;
    }

    static TextMeshProUGUI WorldLabel(string name, Transform parent, string text, float fontSize, Color color, Vector2 anchoredPosition, float height)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(1f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(0f, height);
        rt.anchoredPosition = anchoredPosition;

        var label = go.GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.color = color;
        return label;
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
        public GameObject loadingPanel, idlePanel, searchingPanel, foundPanel, soloBotPanel, readyPanel, reconnectingPanel, resumingPanel;
        public Button quickMatchButton, startWithBotsButton, readyToggleButton, cancelButton;
        public TMP_Text loadingText, searchingText, playersFoundText, foundText, readyToggleLabel, resumingText;
    }

    // A full-screen black Loading panel, then compact translucent side panels down the left
    // edge - never an overlay over the arena, so the robots stay in view. Each phase's panel is
    // its own small box; Cancel sits in a separate box below them. Two more full-screen panels
    // (translucent, the arena still visible behind) cover a host migration.
    static void BuildScreen(RectTransform canvas, out ScreenRefs refs)
    {
        refs = new ScreenRefs();

        var root = GetOrCreateUI("MatchmakingScreen", canvas, typeof(MatchmakingScreen));
        Stretch(root);
        RemoveLegacyChildren(root);

        var loading = FullScreenPanel("LoadingPanel", root, Color.black, "Loading... 0s", out refs.loadingText);
        refs.loadingPanel = loading.gameObject;

        var idle = SidePanel("IdlePanel", root);
        refs.quickMatchButton = Btn("QuickMatchButton", idle, "Quick Match");
        refs.idlePanel = idle.gameObject;

        var searching = SidePanel("SearchingPanel", root);
        refs.searchingText = TextEl("SearchingText", searching, "Searching for players... 0s", 20, 36);
        refs.playersFoundText = TextEl("PlayersFoundText", searching, "2 / 6 players found", 18, 32);
        var soloBot = GetOrCreateUI("SoloBotPanel", searching, typeof(VerticalLayoutGroup));
        refs.startWithBotsButton = Btn("StartWithBotsButton", soloBot, "Start with computer players", 44, 18);
        refs.soloBotPanel = soloBot.gameObject;
        var ready = GetOrCreateUI("ReadyPanel", searching, typeof(VerticalLayoutGroup));
        refs.readyToggleButton = Btn("ReadyToggleButton", ready, "Play with computer players", 44, 18);
        refs.readyToggleLabel = refs.readyToggleButton.GetComponentInChildren<TMP_Text>();
        refs.readyPanel = ready.gameObject;
        refs.searchingPanel = searching.gameObject;

        var found = SidePanel("FoundPanel", root);
        refs.foundText = TextEl("FoundText", found, "Player found! Joining lobby in 3", 22, 36);
        refs.foundPanel = found.gameObject;

        // Cancel lives at the root, anchored below the phase panels.
        var cancelBox = SidePanel("CancelPanel", root, anchoredY: -120f);
        refs.cancelButton = Btn("CancelButton", cancelBox, "Cancel", 40);
        cancelBox.GetComponent<Image>().enabled = false;

        // Host migration overlays: dimmed, not black - the frozen cars stay in view behind them.
        var dim = new Color(0f, 0f, 0f, 0.6f);
        var reconnecting = FullScreenPanel("ReconnectingPanel", root, dim, "Host disconnected - reconnecting...", out _);
        refs.reconnectingPanel = reconnecting.gameObject;
        var resuming = FullScreenPanel("ResumingPanel", root, dim, "Game resumes in 3", out refs.resumingText);
        refs.resumingPanel = resuming.gameObject;

        // The full-screen panels draw over the side panels; Loading over everything.
        reconnecting.SetAsLastSibling();
        resuming.SetAsLastSibling();
        loading.SetAsLastSibling();
    }

    static RectTransform FullScreenPanel(string name, Transform parent, Color color, string text, out TMP_Text label)
    {
        var panel = GetOrCreateUI(name, parent, typeof(Image));
        Stretch(panel);
        panel.GetComponent<Image>().color = color;

        label = TextEl("Text", panel, text, 28, 48);
        var rt = label.rectTransform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(700f, 48f);
        rt.anchoredPosition = Vector2.zero;
        return panel;
    }

    // Objects from earlier UI layouts that would otherwise survive re-runs as dead objects
    // (GetOrCreateUI only finds-or-creates by name) - removed explicitly before rebuilding.
    static void RemoveLegacyChildren(Transform root)
    {
        foreach (Transform panel in root)
        {
            var content = panel.Find("Content");
            if (content != null) UnityEngine.Object.DestroyImmediate(content.gameObject);
        }

        foreach (var path in new[] { "SearchingPanel/CancelButton", "IdlePanel/LoadingText", "IdlePanel/FindMatchButton", "LobbyPanel", "StartingPanel", "LoadingPanel/LoadingText" })
        {
            var legacy = root.Find(path);
            if (legacy != null) UnityEngine.Object.DestroyImmediate(legacy.gameObject);
        }
    }

    static void WireScreen(RectTransform canvas, ScreenRefs refs, MatchmakingFlowController flowController)
    {
        var screen = canvas.Find("MatchmakingScreen").GetComponent<MatchmakingScreen>();

        SetField(screen, "flowController", flowController);
        SetField(screen, "loadingPanel", refs.loadingPanel);
        SetField(screen, "loadingText", refs.loadingText);
        SetField(screen, "idlePanel", refs.idlePanel);
        SetField(screen, "quickMatchButton", refs.quickMatchButton);
        SetField(screen, "searchingPanel", refs.searchingPanel);
        SetField(screen, "searchingText", refs.searchingText);
        SetField(screen, "playersFoundText", refs.playersFoundText);
        SetField(screen, "foundPanel", refs.foundPanel);
        SetField(screen, "foundText", refs.foundText);
        SetField(screen, "soloBotPanel", refs.soloBotPanel);
        SetField(screen, "startWithBotsButton", refs.startWithBotsButton);
        SetField(screen, "readyPanel", refs.readyPanel);
        SetField(screen, "readyToggleButton", refs.readyToggleButton);
        SetField(screen, "readyToggleLabel", refs.readyToggleLabel);
        SetField(screen, "cancelButton", refs.cancelButton);
        SetField(screen, "reconnectingPanel", refs.reconnectingPanel);
        SetField(screen, "resumingPanel", refs.resumingPanel);
        SetField(screen, "resumingText", refs.resumingText);
    }

    // ─── Low-level UI builders ───────────────────────────────────────────

    const float SidePanelWidth = 340f;
    const float SidePanelMargin = 24f;

    // A small translucent box anchored to the left edge, vertically centred (offset by
    // anchoredY), sized to its content. Its VerticalLayoutGroup lays the controls out directly -
    // no nested Content object.
    static RectTransform SidePanel(string name, Transform parent, float anchoredY = 0f)
    {
        var rt = GetOrCreateUI(name, parent, typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.sizeDelta = new Vector2(SidePanelWidth, 0f);
        rt.anchoredPosition = new Vector2(SidePanelMargin, anchoredY);

        var img = rt.GetComponent<Image>();
        img.color = PanelColor;
        img.enabled = true;

        var vlg = rt.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 10f;
        vlg.padding = new RectOffset(14, 14, 12, 12);
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

    static Button Btn(string name, Transform parent, string label, float height = 44, float fontSize = 20)
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
        tmp.fontSize = fontSize;
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
        if (existing != null)
        {
            // An object from an earlier build may predate a component this version wants on it
            // (e.g. the side panels gained a VerticalLayoutGroup) - add what's missing rather
            // than returning something the caller will then GetComponent-null on.
            foreach (var type in components)
                if (existing.GetComponent(type) == null) Undo.AddComponent(existing.gameObject, type);
            return existing.GetComponent<RectTransform>();
        }

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
