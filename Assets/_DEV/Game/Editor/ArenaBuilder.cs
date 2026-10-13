using UnityEditor;
using UnityEngine;

// Builds the placeholder arena from primitives: floor and walls sized from ArenaBounds, a ramp
// and a banked curve to test driving on, the lobby camera and the seat gizmos.
static class ArenaBuilder
{
    public static void Build()
    {
        var arena = SetupUtils.GetOrCreatePlain("ArenaBounds", null, typeof(ArenaBounds)).GetComponent<ArenaBounds>();
        BuildFloor(arena);
        BuildWalls(arena);
        BuildRamp();
        BuildCurve();
        PlaceCamera();
        BuildSeatMarkers();
    }

    // Floor and walls are rebuilt every run so inspector size changes on ArenaBounds apply.
    static void BuildFloor(ArenaBounds arena)
    {
        SetupUtils.DestroyIfExists("Floor");

        var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "Floor";
        floor.transform.position = Vector3.zero;
        float planeScale = arena.FloorSize / 10f; // the plane primitive is 10 units across
        floor.transform.localScale = new Vector3(planeScale, 1f, planeScale);
        Undo.RegisterCreatedObjectUndo(floor, SetupUtils.UndoLabel);
    }

    // Closed on all four sides (FD-06: no out-of-bounds, no gaps). No Rigidbody = static.
    static void BuildWalls(ArenaBounds arena)
    {
        SetupUtils.DestroyIfExists("Walls");

        var wallsParent = new GameObject("Walls");
        Undo.RegisterCreatedObjectUndo(wallsParent, SetupUtils.UndoLabel);

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

    static void BuildRamp()
    {
        if (GameObject.Find("Ramp") != null) return;

        var parent = new GameObject("Ramp");
        Undo.RegisterCreatedObjectUndo(parent, SetupUtils.UndoLabel);

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

    // A banked arc made of overlapping straight segments.
    static void BuildCurve()
    {
        if (GameObject.Find("Curve") != null) return;

        var parent = new GameObject("Curve");
        Undo.RegisterCreatedObjectUndo(parent, SetupUtils.UndoLabel);

        const int segmentCount = 14;
        const float radius = 11f;
        const float arcDegrees = 150f;
        const float trackWidth = 6f;
        const float bankAngle = 10f; // tilted toward the inside of the turn
        Vector3 center = new Vector3(-18f, 0f, -6f);

        float segAngle = arcDegrees / segmentCount;
        float segLength = 2f * radius * Mathf.Sin(segAngle * Mathf.Deg2Rad * 0.5f) * 1.1f; // 10% overlap, no gaps

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

    // PlayerCamera finds its target at runtime; at edit time it's parked on the lobby shot.
    static void PlaceCamera()
    {
        var cameraGO = GameObject.Find(SetupUtils.MainCameraName);
        if (cameraGO == null)
        {
            Debug.LogWarning("[GameSceneSetup] No 'Main Camera' found - PlayerCamera not wired.");
            return;
        }

        if (cameraGO.GetComponent<PlayerCamera>() == null) Undo.AddComponent<PlayerCamera>(cameraGO);
        cameraGO.transform.position = LobbyLayout.CameraPosition;
        cameraGO.transform.rotation = LobbyLayout.CameraRotation;
    }

    // Rebuilt from LobbyLayout every run so the gizmos always match the real seats.
    // "SeatAssigner" is a leftover from an old four-seat design.
    static void BuildSeatMarkers()
    {
        SetupUtils.DestroyIfExists("SeatAnchors");
        SetupUtils.DestroyIfExists("SeatAssigner");

        var parent = new GameObject("SeatAnchors");
        Undo.RegisterCreatedObjectUndo(parent, SetupUtils.UndoLabel);

        for (int slot = 0; slot < LobbyLayout.SlotCount; slot++)
        {
            var marker = new GameObject($"Seat_{slot}", typeof(LobbySeatMarker));
            marker.transform.SetParent(parent.transform, false);
            marker.transform.position = LobbyLayout.SlotPosition(slot);
            marker.transform.rotation = LobbyLayout.SlotRotation;
            marker.GetComponent<LobbySeatMarker>().SetSlot(slot);
        }
    }
}
