using UnityEngine;

// The lobby picture every player sees: six seats in a staggered row facing the camera, and the
// camera positions for the row shot and the solo shot. Read by both PlayerLobbySpawner and
// PlayerCamera so they can't disagree. Placeholder until the real arena has authored anchors.
public static class LobbyLayout
{
    public const float SlotSpacing = 2.5f;   // metres between seats along X
    public const float SlotStagger = 2.5f;   // how far odd seats step back in Z
    public const float SpawnHeight = 1f;

    const float CameraDistance = 9f;
    const float CameraHeight = 6f;
    const float CameraYawDegrees = 25f;      // slightly off-axis so the row reads as depth
    const float SoloCameraDistance = 5f;
    const float SoloCameraHeight = 3.2f;

    public static int SlotCount => MatchmakingConfig.MaxPlayers;

    public static Vector3 SlotPosition(int slot)
    {
        slot = Mathf.Clamp(slot, 0, SlotCount - 1);
        return new Vector3(slot * SlotSpacing, SpawnHeight, (slot % 2) * SlotStagger);
    }

    // Facing -Z, toward the camera.
    public static Quaternion SlotRotation => Quaternion.Euler(0f, 180f, 0f);

    public static Vector3 RowCenter => new Vector3((SlotCount - 1) * 0.5f * SlotSpacing, 0f, SlotStagger * 0.5f);

    public static Vector3 CameraPosition => OrbitPosition(RowCenter, CameraDistance, CameraHeight);

    public static Quaternion CameraRotation => LookAt(RowCenter, CameraPosition);

    // Same angle as the row shot, pulled in on one robot, so the fade reads as "the others appeared".
    public static Vector3 SoloCameraPosition(Vector3 focus) => OrbitPosition(focus, SoloCameraDistance, SoloCameraHeight);

    public static Quaternion SoloCameraRotation(Vector3 focus) => LookAt(focus, SoloCameraPosition(focus));

    static Vector3 OrbitPosition(Vector3 focus, float distance, float height)
    {
        Vector3 back = Quaternion.Euler(0f, CameraYawDegrees, 0f) * Vector3.back * distance;
        return focus + back + Vector3.up * height;
    }

    static Quaternion LookAt(Vector3 focus, Vector3 from) =>
        Quaternion.LookRotation(focus + Vector3.up * 0.5f - from, Vector3.up);
}
