using Fusion;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Builds Power-Ups/Prefab/OilPatch.prefab and wires it onto PlayerCar.prefab's OilSlick component
// (adding the component if missing). Safe to run again: it overwrites the patch prefab and keeps
// everything else on the car.
static class OilPatchPrefabBuilder
{
    const string PrefabFolder = "Assets/_DEV/Game/Power-Ups/Prefab";
    const string PatchPrefabPath = PrefabFolder + "/OilPatch.prefab";
    const string MaterialPath = PrefabFolder + "/Oil.mat";
    const string CarPrefabPath = "Assets/_DEV/Game/Player/Resources/PlayerCar.prefab";

    [MenuItem("Game/Build Oil Patch Prefab")]
    static void Build()
    {
        GameObject patchAsset = BuildPatchPrefab();
        if (patchAsset == null) return;

        WireOntoCar(patchAsset.GetComponent<NetworkObject>());
        AssetDatabase.SaveAssets();
        Debug.Log($"[OilPatchPrefabBuilder] Built {PatchPrefabPath} and wired it into {CarPrefabPath}.");
    }

    static GameObject BuildPatchPrefab()
    {
        var root = new GameObject("OilPatch", typeof(NetworkObject), typeof(OilPatch));
        try
        {
            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = "Visual";
            // No collider, ever: the ground probes would treat it as floor and the wall sensor as a wall.
            UnityEngine.Object.DestroyImmediate(disc.GetComponent<Collider>());
            disc.transform.SetParent(root.transform, false);
            disc.transform.localScale = new Vector3(1f, 0.005f, 1f); // unit diameter; OilPatch scales x/z to 2 x radius

            var renderer = disc.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = GetOrCreateMaterial();
            renderer.shadowCastingMode = ShadowCastingMode.Off;

            SetupUtils.SetField(root.GetComponent<OilPatch>(), "visual", disc.transform);

            return PrefabUtility.SaveAsPrefabAsset(root, PatchPrefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    static void WireOntoCar(NetworkObject patchPrefab)
    {
        GameObject car = PrefabUtility.LoadPrefabContents(CarPrefabPath);
        try
        {
            var oil = car.GetComponent<OilSlick>();
            if (oil == null) oil = car.AddComponent<OilSlick>();
            SetupUtils.SetField(oil, "patchPrefab", patchPrefab);
            PrefabUtility.SaveAsPrefabAsset(car, CarPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(car);
        }
    }

    // Dark, glossy, slightly blue - reads as oil against FD-06's desaturated arena palette.
    static Material GetOrCreateMaterial()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (existing != null) return existing;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var material = new Material(shader) { name = "Oil" };
        var color = new Color(0.04f, 0.04f, 0.07f);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.95f);
        if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.95f);

        AssetDatabase.CreateAsset(material, MaterialPath);
        return material;
    }
}
