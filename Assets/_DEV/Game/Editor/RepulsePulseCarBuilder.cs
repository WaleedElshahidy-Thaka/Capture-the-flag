using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Adds RepulsePulse to PlayerCar.prefab with its two visuals (charge-up and burst), parented under
// View so they follow the render-interpolated car rather than the raw simulation. Safe to run again:
// existing visuals are replaced, everything else on the car is kept.
static class RepulsePulseCarBuilder
{
    const string CarPrefabPath = "Assets/_DEV/Game/Player/Resources/PlayerCar.prefab";
    const string MaterialFolder = "Assets/_DEV/Game/Power-Ups/Prefab";
    const string ChargeMaterialPath = MaterialFolder + "/RepulseCharge.mat";
    const string BurstMaterialPath = MaterialFolder + "/RepulseBurst.mat";

    [MenuItem("Game/Add Repulse Pulse To Car")]
    static void Build()
    {
        GameObject car = PrefabUtility.LoadPrefabContents(CarPrefabPath);
        try
        {
            var pulse = car.GetComponent<RepulsePulse>();
            if (pulse == null) pulse = car.AddComponent<RepulsePulse>();

            Transform view = car.transform.Find("View");
            if (view == null)
            {
                Debug.LogWarning("[RepulsePulseCarBuilder] No 'View' child on the car; parenting visuals to the root.");
                view = car.transform;
            }

            Transform charge = CreateSphere(view, "RepulseCharge",
                GetOrCreateMaterial(ChargeMaterialPath, new Color(0.35f, 0.85f, 1f, 0.18f)));
            Transform burst = CreateSphere(view, "RepulseBurst",
                GetOrCreateMaterial(BurstMaterialPath, new Color(0.6f, 0.95f, 1f, 0.35f)));

            SetupUtils.SetField(pulse, "chargeVisual", charge);
            SetupUtils.SetField(pulse, "burstVisual", burst);

            PrefabUtility.SaveAsPrefabAsset(car, CarPrefabPath);
            Debug.Log($"[RepulsePulseCarBuilder] RepulsePulse added to {CarPrefabPath}.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(car);
        }
        AssetDatabase.SaveAssets();
    }

    static Transform CreateSphere(Transform parent, string name, Material material)
    {
        Transform old = parent.Find(name);
        if (old != null) Object.DestroyImmediate(old.gameObject);

        var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = name;
        // No collider, ever: it would block the probes, the wall sensor and other cars.
        Object.DestroyImmediate(sphere.GetComponent<Collider>());
        sphere.transform.SetParent(parent, false);
        sphere.transform.localPosition = Vector3.zero;

        var renderer = sphere.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        sphere.SetActive(false); // RepulsePulse shows it while charging / bursting
        return sphere.transform;
    }

    // Translucent URP Unlit. Falls back to an opaque Standard material if URP isn't present.
    static Material GetOrCreateMaterial(string path, Color color)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Standard");
        var material = new Material(shader);

        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 1f); // Transparent
            material.SetFloat("_Blend", 0f);   // Alpha
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
        }

        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);

        AssetDatabase.CreateAsset(material, path);
        return material;
    }
}
