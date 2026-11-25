using System.Collections.Generic;
using UnityEngine;

// Applies a lobby skin to a robot already present in the scene.
//
// The property list and the order it is written in mirror SkinCardHolder.ApplySkinTextures in
// the lobby exactly. Robots here use a mix of URP Lit and Standard materials, so the same three
// source textures are offered under both naming schemes (_BaseMap/_MainTex, _MetallicGlossMap/
// _MaskMap/_MetallicMap) and whichever the shader actually declares is the one that sticks.
//
// Unlike the lobby, this writes to per-renderer material instances rather than the material
// asset. The lobby edits the asset because its robot preview and its selection UI are the same
// screen; a game only ever dresses the one robot it spawned, and mutating a shared asset at
// runtime leaks the change into every other object using it — and, in the Editor, onto disk.
//
// Duplicated verbatim in every game. Keep the copies identical.
public static class BotSkins
{
    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    private static readonly int NormalMapId = Shader.PropertyToID("_BumpMap");
    private static readonly int MetallicGlossMapId = Shader.PropertyToID("_MetallicGlossMap");
    private static readonly int MaskMapId = Shader.PropertyToID("_MaskMap");
    private static readonly int MetallicMapId = Shader.PropertyToID("_MetallicMap");

    /// <summary>
    /// Dresses <paramref name="scope"/> (a robot root, or any object holding its renderers) in
    /// the named skin. Returns false when there is nothing to do — no library, unknown robot,
    /// unknown or unset skin name — which is the normal path when the player launched a game
    /// without ever opening the skin screen.
    /// </summary>
    public static bool Apply(BotSkinLibrary library, string botName, string skinName, GameObject scope)
    {
        if (library == null || scope == null) return false;

        BotSkinSet set = library.FindBot(botName);
        if (set == null)
        {
            Debug.LogWarning($"[BotSkins] No skin set for bot '{botName}'; leaving its material as authored.");
            return false;
        }

        // No --skin= is the ordinary case, not a fault: the robot keeps the skin baked into
        // its material in the scene.
        if (string.IsNullOrEmpty(skinName)) return false;

        BotSkin skin = set.FindSkin(skinName);
        if (skin == null)
        {
            Debug.LogWarning($"[BotSkins] Bot '{botName}' has no skin named '{skinName}'; leaving its material as authored.");
            return false;
        }

        List<Material> targets = CollectTargetMaterials(scope, set.targetMaterial);
        if (targets.Count == 0)
        {
            Debug.LogWarning($"[BotSkins] Found no renderer on '{scope.name}' using {(set.targetMaterial == null ? "any material" : set.targetMaterial.name)}; skin '{skinName}' not applied.");
            return false;
        }

        for (int i = 0; i < targets.Count; i++) ApplyTo(targets[i], skin);
        return true;
    }

    // Reading Renderer.materials is what instantiates the materials, so it must be read once
    // per renderer and the resulting instances kept — re-reading it later would hand back the
    // same instances, but writing through .sharedMaterial would hit the asset instead.
    private static List<Material> CollectTargetMaterials(GameObject scope, Material filter)
    {
        List<Material> found = new List<Material>();
        Renderer[] renderers = scope.GetComponentsInChildren<Renderer>(true);

        for (int r = 0; r < renderers.Length; r++)
        {
            Material[] shared = renderers[r].sharedMaterials;
            bool wanted = filter == null;
            for (int s = 0; s < shared.Length && !wanted; s++)
                wanted = shared[s] == filter;
            if (!wanted) continue;

            Material[] instances = renderers[r].materials;
            for (int s = 0; s < instances.Length; s++)
            {
                if (filter != null && (s >= shared.Length || shared[s] != filter)) continue;
                if (instances[s] != null) found.Add(instances[s]);
            }
        }
        return found;
    }

    private static void ApplyTo(Material material, BotSkin skin)
    {
        if (material.HasProperty(BaseMapId)) material.SetTexture(BaseMapId, skin.baseMap);
        if (material.HasProperty(MainTexId)) material.SetTexture(MainTexId, skin.baseMap);
        if (material.HasProperty(NormalMapId)) material.SetTexture(NormalMapId, skin.normalMap);

        if (material.HasProperty(MetallicGlossMapId))
        {
            material.SetTexture(MetallicGlossMapId, skin.metallicMap);
            // Standard ignores the map unless the keyword is on; the lobby does the same.
            if (skin.metallicMap != null) material.EnableKeyword("_METALLICGLOSSMAP");
        }

        if (material.HasProperty(MaskMapId)) material.SetTexture(MaskMapId, skin.metallicMap);
        if (material.HasProperty(MetallicMapId)) material.SetTexture(MetallicMapId, skin.metallicMap);
    }
}
