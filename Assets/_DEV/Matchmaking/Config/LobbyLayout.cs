using UnityEngine;

// Where the seats are and where the lobby camera looks. Read by PlayerLobbySpawner, BotFill and
// PlayerCamera so they can't disagree.
//
// Seats come from the scene's StartAnchors (authored by the level designer, FD-06). Without one, or
// for a seat with no anchor, the old placeholder row is used, so the project keeps working before
// the arena art is in.
public static class LobbyLayout
{
    // Placeholder row (the original layout). Only used as a fallback.
    public const float SlotSpacing = 2.5f;   // metres between seats along X
    public const float SlotStagger = 2.5f;   // how far odd seats step back in Z
    public const float SpawnHeight = 1f;

    const float CameraDistance = 9f;
    const float CameraHeight = 6f;
    const float CameraYawDegrees = 25f;      // slightly off-axis so the row reads as depth
    const float SoloCameraDistance = 5f;
    const float SoloCameraHeight = 3.2f;

    public static int SlotCount => MatchmakingConfig.MaxPlayers;

    // ─── Seats ───────────────────────────────────────────────────────────

    public static Vector3 SlotPosition(int slot)
    {
        slot = Mathf.Clamp(slot, 0, SlotCount - 1);
        StartAnchors anchors = Anchors;
        Transform anchor = anchors != null ? anchors.Anchor(slot) : null;

        if (anchor != null)
            return anchor.position + Vector3.up * anchors.SpawnHeight;

        return new Vector3(slot * SlotSpacing, SpawnHeight, (slot % 2) * SlotStagger);
    }

    public static Quaternion SlotRotation(int slot)
    {
        slot = Mathf.Clamp(slot, 0, SlotCount - 1);
        StartAnchors anchors = Anchors;
        Transform anchor = anchors != null ? anchors.Anchor(slot) : null;

        // Yaw only: a tilted anchor must never spawn a tilted car.
        if (anchor != null)
            return Quaternion.Euler(0f, anchor.eulerAngles.y, 0f);

        return Quaternion.Euler(0f, 180f, 0f); // placeholder row faces -Z, toward the camera
    }

    // Middle of all the seats.
    public static Vector3 RowCenter
    {
        get
        {
            Vector3 sum = Vector3.zero;
            for (int slot = 0; slot < SlotCount; slot++) sum += SlotPosition(slot);
            Vector3 centre = sum / SlotCount;
            StartAnchors anchors = Anchors;
            centre.y -= anchors != null ? anchors.SpawnHeight : SpawnHeight; // back down to the floor
            return centre;
        }
    }

    // ─── Lobby camera ────────────────────────────────────────────────────

    public static Vector3 CameraPosition
    {
        get
        {
            StartAnchors anchors = Anchors;
            if (anchors != null && anchors.LobbyCamera != null) return anchors.LobbyCamera.position;
            return OrbitPosition(RowCenter, SlotRotation(0), CameraDistance, CameraHeight);
        }
    }

    public static Quaternion CameraRotation
    {
        get
        {
            StartAnchors anchors = Anchors;
            if (anchors != null && anchors.LobbyCamera != null) return anchors.LobbyCamera.rotation;
            return LookAt(RowCenter, CameraPosition);
        }
    }

    // Same angle as the row shot, pulled in on one robot, in front of the way it faces.
    public static Vector3 SoloCameraPosition(Vector3 focus) =>
        SoloCameraPosition(focus, SlotRotation(0));

    public static Vector3 SoloCameraPosition(Vector3 focus, Quaternion facing) =>
        OrbitPosition(focus, facing, SoloCameraDistance, SoloCameraHeight);

    public static Quaternion SoloCameraRotation(Vector3 focus) =>
        SoloCameraRotation(focus, SlotRotation(0));

    public static Quaternion SoloCameraRotation(Vector3 focus, Quaternion facing) =>
        LookAt(focus, SoloCameraPosition(focus, facing));

    // In front of the car (along its facing), turned a little off-axis. With the placeholder row
    // (facing -Z) this gives exactly the original camera position.
    static Vector3 OrbitPosition(Vector3 focus, Quaternion facing, float distance, float height)
    {
        Vector3 forward = facing * Vector3.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.back;
        Vector3 front = Quaternion.Euler(0f, CameraYawDegrees, 0f) * forward.normalized * distance;
        return focus + front + Vector3.up * height;
    }

    static Quaternion LookAt(Vector3 focus, Vector3 from) =>
        Quaternion.LookRotation(focus + Vector3.up * 0.5f - from, Vector3.up);

    // ─── Anchors lookup ──────────────────────────────────────────────────

    static StartAnchors cached;

    // At runtime StartAnchors registers itself in Awake. In the editor (scene builders) nothing has
    // run Awake yet, so search the scene once and cache it.
    static StartAnchors Anchors
    {
        get
        {
            if (StartAnchors.Instance != null) return StartAnchors.Instance;
            if (cached == null) cached = Object.FindFirstObjectByType<StartAnchors>();
            return cached;
        }
    }
}
