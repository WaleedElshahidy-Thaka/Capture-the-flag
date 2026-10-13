using UnityEngine;

// Pure wall-contact resolution (GDD doc 05): given this tick's velocity and the wall it is about
// to meet, returns the velocity to write instead. Always PROJECTS along the wall, never reflects,
// so the player keeps their line and pays a speed cost proportional to the angle.
//   incidence < WallImpactAngle -> Scrape: slide along, lose up to WallScrapeCost
//   incidence >= WallImpactAngle -> Impact: slide along, lose WallImpactCost, small push-off
// Every outcome leaves the chassis moving (MinExitSpeed floor).
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

    const float WallNormalMaxUpDot = 0.5f; // normals more than 60° from vertical are walls

    public static bool IsWall(Vector3 normal) => normal.y < WallNormalMaxUpDot;

    // For a dead-on hit: slide whichever way the nose already leans along the wall.
    public static Vector3 SideHint(Vector3 chassisForward, Vector3 wallNormal)
    {
        Vector3 along = Vector3.Cross(wallNormal, Vector3.up);
        return Vector3.Dot(chassisForward, along) >= 0f ? along : Vector3.Cross(Vector3.up, wallNormal);
    }

    // False when not actually moving into the wall; the velocity then stands.
    public static bool TryResolve(Vector3 velocity, Vector3 wallNormal, Vector3 sideHint, HandlingValues handling, out Result result)
    {
        result = default;

        float closingSpeed = -Vector3.Dot(velocity, wallNormal);
        if (closingSpeed <= 0.01f) return false;

        float speed = velocity.magnitude;
        float incidence = Mathf.Asin(Mathf.Clamp01(closingSpeed / speed)) * Mathf.Rad2Deg;
        Vector3 along = velocity - Vector3.Dot(velocity, wallNormal) * wallNormal;

        Vector3 resolved;
        if (incidence < handling.WallImpactAngle)
        {
            result.Class = Class.Scrape;
            float cost = handling.WallScrapeCost * (incidence / handling.WallImpactAngle);
            resolved = along * (1f - cost);
        }
        else
        {
            // Costs total speed (not just the tiny along-wall part) and redirects what's left
            // along the wall, so a steep hit doesn't become a dead stop.
            result.Class = Class.Impact;
            Vector3 alongDirection = AlongWall(along, sideHint, wallNormal);
            resolved = alongDirection * (speed * (1f - handling.WallImpactCost))
                       + wallNormal * handling.WallReboundSpeed;
            result.QualifiesForSpinout = speed > handling.WallSpinoutSpeed;
        }

        // A wall can cost speed but never add it, so the floor is capped at the arrival speed.
        float floor = Mathf.Min(handling.MinExitSpeed, speed);
        if (resolved.magnitude < floor)
            resolved = AlongWall(along, sideHint, wallNormal) * floor;

        result.IncidenceDegrees = incidence;
        result.ClosingSpeed = closingSpeed;
        result.Velocity = resolved;
        return true;
    }

    static Vector3 AlongWall(Vector3 along, Vector3 sideHint, Vector3 wallNormal)
    {
        if (along.sqrMagnitude > 0.01f) return along.normalized;

        Vector3 side = sideHint - Vector3.Dot(sideHint, wallNormal) * wallNormal;
        return side.sqrMagnitude > 0.0001f ? side.normalized : Vector3.Cross(wallNormal, Vector3.up).normalized;
    }
}
