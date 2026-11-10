public struct DriftBoostInput
{
    public bool Hop;                 // Space HELD: press = hop, held = slide, release = slide ends
    public bool Boost;               // Left Shift HELD; a PRESS inside a stage window fires that stage
    public float Steer;              // -1..1
    public float ForwardSpeed;       // signed m/s along the nose
    public float LateralSpeed;       // signed m/s sideways (for slip angle)
    public bool Grounded;
    public bool Collided;            // not used: contact never costs direction, reserve or the slide
    public float SurfaceChargeFactor; // surface's drift-charge channel; 1 on the default surface (must be set)
    public int Tick;                 // simulation tick, for same-tick boost merging
}
