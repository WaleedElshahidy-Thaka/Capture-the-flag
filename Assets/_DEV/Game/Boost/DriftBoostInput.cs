public struct DriftBoostInput
{
    public bool Hop;                 // Space HELD (press = hop, held = slide, release = open window)
    public bool Boost;               // Left Shift HELD (held counts as a press the tick the window opens)
    public float Steer;              // -1..1
    public float ForwardSpeed;       // signed m/s along the nose
    public float LateralSpeed;       // signed m/s sideways (for slip angle)
    public bool Grounded;
    public bool Collided;            // any wall/car contact this tick - opens the window, never costs reserve
    public float SurfaceChargeFactor; // surface's drift-charge channel; 1 on the default surface (must be set)
    public int Tick;                 // simulation tick, for same-tick boost merging
}
