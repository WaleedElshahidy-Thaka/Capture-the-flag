using UnityEngine;

// Step 11 of the drive model's tick, for walls only (GDD doc 05, "Wall behaviour"). Pure
// functions: given this tick's composed velocity and the wall it is about to meet, return the
// velocity to write instead. No MonoBehaviour, no state, nothing networked - doc 05 allows wall
// contact to resolve locally because walls don't move and every peer has the same geometry.
//
// The one rule that matters: PROJECT, never reflect. A reflected velocity throws the chassis
// back across the arena at the mirror angle, which is what real physics does and what feels
// terrible in an arcade driving game. Projection slides it along the wall and bleeds speed by
// angle - the player stays on their line and pays a proportional, readable price.
//
// Classification is by incidence angle alone (velocity against the wall surface):
//   below WallImpactAngle  -> Scrape: slide along the wall, lose WallScrapeCost scaled by angle
//   above                  -> Impact: slide along the wall, lose WallImpactCost, small push-off
//                             away from the wall; qualifies for spin-out above WallSpinoutSpeed
// Every outcome leaves the chassis moving: MinExitSpeed is a floor, applied structurally.
public static class WallContact
{
    public enum Class { Scrape, Impact }

    public struct Result
    {
        public Class Class;
        public float IncidenceDegrees;
        public float ClosingSpeed;   // m/s into the wall, before resolution
        public bool QualifiesForSpinout;
        public Vector3 Velocity;     // what the drive model writes instead
    }

    // Anything whose surface normal is this far or more from straight up is a wall. Floors and
    // ramps face up; walls face sideways. No layers or tags needed for a placeholder arena.
    const float WallNormalMaxUpDot = 0.5f; // 60 degrees from vertical

    public static bool IsWall(Vector3 normal) => normal.y < WallNormalMaxUpDot;

    // False when the chassis isn't actually moving into the wall (already sliding along it, or
    // moving away) - then there is nothing to resolve and the velocity stands.
    //
    // sideHint: which way along the wall to send a dead-on hit, where the velocity itself has no
    // "along" to speak of - the chassis's own right vector does; the sign of its lean decides.
    public static bool TryResolve(Vector3 velocity, Vector3 wallNormal, Vector3 sideHint, HandlingValues handling, out Result result)
    {
        result = default;

        float closingSpeed = -Vector3.Dot(velocity, wallNormal);
        if (closingSpeed <= 0.01f) return false;

        float speed = velocity.magnitude;
        float incidence = Mathf.Asin(Mathf.Clamp01(closingSpeed / speed)) * Mathf.Rad2Deg;
        Vector3 along = velocity - Vector3.Dot(velocity, wallNormal) * wallNormal; // the projection

        Vector3 resolved;
        if (incidence < handling.WallImpactAngle)
        {
            // Scrape: the projection is the exit, costed by angle.
            result.Class = Class.Scrape;
            float cost = handling.WallScrapeCost * (incidence / handling.WallImpactAngle);
            resolved = along * (1f - cost);
        }
        else
        {
            // Impact: the doc costs SPEED, not the along-wall component - which at 86 degrees is
            // nearly nothing and would leave the floor doing all the work: a dead stop with a
            // nudge. So the retained speed is redirected along the wall, whichever way the
            // chassis was already leaning, and the player drives on out of it.
            result.Class = Class.Impact;
            Vector3 alongDirection = AlongWall(along, sideHint, wallNormal);
            resolved = alongDirection * (speed * (1f - handling.WallImpactCost))
                       + wallNormal * handling.WallReboundSpeed;
            result.QualifiesForSpinout = speed > handling.WallSpinoutSpeed;
        }

        // The floor. Never above the speed the chassis arrived with - a wall can cost speed, it
        // can't hand it out, so a slow nudge against a wall doesn't launch a parked car.
        float floor = Mathf.Min(handling.MinExitSpeed, speed);
        if (resolved.magnitude < floor)
            resolved = AlongWall(along, sideHint, wallNormal) * floor;

        result.IncidenceDegrees = incidence;
        result.ClosingSpeed = closingSpeed;
        result.Velocity = resolved;
        return true;
    }

    // Unit direction along the wall plane: the velocity's own projection when it has one, else
    // the side hint projected onto the wall (a dead-on hit still has to go somewhere, and
    // sideways along the wall is the only readable somewhere).
    static Vector3 AlongWall(Vector3 along, Vector3 sideHint, Vector3 wallNormal)
    {
        if (along.sqrMagnitude > 0.01f) return along.normalized;

        Vector3 side = sideHint - Vector3.Dot(sideHint, wallNormal) * wallNormal;
        return side.sqrMagnitude > 0.0001f ? side.normalized : Vector3.Cross(wallNormal, Vector3.up).normalized;
    }
}
