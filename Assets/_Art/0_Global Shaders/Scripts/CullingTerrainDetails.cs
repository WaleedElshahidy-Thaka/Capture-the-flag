using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(Terrain))]
public class TerrainGrassCulling : MonoBehaviour
{
    [Header("Culling Settings")]
    [Tooltip("Distance from camera at which grass stops rendering.")]
    public float maxDrawDistance = 100f;

    [Range(0f, 1f)]
    [Tooltip("Grass density multiplier.")]
    public float grassDensity = 1f;

    private Terrain terrainComp;
    private Camera mainCam;

    void OnEnable()
    {
        terrainComp = GetComponent<Terrain>();
        if (!terrainComp) return;

        if (!mainCam) mainCam = Camera.main;
        ApplySettings(maxDrawDistance, grassDensity);
    }

    void Update()
    {
        if (!terrainComp) return;
        if (!mainCam) mainCam = Camera.main;
        if (!mainCam) return;

        // Get the closest point on terrain to camera
        Vector3 terrainCenter = terrainComp.terrainData.bounds.center + terrainComp.transform.position;
        float dist = Vector3.Distance(mainCam.transform.position, terrainCenter);

        // Enable/disable grass based on distance
        if (dist <= maxDrawDistance)
            ApplySettings(maxDrawDistance, grassDensity);
        else
            ApplySettings(0f, grassDensity);
    }

    private void ApplySettings(float drawDist, float density)
    {
        terrainComp.detailObjectDistance = drawDist;
        terrainComp.detailObjectDensity = density;
    }
}
