using UnityEngine;

// The lobby is one fixed picture that every player sees identically: the six seats in a
// staggered row (slot 0 at X=0/Z=0, slot 1 at X=1/Z=1, slot 2 at X=2/Z=0 ... scaled by
// SlotSpacing), cars facing the camera, and one isometric camera parked at the row's centre
// X. Both the spawner (where a car goes) and PlayerCamera (where the lobby view sits) read
// from here, so the two can't drift apart.
//
// Placeholder geometry until the art team's arena arrives with authored anchors - at that
// point this becomes a wrapper around those.
public static class LobbyLayout
{
    // Metres between neighbouring seats along X, and how far the odd seats step back in Z.
    public const float SlotSpacing = 2.5f;
    public const float SlotStagger = 2.5f;
    public const float SpawnHeight = 1f;

    // Isometric lobby camera: centred on the row, pulled back toward -Z and up, angled down.
    const float CameraDistance = 9f;
    const float CameraHeight = 6f;
    const float CameraYawDegrees = 25f; // a little off-axis so the row reads as depth, not a line

    public static int SlotCount => MatchmakingConfig.MaxPlayers;

    public static Vector3 SlotPosition(int slot)
    {
        slot = Mathf.Clamp(slot, 0, SlotCount - 1);
        return new Vector3(slot * SlotSpacing, SpawnHeight, (slot % 2) * SlotStagger);
    }

    // Cars face -Z, toward the camera, so the lobby shows their fronts.
    public static Quaternion SlotRotation => Quaternion.Euler(0f, 180f, 0f);

    // Middle of the row: X = 2.5 * spacing for six seats (the "camera at 2.5X" rule),
    // Z halfway between the two stagger lines.
    public static Vector3 RowCenter => new Vector3((SlotCount - 1) * 0.5f * SlotSpacing, 0f, SlotStagger * 0.5f);

    public static Vector3 CameraPosition
    {
        get
        {
            Vector3 back = Quaternion.Euler(0f, CameraYawDegrees, 0f) * Vector3.back * CameraDistance;
            return RowCenter + back + Vector3.up * CameraHeight;
        }
    }

    public static Quaternion CameraRotation => Quaternion.LookRotation(RowCenter + Vector3.up * 0.5f - CameraPosition, Vector3.up);

    // The solo shot (before a match is found): the same yaw and the same feel as the row shot,
    // pulled in on one robot instead of six, so the fade to the lobby reads as "the others
    // appeared", not as a different camera.
    const float SoloCameraDistance = 5f;
    const float SoloCameraHeight = 3.2f;

    public static Vector3 SoloCameraPosition(Vector3 focus)
    {
        Vector3 back = Quaternion.Euler(0f, CameraYawDegrees, 0f) * Vector3.back * SoloCameraDistance;
        return focus + back + Vector3.up * SoloCameraHeight;
    }

    public static Quaternion SoloCameraRotation(Vector3 focus) =>
        Quaternion.LookRotation(focus + Vector3.up * 0.5f - SoloCameraPosition(focus), Vector3.up);
}
