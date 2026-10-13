using System;
using UnityEditor;
using UnityEngine;

// Shared constants and helpers for the Game/Setup Game Scene editor tool.
static class SetupUtils
{
    public const string UndoLabel = "Setup Game Scene";
    public const string MainCameraName = "Main Camera";
    public const string RobotArtPath = "Assets/_DEV/Game/Art/Bolt/PlayerRobot.prefab";

    public static GameObject GetOrCreatePlain(string name, Transform parent, params Type[] components)
    {
        Transform existing = parent != null ? parent.Find(name) : GameObject.Find(name)?.transform;
        if (existing != null) return existing.gameObject;

        var go = new GameObject(name, components);
        if (parent != null) go.transform.SetParent(parent, false);
        Undo.RegisterCreatedObjectUndo(go, UndoLabel);
        return go;
    }

    public static void DestroyIfExists(string name)
    {
        var existing = GameObject.Find(name);
        if (existing != null) UnityEngine.Object.DestroyImmediate(existing);
    }

    // The robot art as a child named "Visual", or a capsule if the art is missing.
    public static GameObject InstantiateRobotArt(Transform parent, out bool isPlaceholder)
    {
        var art = AssetDatabase.LoadAssetAtPath<GameObject>(RobotArtPath);
        isPlaceholder = art == null;
        GameObject visual = isPlaceholder
            ? GameObject.CreatePrimitive(PrimitiveType.Capsule)
            : (GameObject)PrefabUtility.InstantiatePrefab(art);

        visual.name = "Visual";
        visual.transform.SetParent(parent, false);
        visual.transform.localRotation = Quaternion.identity;
        return visual;
    }

    // Sets a serialized object-reference field by name (works on private [SerializeField]s).
    public static void SetField(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
    {
        var so = new SerializedObject(target);
        var prop = so.FindProperty(propertyName);
        if (prop == null)
        {
            Debug.LogError($"[GameSceneSetup] Field '{propertyName}' not found on {target.GetType().Name}.");
            return;
        }
        prop.objectReferenceValue = value;
        so.ApplyModifiedProperties();
    }

    public static void EnsureFolder(string parent, string name)
    {
        string path = $"{parent}/{name}";
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, name);
    }
}
