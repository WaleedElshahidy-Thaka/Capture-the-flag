using Fusion;
using Fusion.Addons.Physics;
using UnityEditor;
using UnityEngine;

// Builds Resources/PlayerCar.prefab: Rigidbody + PlayerMovement + PlayerMatchState +
// NetworkRigidbody on the root, colliders measured from the robot art, and a "View" child
// (NetworkRigidbody's interpolation target) holding the art and the name tag.
static class PlayerCarPrefabBuilder
{
    // Any Resources/ folder works for Resources.Load; this one keeps the prefab next to its scripts.
    const string PrefabPath = "Assets/_DEV/Game/Player/Resources/PlayerCar.prefab";
    const float DefaultWheelRadius = 0.3f;

    // PlayerRobot.prefab child names. The un-suffixed pair sits further forward: the steered wheels.
    const string FrontLeftTire = "Tire_Left";
    const string FrontRightTire = "Tire_Right";
    const string RearLeftTire = "Tire_Left (1)";
    const string RearRightTire = "Tire_Right (1)";

    public static void Build()
    {
        var existing = GameObject.Find("PlayerCarBuildTemp");
        GameObject car = existing != null ? existing : new GameObject("PlayerCarBuildTemp", typeof(Rigidbody));

        ConfigureRigidbody(car.GetComponent<Rigidbody>());

        if (car.GetComponent<PlayerMovement>() == null) Undo.AddComponent<PlayerMovement>(car);
        if (car.GetComponent<PlayerMatchState>() == null) Undo.AddComponent<PlayerMatchState>(car);
        if (car.GetComponent<NetworkObject>() == null) Undo.AddComponent<NetworkObject>(car);
        var networkRigidbody = AddNetworkRigidbody(car);

        RemoveChild(car, "View");
        RemoveChild(car, "Visual");
        RemoveChild(car, "NameTag");

        var view = new GameObject("View");
        view.transform.SetParent(car.transform, false);
        var nrbSo = new SerializedObject(networkRigidbody);
        nrbSo.FindProperty("_interpolationTarget").objectReferenceValue = view.transform;
        nrbSo.ApplyModifiedProperties();

        var visual = SetupUtils.InstantiateRobotArt(view.transform, out bool isPlaceholder);
        visual.transform.localPosition = isPlaceholder ? new Vector3(0f, 0.4f, 0f) : Vector3.zero;
        var visualCollider = visual.GetComponent<Collider>();
        if (visualCollider != null) Object.DestroyImmediate(visualCollider);

        WireTires(car, visual);
        BuildColliders(car, visual);
        var nameTag = BuildNameTag(car, view.transform);
        WireLobbyVisibility(car, visual, nameTag);

        PrefabUtility.SaveAsPrefabAsset(car, PrefabPath);
        Object.DestroyImmediate(car);
    }

    // The drive model writes velocity itself (see ChassisPhysicsSetup, which enforces the same at runtime).
    // Interpolation None: NetworkRigidbody interpolates the View child instead.
    static void ConfigureRigidbody(Rigidbody body)
    {
        body.mass = 50f;
        body.linearDamping = 0f;
        body.angularDamping = 0f;
        body.constraints = RigidbodyConstraints.None;
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.None;
    }

    // NetworkRigidbody (not NetworkTransform): it networks velocity too and restores it before
    // a resimulation, which the drive model's rollback contract needs.
    static NetworkRigidbody AddNetworkRigidbody(GameObject car)
    {
        var legacyTransform = car.GetComponent<NetworkTransform>();
        if (legacyTransform != null) Object.DestroyImmediate(legacyTransform);

        var networkRigidbody = car.GetComponent<NetworkRigidbody>();
        return networkRigidbody != null ? networkRigidbody : Undo.AddComponent<NetworkRigidbody>(car);
    }

    static void RemoveChild(GameObject parent, string name)
    {
        var child = parent.transform.Find(name);
        if (child != null) Object.DestroyImmediate(child.gameObject);
    }

    // Shown only once matched (PlayerLobbyVisibility toggles these two).
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

    // Under View (follows the interpolated car) but beside Visual (so body lean doesn't tilt it).
    static GameObject BuildNameTag(GameObject car, Transform view)
    {
        var tagGO = SetupUI.WorldNameTag(view, out var nameLabel);
        var tag = tagGO.AddComponent<PlayerNameTag>();

        var readyLabel = SetupUI.WorldLabel("Ready", tagGO.transform, "READY", 30, new Color(0.3f, 0.9f, 0.4f), new Vector2(0f, -30f), 40f);
        readyLabel.gameObject.SetActive(false);

        SetupUtils.SetField(tag, "nameLabel", nameLabel);
        SetupUtils.SetField(tag, "readyLabel", readyLabel);
        SetupUtils.SetField(tag, "matchState", car.GetComponent<PlayerMatchState>());
        return tagGO;
    }

    // A raised body box plus a sphere per wheel, so the car rests and tumbles on its wheels.
    // The box sits well above ride height: if it touches the floor, PhysX overrides the suspension.
    static void BuildColliders(GameObject car, GameObject visual)
    {
        foreach (var existingCollider in car.GetComponents<Collider>())
            Object.DestroyImmediate(existingCollider);

        float wheelRadius = MeasureWheelRadius(visual);

        var chassisBody = Undo.AddComponent<BoxCollider>(car);
        chassisBody.size = new Vector3(0.9f, 0.7f, 1.1f); // WallSensor's half extents match this
        chassisBody.center = new Vector3(0f, wheelRadius * 2f + 0.15f, 0f);

        AddWheelSphere(car, visual, FrontLeftTire, wheelRadius);
        AddWheelSphere(car, visual, FrontRightTire, wheelRadius);
        AddWheelSphere(car, visual, RearLeftTire, wheelRadius);
        AddWheelSphere(car, visual, RearRightTire, wheelRadius);
    }

    static void AddWheelSphere(GameObject car, GameObject visual, string tireName, float radius)
    {
        var tire = visual.transform.Find(tireName);
        if (tire == null) return;

        var wheel = Undo.AddComponent<SphereCollider>(car);
        wheel.center = car.transform.InverseTransformPoint(tire.position);
        wheel.radius = radius;
    }

    static float MeasureWheelRadius(GameObject visual)
    {
        var tire = visual.transform.Find(FrontLeftTire);
        if (tire == null) return DefaultWheelRadius;

        var renderer = tire.GetComponentInChildren<Renderer>();
        if (renderer == null) return DefaultWheelRadius;

        return Mathf.Max(0.05f, renderer.bounds.extents.y);
    }

    // Wires the tire transforms and bakes the suspension probe offsets from the wheels' rest
    // positions (not read live: the tires are animated every frame).
    static void WireTires(GameObject car, GameObject visual)
    {
        var movement = car.GetComponent<PlayerMovement>();
        if (movement == null) return;

        var frontLeft = visual.transform.Find(FrontLeftTire);
        var frontRight = visual.transform.Find(FrontRightTire);
        var rearLeft = visual.transform.Find(RearLeftTire);
        var rearRight = visual.transform.Find(RearRightTire);

        var so = new SerializedObject(movement);
        so.FindProperty("visual").objectReferenceValue = visual.transform;
        so.FindProperty("frontLeftTire").objectReferenceValue = frontLeft;
        so.FindProperty("frontRightTire").objectReferenceValue = frontRight;
        so.FindProperty("rearLeftTire").objectReferenceValue = rearLeft;
        so.FindProperty("rearRightTire").objectReferenceValue = rearRight;

        // Order matches GroundProbes: front-left, front-right, rear-left, rear-right.
        SetProbeOffset(so, car, 0, frontLeft);
        SetProbeOffset(so, car, 1, frontRight);
        SetProbeOffset(so, car, 2, rearLeft);
        SetProbeOffset(so, car, 3, rearRight);

        so.FindProperty("wheelRadius").floatValue = MeasureWheelRadius(visual);
        so.ApplyModifiedProperties();
    }

    static void SetProbeOffset(SerializedObject so, GameObject car, int index, Transform tire)
    {
        var array = so.FindProperty("probeOffsets");
        if (array.arraySize < 4) array.arraySize = 4;
        if (tire == null) return;

        array.GetArrayElementAtIndex(index).vector3Value = car.transform.InverseTransformPoint(tire.position);
    }
}
