using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Game > Fix VFX Pack (Reimport): recompiles an imported VFX pack in the right order - Shader Graphs
// first, then the VFX Graphs that use them, then the prefabs - so effects imported from another
// project stop showing pink. Select the pack's folder in the Project window first (or it uses the
// default path below).
static class VFXPackReimporter
{
    const string DefaultFolder = "Assets/Adjustable Slash VFX Pack";

    [MenuItem("Game/Fix VFX Pack (Reimport)")]
    static void Reimport()
    {
        string folder = SelectedFolder() ?? DefaultFolder;
        if (!AssetDatabase.IsValidFolder(folder))
        {
            Debug.LogError($"[VFXPackReimporter] Folder not found: {folder}. Select the pack's folder in the Project window and try again.");
            return;
        }

        int shaders = ReimportAll(folder, "*.shadergraph");
        int graphs = ReimportAll(folder, "*.vfx");
        int prefabs = ReimportAll(folder, "*.prefab");
        AssetDatabase.Refresh();

        Debug.Log($"[VFXPackReimporter] Reimported {shaders} Shader Graphs, {graphs} VFX Graphs and {prefabs} prefabs in {folder}.");
    }

    static int ReimportAll(string folder, string pattern)
    {
        var files = Directory.GetFiles(folder, pattern, SearchOption.AllDirectories)
                             .Select(p => p.Replace('\\', '/'))
                             .ToArray();
        foreach (var path in files)
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        return files.Length;
    }

    static string SelectedFolder()
    {
        foreach (var obj in Selection.GetFiltered<Object>(SelectionMode.Assets))
        {
            string path = AssetDatabase.GetAssetPath(obj);
            if (AssetDatabase.IsValidFolder(path)) return path;
        }
        return null;
    }
}
