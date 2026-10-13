using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

// Low-level uGUI builders for the Game/Setup Game Scene tool. All find-or-create by name, so
// re-running the tool updates objects instead of duplicating them.
static class SetupUI
{
    static readonly Color PanelColor = new Color(0.08f, 0.09f, 0.12f, 0.78f);
    static readonly Color ButtonColor = new Color(0.18f, 0.22f, 0.30f, 1f);
    const float SidePanelWidth = 340f;
    const float SidePanelMargin = 24f;

    public static RectTransform GetOrCreateCanvas(string name)
    {
        var existing = GameObject.Find(name);
        if (existing != null) return existing.GetComponent<RectTransform>();

        var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(go, SetupUtils.UndoLabel);

        go.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        return go.GetComponent<RectTransform>();
    }

    public static void EnsureEventSystem()
    {
        if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() != null) return;

        var go = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
        go.AddComponent<InputSystemUIInputModule>();
#else
        go.AddComponent<StandaloneInputModule>();
#endif
        Undo.RegisterCreatedObjectUndo(go, SetupUtils.UndoLabel);
    }

    // Finds a child by name, adding any missing components, or creates it.
    public static RectTransform GetOrCreateUI(string name, Transform parent, params Type[] components)
    {
        var existing = parent != null ? parent.Find(name) : null;
        if (existing != null)
        {
            foreach (var type in components)
                if (existing.GetComponent(type) == null) Undo.AddComponent(existing.gameObject, type);
            return existing.GetComponent<RectTransform>();
        }

        var types = new Type[components.Length + 1];
        types[0] = typeof(RectTransform);
        Array.Copy(components, 0, types, 1, components.Length);

        var go = new GameObject(name, types);
        go.transform.SetParent(parent, false);
        Undo.RegisterCreatedObjectUndo(go, SetupUtils.UndoLabel);
        return go.GetComponent<RectTransform>();
    }

    // Small translucent box on the left edge, sized to its content.
    public static RectTransform SidePanel(string name, Transform parent, float anchoredY = 0f)
    {
        var rt = GetOrCreateUI(name, parent, typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.sizeDelta = new Vector2(SidePanelWidth, 0f);
        rt.anchoredPosition = new Vector2(SidePanelMargin, anchoredY);

        var img = rt.GetComponent<Image>();
        img.color = PanelColor;
        img.enabled = true;

        var vlg = rt.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 10f;
        vlg.padding = new RectOffset(14, 14, 12, 12);
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        rt.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return rt;
    }

    public static RectTransform FullScreenPanel(string name, Transform parent, Color color, string text, out TMP_Text label)
    {
        var panel = GetOrCreateUI(name, parent, typeof(Image));
        Stretch(panel);
        panel.GetComponent<Image>().color = color;

        label = CreateText("Text", panel, text, 28, 48);
        var rt = label.rectTransform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(700f, 48f);
        rt.anchoredPosition = Vector2.zero;
        return panel;
    }

    public static TMP_Text CreateText(string name, Transform parent, string content, float size, float height = 48)
    {
        var rt = GetOrCreateUI(name, parent, typeof(TextMeshProUGUI), typeof(LayoutElement));
        var tmp = rt.GetComponent<TextMeshProUGUI>();
        tmp.text = content;
        tmp.fontSize = size;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;

        var le = rt.GetComponent<LayoutElement>();
        le.preferredHeight = height;
        le.minHeight = height;
        return tmp;
    }

    public static Button CreateButton(string name, Transform parent, string label, float height = 44, float fontSize = 20)
    {
        var rt = GetOrCreateUI(name, parent, typeof(Image), typeof(Button), typeof(LayoutElement));
        var img = rt.GetComponent<Image>();
        img.color = ButtonColor;

        var le = rt.GetComponent<LayoutElement>();
        le.preferredHeight = height;
        le.minHeight = height;

        var labelRt = GetOrCreateUI("Label", rt, typeof(TextMeshProUGUI));
        Stretch(labelRt);
        var tmp = labelRt.GetComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;

        var button = rt.GetComponent<Button>();
        button.targetGraphic = img;
        return button;
    }

    // A small world-space canvas 1.6 m above `parent` with a centred name label.
    public static GameObject WorldNameTag(Transform parent, out TextMeshProUGUI nameLabel)
    {
        var tagGO = new GameObject("NameTag", typeof(RectTransform), typeof(Canvas));
        tagGO.transform.SetParent(parent, false);
        tagGO.transform.localPosition = new Vector3(0f, 1.6f, 0f);
        tagGO.transform.localScale = Vector3.one * 0.01f;
        tagGO.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        tagGO.GetComponent<RectTransform>().sizeDelta = new Vector2(300f, 110f);

        nameLabel = WorldLabel("Name", tagGO.transform, "Player", 36, Color.white, new Vector2(0f, 20f), 60f);
        return tagGO;
    }

    public static TextMeshProUGUI WorldLabel(string name, Transform parent, string text, float fontSize, Color color, Vector2 anchoredPosition, float height)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(1f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(0f, height);
        rt.anchoredPosition = anchoredPosition;

        var label = go.GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.color = color;
        return label;
    }

    public static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
