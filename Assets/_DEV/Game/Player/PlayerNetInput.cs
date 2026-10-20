using Fusion;

// One tick of driving input. Gathered in Fusion's OnInput callback (PlayerInputSampler) and
// read by PlayerMovement via GetInput, or produced by BotDriver for unowned cars.
public struct PlayerNetInput : INetworkInput
{
    public float ThrottleAxis; // -1..1, W/S or Up/Down
    public float SteerAxis;    // -1..1, A/D or Left/Right
    public NetworkBool Boost;  // Left Shift / right shoulder, HELD (Layer C decides what a press means)
    public NetworkBool Drift;  // Left Ctrl. OBSOLETE: not in the Driving Foundation; PlayerMovement ignores it.
    public NetworkBool Hop;    // Space / A-Cross, HELD: press = hop, held = slide, release = open the boost window
}