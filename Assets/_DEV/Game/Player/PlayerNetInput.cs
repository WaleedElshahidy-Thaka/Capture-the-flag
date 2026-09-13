using Fusion;

// Sampled once per network tick by PlayerInputSampler.Sample(), called from an OnInput callback
// (Fusion requires input to be gathered there, not read directly in a NetworkBehaviour's
// Update/FixedUpdateNetwork) and consumed by PlayerMovement.FixedUpdateNetwork via
// GetInput<PlayerNetInput>.
public struct PlayerNetInput : INetworkInput
{
    public float ThrottleAxis; // -1..1, W/S or Up/Down
    public float SteerAxis;    // -1..1, A/D or Left/Right
    public NetworkBool Boost;  // Left Shift
    public NetworkBool Drift;  // Left Ctrl
}
