using UnityEngine;

// Billboarding for world-space labels.
public static class CameraFacing
{
    // Turns `label` to face the main camera, rotating about world up only so it never tilts.
    public static void FaceUpright(Transform label)
    {
        var cam = Camera.main;
        if (cam == null) return;

        Vector3 toCamera = label.position - cam.transform.position;
        toCamera.y = 0f;
        if (toCamera.sqrMagnitude > 0.0001f)
            label.rotation = Quaternion.LookRotation(toCamera, Vector3.up);
    }
}
