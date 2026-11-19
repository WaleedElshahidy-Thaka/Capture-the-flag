using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Game > Build Player Marker Prefab: creates a UI marker prefab for PlayerIndicatorsUI and assigns
// it to the PlayerIndicatorsUI in the open scene. The marker is a plain UI object - never part of the
// car prefab. Placeholder art (Unity's built-in circle and square); swap the sprites for yours later.
//
//   PlayerMarker (RectTransform 48x48, PlayerIndicatorView)
//   ├── Arrow       - shown at the screen edge, points at the car (rotates around the icon)
//   │   └── Tip     - the pointy bit, sitting to the RIGHT of the icon (0 degrees)
//   ├── Icon        - tinted with the seat colour
//   ├── CrownBadge  - shown on the Crown holder
//   └── Distance    - "24 m" under the icon
static class PlayerMarkerPrefabBuilder
{
    const string PrefabPath = "Assets/_DEV/Game/UI/Prefabs/PlayerMarker.prefab";

    [MenuItem("Game/Build Player Marker Prefab")]
    static void Build()
    {
        Sprite circle = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        Sprite square = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        var root = new GameObject("PlayerMarker", typeof(RectTransform), typeof(PlayerIndicatorView));
        try
        {
            var rootRect = (RectTransform)root.transform;
            rootRect.sizeDelta = new Vector2(48f, 48f);

            // Arrow: an empty pivot at the centre, with the tip offset to the right. Rotating the pivot
            // swings the tip around the icon, so it always points toward the car.
            var arrow = NewChild("Arrow", root.transform, Vector2.zero, Vector2.zero);
            var tip = NewChild("Tip", arrow, new Vector2(34f, 0f), new Vector2(16f, 16f));
            tip.localRotation = Quaternion.Euler(0f, 0f, 45f); // a diamond reads as an arrowhead
            var tipImage = tip.gameObject.AddComponent<Image>();
            tipImage.sprite = square;
            tipImage.raycastTarget = false;

            var icon = NewChild("Icon", root.transform, Vector2.zero, new Vector2(36f, 36f));
            var iconImage = icon.gameObject.AddComponent<Image>();
            iconImage.sprite = circle;
            iconImage.raycastTarget = false;

            var crown = NewChild("CrownBadge", root.transform, new Vector2(0f, 30f), new Vector2(40f, 24f));
            var crownText = crown.gameObject.AddComponent<TextMeshProUGUI>();
            crownText.text = "CROWN";
            crownText.fontSize = 14f;
            crownText.fontStyle = FontStyles.Bold;
            crownText.alignment = TextAlignmentOptions.Center;
            crownText.color = new Color(1f, 0.8f, 0.2f);
            crownText.raycastTarget = false;

            var distance = NewChild("Distance", root.transform, new Vector2(0f, -32f), new Vector2(80f, 20f));
            var distanceText = distance.gameObject.AddComponent<TextMeshProUGUI>();
            distanceText.text = "0 m";
            distanceText.fontSize = 14f;
            distanceText.alignment = TextAlignmentOptions.Center;
            distanceText.raycastTarget = false;

            var view = root.GetComponent<PlayerIndicatorView>();
            SetupUtils.SetField(view, "icon", iconImage);
            SetupUtils.SetField(view, "arrow", arrow);
            SetupUtils.SetField(view, "arrowImage", tipImage);
            SetupUtils.SetField(view, "crownBadge", crown.gameObject);
            SetupUtils.SetField(view, "distanceText", (TMP_Text)distanceText);

            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(PrefabPath));
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);

            // Hook it up in the open scene.
            var indicators = Object.FindFirstObjectByType<PlayerIndicatorsUI>();
            if (indicators != null)
            {
                Undo.RecordObject(indicators, "Assign player marker");
                SetupUtils.SetField(indicators, "markerPrefab", prefab.GetComponent<PlayerIndicatorView>());
                EditorUtility.SetDirty(indicators);
                Debug.Log($"[PlayerMarkerPrefabBuilder] Built {PrefabPath} and assigned it to {indicators.name}.");
            }
            else
            {
                Debug.Log($"[PlayerMarkerPrefabBuilder] Built {PrefabPath}. No PlayerIndicatorsUI in the open scene - assign it by hand.");
            }
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    static RectTransform NewChild(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }
}
