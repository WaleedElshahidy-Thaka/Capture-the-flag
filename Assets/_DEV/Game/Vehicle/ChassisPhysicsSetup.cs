using UnityEngine;

// Puts the chassis Rigidbody into the state the drive model expects. The model composes and
// writes velocity itself every tick, so PhysX must not add gravity, damping, friction or sleep.
// Enforced in code so a stale prefab can't reintroduce any of them.
public static class ChassisPhysicsSetup
{
    public static void Apply(Rigidbody body)
    {
        body.constraints = RigidbodyConstraints.None; // levelled actively by ChassisAttitude, never frozen
        body.useGravity = false;                      // gravity is HandlingValues.GravityScale
        body.linearDamping = 0f;                      // drag is CoastDrag / AirDrag
        body.angularDamping = 0f;
        body.sleepThreshold = 0f;                     // a sleeping body ignores the velocity we write

        var frictionless = new PhysicsMaterial("ChassisFrictionless")
        {
            dynamicFriction = 0f,
            staticFriction = 0f,
            frictionCombine = PhysicsMaterialCombine.Minimum,
        };
        foreach (var collider in body.GetComponents<Collider>())
            collider.material = frictionless; // grip is GripRate, not a physics material
    }
}
