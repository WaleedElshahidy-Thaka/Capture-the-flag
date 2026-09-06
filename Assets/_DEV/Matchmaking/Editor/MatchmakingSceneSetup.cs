using System;
using Fusion;
using Fusion.Editor;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

// Builds the two networked prefabs (PlayerLobbyState, MatchmakingSessionState - must live
// under a Resources/ folder since the services load them via Resources.Load), the local
// LobbySlotView placeholder prefab, and the Matchmaking scene itself (UI screen, seat
// anchors, flow controller), then registers the networked prefabs with Fusion's Network
// Prefab Table. Idempotent - safe to re-run after script changes. Same technique as the
// shelved LobbyUISceneSetup.cs: there's no way to drive the Unity Editor GUI directly from
// here, so this produces a normal, hand-editable scene/prefabs instead.
public static class MatchmakingSceneSetup
{
    const string ScenePath = "Assets/_DEV/Matchmaking/Scenes/Matchmaking.unity";
    const string ResourcesPath = "Assets/_DEV/Matchmaking/Resources";
    const string PrefabsPath = "Assets/_DEV/Matchmaking/Prefabs";
    const string UndoLabel = "Setup Matchmaking";

    static readonly Color PanelColor = new Color(0.08f, 0.09f, 0.12f, 0.96f);
    static readonly Color ButtonColor = new Color(0.18f, 0.22f, 0.30f, 1f);

    [MenuItem("Multiplayer/Setup Matchmaking Scene")]
    public static void SetupScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[MatchmakingSceneSetup] Exit Play Mode before running this.");
            return;
        }

        EnsureFolders();
        OpenOrCreateScene();

        BuildPlayerLobbyStatePrefab();
        BuildMatchmakingSessionStatePrefab();
        var slotViewPrefab = BuildLobbySlotViewPrefab();

        EnsureEventSystem();
        var canvas = GetOrCreateCanvasUI();
        BuildScreen(canvas, out var refs);

        var anchorsGO = GetOrCreatePlain("SeatAnchors", null, typeof(LobbySeatAnchors));
        BuildAnchors(anchorsGO);

        var flowControllerGO = GetOrCreatePlain("MatchmakingFlowController", null, typeof(MatchmakingFlowController));

        var seatAssignerGO = GetOrCreatePlain("SeatAssigner", null, typeof(LobbySeatAssigner));
        SetField(seatAssignerGO.GetComponent<LobbySeatAssigner>(), "anchors", anchorsGO.GetComponent<LobbySeatAnchors>());
        SetField(seatAssignerGO.GetComponent<LobbySeatAssigner>(), "slotViewPrefab", slotViewPrefab.GetComponent<LobbySlotView>());

        WireScreen(canvas, refs, flowControllerGO.GetComponent<MatchmakingFlowController>());

        var activeScene = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);

        NetworkProjectConfigUtilities.RebuildPrefabTable();

        Debug.Log("[MatchmakingSceneSetup] Done. Backend is QuickMatchLocalService (MatchmakingServices.cs) — " +
                   "open Assets/_DEV/Matchmaking/Scenes/Matchmaking.unity and press Play to test the solo path.");
    }

    // ─── Scene ───────────────────────────────────────────────────────────

    static void OpenOrCreateScene()
    {
        if (System.IO.File.Exists(ScenePath))
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }
        else
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }
    }

    // ─── Prefabs ─────────────────────────────────────────────────────────

    static void BuildPlayerLobbyStatePrefab()
    {
        string path = $"{ResourcesPath}/PlayerLobbyState.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;

        var go = new GameObject("PlayerLobbyState", typeof(NetworkObject), typeof(PlayerLobbyState));
        PrefabUtility.SaveAsPrefabAsset(go, path);
        UnityEngine.Object.DestroyImmediate(go);
    }

    static void BuildMatchmakingSessionStatePrefab()
    {
        string path = $"{ResourcesPath}/MatchmakingSessionState.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;

        var go = new GameObject("MatchmakingSessionState", typeof(NetworkObject), typeof(MatchmakingSessionState));
        PrefabUtility.SaveAsPrefabAsset(go, path);
        UnityEngine.Object.DestroyImmediate(go);
    }

    static GameObject BuildLobbySlotViewPrefab()
    {
        string path = $"{PrefabsPath}/LobbySlotView.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing;

        var root = new GameObject("LobbySlotView", typeof(LobbySlotView));

        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Body";
        body.transform.SetParent(root.transform, false);
        body.transform.localPosition = new Vector3(0, 1f, 0);
        UnityEngine.Object.DestroyImmediate(body.GetComponent<CapsuleCollider>());

        var nameTagGO = new GameObject("NameTag", typeof(TextMeshPro));
        nameTagGO.transform.SetParent(root.transform, false);
        nameTagGO.transform.localPosition = new Vector3(0, 2.6f, 0);
        var nameTag = nameTagGO.GetComponent<TextMeshPro>();
        nameTag.alignment = TextAlignmentOptions.Center;
        nameTag.fontSize = 4;
        nameTag.text = "Player";

        var readyLabelGO = new GameObject("ReadyLabel", typeof(TextMeshPro));
        readyLabelGO.transform.SetParent(root.transform, false);
        readyLabelGO.transform.localPosition = new Vector3(0, 3.1f, 0);
        var readyLabel = readyLabelGO.GetComponent<TextMeshPro>();
        readyLabel.alignment = TextAlignmentOptions.Center;
        readyLabel.fontSize = 4;
        readyLabel.color = Color.green;
        readyLabel.text = "READY";

        var view = root.GetComponent<LobbySlotView>();
        SetField(view, "nameText", nameTag);
        SetField(view, "readyLabel", readyLabelGO);
        readyLabelGO.SetActive(false);

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        UnityEngine.Object.DestroyImmediate(root);
        return prefab;
    }

    // ─── Seat anchors ────────────────────────────────────────────────────

    static void BuildAnchors(GameObject anchorsGO)
    {
        var anchors = anchorsGO.GetComponent<LobbySeatAnchors>();

        var center = GetOrCreateChildTransform(anchorsGO.transform, "CenterAnchor", new Vector3(0, 0, 0));
        var other1 = GetOrCreateChildTransform(anchorsGO.transform, "OtherAnchor1", new Vector3(-2.5f, 0, 2.5f));
        var other2 = GetOrCreateChildTransform(anchorsGO.transform, "OtherAnchor2", new Vector3(2.5f, 0, 2.5f));
        var other3 = GetOrCreateChildTransform(anchorsGO.transform, "OtherAnchor3", new Vector3(0, 0, 4.5f));

        SetField(anchors, "centerAnchor", center);
        SetObjectArray(anchors, "otherAnchors", new UnityEngine.Object[] { other1, other2, other3 });
    }

    static Transform GetOrCreateChildTransform(Transform parent, string name, Vector3 localPos)
    {
        var existing = parent.Find(name);
        if (existing != null) return existing;

        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        Undo.RegisterCreatedObjectUndo(go, UndoLabel);
        return go.transform;
    }

    // ─── Screen ──────────────────────────────────────────────────────────

    class ScreenRefs
    {
        public GameObject idlePanel, searchingPanel, soloBotPanel, readyPanel, startingPanel;
        public Button findMatchButton, startWithBotsButton, readyToggleButton, cancelButton;
        public TMP_Text searchingText, readyToggleLabel;
    }

    static void BuildScreen(RectTransform canvas, out ScreenRefs refs)
    {
        refs = new ScreenRefs();

        var root = GetOrCreateUI("MatchmakingScreen", canvas, typeof(MatchmakingScreen));
        Stretch(root);

        var idle = Panel("IdlePanel", root);
        var idleContent = VList("Content", idle);
        refs.findMatchButton = Btn("FindMatchButton", idleContent, "Find Match");
        refs.idlePanel = idle.gameObject;

        var searching = Panel("SearchingPanel", root);
        var searchingContent = VList("Content", searching);
        refs.searchingText = TextEl("SearchingText", searchingContent, "Finding player... 0s", 32);

        var soloBot = GetOrCreateUI("SoloBotPanel", searchingContent, typeof(VerticalLayoutGroup));
        refs.startWithBotsButton = Btn("StartWithBotsButton", soloBot, "Don't wait, start with computer players");
        refs.soloBotPanel = soloBot.gameObject;

        var ready = GetOrCreateUI("ReadyPanel", searchingContent, typeof(VerticalLayoutGroup));
        refs.readyToggleButton = Btn("ReadyToggleButton", ready, "Don't wait, play with computer players");
        refs.readyToggleLabel = refs.readyToggleButton.GetComponentInChildren<TMP_Text>();
        refs.readyPanel = ready.gameObject;

        refs.cancelButton = Btn("CancelButton", searchingContent, "Cancel");
        refs.searchingPanel = searching.gameObject;

        var starting = Panel("StartingPanel", root);
        var startingContent = VList("Content", starting);
        TextEl("StartingText", startingContent, "Starting...", 40);
        refs.startingPanel = starting.gameObject;
    }

    static void WireScreen(RectTransform canvas, ScreenRefs refs, MatchmakingFlowController flowController)
    {
        var screen = canvas.Find("MatchmakingScreen").GetComponent<MatchmakingScreen>();

        SetField(screen, "flowController", flowController);
        SetField(screen, "idlePanel", refs.idlePanel);
        SetField(screen, "findMatchButton", refs.findMatchButton);
        SetField(screen, "searchingPanel", refs.searchingPanel);
        SetField(screen, "searchingText", refs.searchingText);
        SetField(screen, "soloBotPanel", refs.soloBotPanel);
        SetField(screen, "startWithBotsButton", refs.startWithBotsButton);
        SetField(screen, "readyPanel", refs.readyPanel);
        SetField(screen, "readyToggleButton", refs.readyToggleButton);
        SetField(screen, "readyToggleLabel", refs.readyToggleLabel);
        SetField(screen, "startingPanel", refs.startingPanel);
        SetField(screen, "cancelButton", refs.cancelButton);
    }

    // ─── Low-level UI builders (self-contained; mirrors the shelved LobbyUISceneSetup.cs style) ───

    static RectTransform Panel(string name, Transform parent)
    {
        var rt = GetOrCreateUI(name, parent, typeof(Image));
        Stretch(rt);
        rt.GetComponent<Image>().color = PanelColor;
        return rt;
    }

    static RectTransform VList(string name, Transform parent, float width = 640, float spacing = 18)
    {
        var rt = GetOrCreateUI(name, parent, typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(width, 0);
        rt.anchoredPosition = Vector2.zero;

        var vlg = rt.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = spacing;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        rt.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return rt;
    }

    static TMP_Text TextEl(string name, Transform parent, string content, float size, float height = 48)
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

    static Button Btn(string name, Transform parent, string label, float height = 56)
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
        tmp.fontSize = 24;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;

        var button = rt.GetComponent<Button>();
        button.targetGraphic = img;
        return button;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static RectTransform GetOrCreateUI(string name, Transform parent, params Type[] components)
    {
        var existing = parent != null ? parent.Find(name) : null;
        if (existing != null) return existing.GetComponent<RectTransform>();

        var types = new Type[components.Length + 1];
        types[0] = typeof(RectTransform);
        Array.Copy(components, 0, types, 1, components.Length);

        var go = new GameObject(name, types);
        go.transform.SetParent(parent, false);
        Undo.RegisterCreatedObjectUndo(go, UndoLabel);
        return go.GetComponent<RectTransform>();
    }

    static GameObject GetOrCreatePlain(string name, Transform parent, params Type[] components)
    {
        Transform existing = parent != null ? parent.Find(name) : GameObject.Find(name)?.transform;
        if (existing != null) return existing.gameObject;

        var go = new GameObject(name, components);
        if (parent != null) go.transform.SetParent(parent, false);
        Undo.RegisterCreatedObjectUndo(go, UndoLabel);
        return go;
    }

    static RectTransform GetOrCreateCanvasUI()
    {
        var existing = GameObject.Find("MatchmakingCanvas");
        if (existing != null) return existing.GetComponent<RectTransform>();

        var go = new GameObject("MatchmakingCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(go, UndoLabel);

        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        return go.GetComponent<RectTransform>();
    }

    static void EnsureEventSystem()
    {
        if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() != null) return;

        var go = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
        go.AddComponent<InputSystemUIInputModule>();
#else
        go.AddComponent<StandaloneInputModule>();
#endif
        Undo.RegisterCreatedObjectUndo(go, UndoLabel);
    }

    static void EnsureFolders()
    {
        EnsureFolder("Assets/_DEV/Matchmaking", "Scenes");
        EnsureFolder("Assets/_DEV/Matchmaking", "Resources");
        EnsureFolder("Assets/_DEV/Matchmaking", "Prefabs");
    }

    static void EnsureFolder(string parent, string name)
    {
        string path = $"{parent}/{name}";
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, name);
    }

    static void SetField(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
    {
        var so = new SerializedObject(target);
        var prop = so.FindProperty(propertyName);
        if (prop == null)
        {
            Debug.LogError($"[MatchmakingSceneSetup] Field '{propertyName}' not found on {target.GetType().Name}.");
            return;
        }
        prop.objectReferenceValue = value;
        so.ApplyModifiedProperties();
    }

    static void SetObjectArray(UnityEngine.Object target, string propertyName, UnityEngine.Object[] values)
    {
        var so = new SerializedObject(target);
        var prop = so.FindProperty(propertyName);
        prop.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        so.ApplyModifiedProperties();
    }
}
