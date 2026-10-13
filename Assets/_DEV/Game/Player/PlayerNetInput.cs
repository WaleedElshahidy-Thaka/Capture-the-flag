using Fusion;

// One tick of driving input. Gathered in Fusion's OnInput callback (PlayerInputSampler) and
// read by PlayerMovement via GetInput, or produced by BotDriver for unowned cars.
public struct PlayerNetInput : INetworkInput
{
    public float ThrottleAxis; // -1..1, W/S or Up/Down
    public float SteerAxis;    // -1..1, A/D or Left/Right
    public NetworkBool Boost;  // Left Shift
    public NetworkBool Drift;  // Left Ctrl
}
