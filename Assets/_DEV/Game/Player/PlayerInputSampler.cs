using UnityEngine.InputSystem;

// Keyboard sampling for PlayerMovement's networked input, pulled out of the old
// DriveNetworkBootstrap (retired along with Drive.unity) so QuickMatchFusionService's
// RunnerCallbackRelay.OnInputAction can call it directly - same shape as the pattern already
// used for OnInputAction elsewhere in this project.
public static class PlayerInputSampler
{
    public static PlayerNetInput Sample()
    {
        var data = new PlayerNetInput();

        var keyboard = Keyboard.current;
        if (keyboard == null) return data;

        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) data.ThrottleAxis += 1f;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) data.ThrottleAxis -= 1f;

        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) data.SteerAxis -= 1f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) data.SteerAxis += 1f;

        data.Boost = keyboard.leftShiftKey.isPressed;
        data.Drift = keyboard.leftCtrlKey.isPressed;

        return data;
    }
}
