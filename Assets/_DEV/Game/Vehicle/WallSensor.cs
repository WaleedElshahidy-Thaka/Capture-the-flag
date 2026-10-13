using UnityEngine;

// Looks ahead along the direction of travel for a wall the chassis will reach this tick.
// Three rays (centre and both front corners) rather than a box sweep: a sweep that starts
// inside its own collider reports garbage normals during a continuous scrape.
public class WallSensor
{
    const float HalfWidth = 0.45f;   // half the chassis box (see PlayerCarPrefabBuilder)
    const float HalfLength = 0.55f;
    const float Skin = 0.05f;

    readonly LayerMask mask;

    public WallSensor(LayerMask mask) => this.mask = mask;

    // heading: horizontal unit direction of travel. Returns the nearest static wall hit.
    public bool TryFindWall(Vector3 origin, Vector3 heading, float speed, float dt, out RaycastHit wall)
    {
        Vector3 side = Vector3.Cross(Vector3.up, heading) * HalfWidth;
        float reach = HalfLength + speed * dt + Skin;

        bool found = false;
        wall = default;
        for (int i = -1; i <= 1; i++)
        {
            if (!Physics.Raycast(origin + side * i, heading, out var hit, reach, mask, QueryTriggerInteraction.Ignore)) continue;
            // Anything with a Rigidbody (our own wheels, another car) is not a wall.
            if (hit.rigidbody != null) continue;
            if (!WallContact.IsWall(hit.normal)) continue;
            if (found && hit.distance >= wall.distance) continue;
            wall = hit;
            found = true;
        }
        return found;
    }
}
