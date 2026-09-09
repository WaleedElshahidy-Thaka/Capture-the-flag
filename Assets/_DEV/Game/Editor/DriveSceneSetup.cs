using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Fusion;

// The one accepted scene for physics-based driving, tuned and tested locally before networking
// was added on top of PlayerMovement in place (see PlayerMovement.cs for the networked-vs-local
// state of the movement itself - this setup script only builds the static scene furniture).
public static class DriveSceneSetup
{
    const string ScenePath = "Assets/_DEV/Game/Scenes/Drive.unity";
    const string UndoLabel = "Setup Drive Scene";

    [MenuItem("Game/Setup Drive Scene")]
    public static void SetupScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[DriveSceneSetup] Exit Play Mode before running this.");
            return;
        }

        OpenOrCreateScene();
        BuildFloor();
        BuildWalls();
        var playerPrefab = BuildPlayerPrefab();
        BuildCar("OtherCar", new Vector3(0f, 1f, 6f), addMovement: false);
        BuildCamera();
        BuildNetworkBootstrap(playerPrefab);

        var activeScene = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);

        Debug.Log($"[DriveSceneSetup] Done. Open {ScenePath} and press Play - WASD/arrows drive immediately.");
    }

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

    // Same physical setup for the player and for anything else we want to test hitting -
    // addMovement is the only difference between a drivable car and a sitting-there one.
    static Rigidbody BuildCar(string name, Vector3 position, bool addMovement)
    {
        var existing = GameObject.Find(name);
        GameObject go = existing != null ? existing : new GameObject(name, typeof(Rigidbody), typeof(BoxCollider));

        var body = go.GetComponent<Rigidbody>();
        if (body == null) body = Undo.AddComponent<Rigidbody>(go);
        body.mass = 50f;
        body.linearDamping = 1.5f;
        body.angularDamping = 3f;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        var collider = go.GetComponent<BoxCollider>();
        if (collider == null) collider = Undo.AddComponent<BoxCollider>(go);
        collider.size = new Vector3(1.2f, 0.8f, 1.6f);
        collider.center = new Vector3(0f, 0.4f, 0f);

        if (addMovement && go.GetComponent<PlayerMovement>() == null) Undo.AddComponent<PlayerMovement>(go);

        // Only the driven car is networked - OtherCar stays a plain local Rigidbody, same as
        // before, since it's not a real second player yet.
        if (addMovement)
        {
            if (go.GetComponent<NetworkObject>() == null) Undo.AddComponent<NetworkObject>(go);

            var networkTransform = go.GetComponent<NetworkTransform>();
            if (networkTransform == null) networkTransform = Undo.AddComponent<NetworkTransform>(go);
            // Forecast Physics (extrapolates from the Rigidbody's velocity instead of
            // resimulating) - see doc excerpt discussed with the user, chosen over the
            // Physics Addon's full resimulation as the simpler first step for this small,
            // LAN-scale game. Requires NetworkProjectConfig.PhysicsForecast enabled globally too.
            networkTransform.PhysicsSettings.ForecastEnabled = true;
        }

        go.transform.position = position;

        // Always replaced by name, not "only if nothing renders yet" - a prior run may have
        // already added a Visual (e.g. the old FBX placeholder), and skipping in that case
        // would silently keep it forever instead of picking up the current art.
        var existingVisual = go.transform.Find("Visual");
        if (existingVisual != null) Object.DestroyImmediate(existingVisual.gameObject);

        var playerArt = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_DEV/Game/Art/Bolt/PlayerRobot.prefab");
        GameObject visual = playerArt != null
            ? (GameObject)PrefabUtility.InstantiatePrefab(playerArt)
            : GameObject.CreatePrimitive(PrimitiveType.Capsule);
        visual.name = "Visual";
        visual.transform.SetParent(go.transform, false);
        // The source prefab's root carries a leftover world-position offset from wherever
        // it was originally built - zero it out so it sits centered on the car.
        visual.transform.localPosition = playerArt != null ? Vector3.zero : new Vector3(0f, 0.4f, 0f);
        visual.transform.localRotation = Quaternion.identity;

        var visualCollider = visual.GetComponent<Collider>();
        if (visualCollider != null) Object.DestroyImmediate(visualCollider);

        Undo.RegisterCreatedObjectUndo(visual, UndoLabel);

        if (addMovement) WireTires(go, visual);

        if (existing == null) Undo.RegisterCreatedObjectUndo(go, UndoLabel);

        return body;
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

    const string PlayerPrefabPath = "Assets/_DEV/Game/Player/PlayerCar.prefab";

    // Shared Mode: each player spawns and drives their own car (see DriveNetworkBootstrap), so
    // Player can no longer be a single scene-placed instance - build it the same way as before,
    // save it as the prefab Runner.Spawn instantiates per join, then remove the scene instance.
    static NetworkObject BuildPlayerPrefab()
    {
        var body = BuildCar("Player", Vector3.zero, addMovement: true);
        var go = body.gameObject;

        PrefabUtility.SaveAsPrefabAsset(go, PlayerPrefabPath);
        var prefabObject = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath).GetComponent<NetworkObject>();

        Undo.DestroyObjectImmediate(go);
        return prefabObject;
    }

    // Shared Mode, no host/client (see DriveNetworkBootstrap) - proves the game plays the way
    // it was described: everyone just joins and drives their own car, no visible authority.
    static void BuildNetworkBootstrap(NetworkObject playerPrefab)
    {
        var go = GameObject.Find("NetworkBootstrap");
        if (go == null)
        {
            go = new GameObject("NetworkBootstrap", typeof(NetworkRunner), typeof(DriveNetworkBootstrap));
            Undo.RegisterCreatedObjectUndo(go, UndoLabel);
        }

        var bootstrap = go.GetComponent<DriveNetworkBootstrap>();
        var so = new SerializedObject(bootstrap);
        so.FindProperty("playerPrefab").objectReferenceValue = playerPrefab;
        so.ApplyModifiedProperties();
    }

    // No target wired here anymore - each player's car (and therefore the camera's target)
    // no longer exists at edit time, only once Runner.Spawn creates it. PlayerMovement.Spawned()
    // wires PlayerCamera.SetTarget at runtime instead, for its own local car only.
    static void BuildCamera()
    {
        var cameraGO = GameObject.Find("Main Camera");
        if (cameraGO == null)
        {
            Debug.LogWarning("[DriveSceneSetup] No 'Main Camera' found - PlayerCamera not wired.");
            return;
        }

        if (cameraGO.GetComponent<PlayerCamera>() == null) Undo.AddComponent<PlayerCamera>(cameraGO);
    }
}
