using Fusion;
using UnityEngine;

// Network adapter for one car. Owns the replicated drive state, chooses where this tick's input
// comes from (player, bot, or nothing yet) and hands the physics to VehicleDriveModel.
// Runs before RunnerSimulatePhysics (order 0) so the velocity written is integrated this tick.
[DefaultExecutionOrder(-50)]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(PlayerMatchState))]
public class PlayerMovement : NetworkBehaviour
{
    [SerializeField] HandlingValues handling = new HandlingValues();
    [SerializeField] LayerMask groundMask = ~0;

    [Header("Probe geometry (baked from the art by GameSceneSetup - don't hand-edit)")]
    [Tooltip("Local wheel positions: front-left, front-right, rear-left, rear-right.")]
    [SerializeField] Vector3[] probeOffsets = new Vector3[4];
    [Tooltip("Measured from the wheel mesh. Also the suspension's rest height.")]
    [SerializeField] float wheelRadius = 0.3f;

    [Header("Tire visuals (cosmetic only)")]
    [SerializeField] Transform visual; // parent of the tires - leaned, never the Rigidbody
    [SerializeField] Transform frontLeftTire;
    [SerializeField] Transform frontRightTire;
    [SerializeField] Transform rearLeftTire;
    [SerializeField] Transform rearRightTire;
    [SerializeField] float maxTireSteerAngle = 30f;

    [Header("Cosmetic body lean (visual only)")]
    [SerializeField] float maxLeanAngle = 12f;
    [SerializeField] float leanSpring = 8f;

    // Drive state, replicated so it is restored on rollback (mirrors DriveState).
    // Declaration order is the network layout - keep it stable.
    [Networked] float YawRate { get; set; }
    [Networked] float BrakeHoldSeconds { get; set; }
    [Networked] float SteerInput { get; set; } // so every peer can turn this car's front wheels
    [Networked] int SpinoutTicksRemaining { get; set; }
    [Networked] int ImmunityTicksRemaining { get; set; }
    [Networked] float SettleHeading { get; set; }
    [Networked] float SpinDegrees { get; set; }

    public bool IsSpinningOut => SpinoutTicksRemaining > 0;
    public bool HasContactImmunity => ImmunityTicksRemaining > 0;
    public bool IsGrounded => driveModel.IsGrounded;
    public float ForwardSpeed => driveModel.ForwardSpeed;

    Rigidbody body;
    PlayerMatchState matchState;
    VehicleDriveModel driveModel;
    VehicleVisuals visuals;

    void Awake()
    {
        body = GetComponent<Rigidbody>();
        matchState = GetComponent<PlayerMatchState>();
        ChassisPhysicsSetup.Apply(body);
        BuildModels();
    }

    // Lets inspector tweaks during Play Mode take effect immediately.
    void OnValidate()
    {
        if (Application.isPlaying && body != null) BuildModels();
    }

    void BuildModels()
    {
        driveModel = new VehicleDriveModel(body, handling, probeOffsets, wheelRadius, groundMask);
        visuals = new VehicleVisuals(new VehicleVisuals.Rig
        {
            Body = visual,
            FrontLeft = frontLeftTire,
            FrontRight = frontRightTire,
            RearLeft = rearLeftTire,
            RearRight = rearRightTire,
            WheelRadius = wheelRadius,
            MaxTireSteerAngle = maxTireSteerAngle,
            MaxLeanAngle = maxLeanAngle,
            LeanSpring = leanSpring,
        });
    }

    public override void FixedUpdateNetwork()
    {
        var session = MatchmakingSessionState.Local;
        if (session != null && session.Frozen)
        {
            // Held still after a host migration until the shared resume tick.
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            return;
        }

        if (!TryGetDriveInput(out var input)) return;

        // Before the match starts the car still simulates (settles on its suspension) with neutral input.
        SimulateTick(matchState.CanMove ? input : default, Runner.DeltaTime);
    }

    // A player's input arrives via GetInput; a car with no player is driven by BotDriver on the host.
    // Clients receive bot-driven cars by replication like any other proxy.
    bool TryGetDriveInput(out PlayerNetInput input)
    {
        if (GetInput(out input)) return true;
        if (!Object.HasStateAuthority || !matchState.IsBot) return false;

        input = BotDriver.Think(this);
        return true;
    }

    // Public so any input source (player or bot) goes through the same simulation.
    public void SimulateTick(PlayerNetInput input, float deltaTime)
    {
        DriveState state = ReadDriveState();
        SteerInput = driveModel.Simulate(input, ref state, deltaTime);
        WriteDriveState(state);
    }

    // Entered by a hard wall impact (inside the tick) or, later, by a slam from another car.
    public void EnterSpinout(Vector3 travelDirection, float spinSign)
    {
        DriveState state = ReadDriveState();
        driveModel.EnterSpinout(ref state, travelDirection, spinSign);
        WriteDriveState(state);
    }

    void LateUpdate()
    {
        if (!matchState.HasState) return; // networked SteerInput isn't readable without state
        visuals.Animate(ForwardSpeed, SteerInput, driveModel.Probes, Time.deltaTime);
    }

    DriveState ReadDriveState() => new DriveState
    {
        YawRate = YawRate,
        BrakeHoldSeconds = BrakeHoldSeconds,
        SpinoutTicksRemaining = SpinoutTicksRemaining,
        ImmunityTicksRemaining = ImmunityTicksRemaining,
        SettleHeading = SettleHeading,
        SpinDegrees = SpinDegrees,
    };

    void WriteDriveState(in DriveState state)
    {
        YawRate = state.YawRate;
        BrakeHoldSeconds = state.BrakeHoldSeconds;
        SpinoutTicksRemaining = state.SpinoutTicksRemaining;
        ImmunityTicksRemaining = state.ImmunityTicksRemaining;
        SettleHeading = state.SettleHeading;
        SpinDegrees = state.SpinDegrees;
    }
}
