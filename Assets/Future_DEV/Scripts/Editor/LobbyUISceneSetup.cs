using System;
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

// Builds the lobby UI hierarchy into the project's one scene (CaptureTheFlag.unity) and
// wires every controller's [SerializeField] reference. Exists because there's no way to
// drive the Unity Editor GUI directly from here — everything it produces is a normal,
// hand-editable scene afterwards, not runtime-procedural UI.
//
// Idempotent: every element is found-by-name before being created, so re-running after
// editing script logic (not hierarchy) is safe. Re-running after manually renaming/deleting
// pieces in the Editor is not guaranteed safe — the found-by-path wiring below assumes the
// hierarchy this tool itself produced.
public static class LobbyUISceneSetup
{
    const string ScenePath = "Assets/_DEV/CaptureTheFlag.unity";
    const string UndoLabel = "Setup Lobby Scene";

    static readonly Color PanelColor = new Color(0.08f, 0.09f, 0.12f, 0.96f);
    static readonly Color ButtonColor = new Color(0.18f, 0.22f, 0.30f, 1f);
    static readonly Color FieldColor = new Color(0.14f, 0.16f, 0.20f, 1f);
    static readonly Color AccentColor = new Color(0.30f, 0.85f, 0.40f, 1f);

    [MenuItem("Multiplayer/Setup Lobby Scene")]
    public static void SetupScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[LobbyUISceneSetup] Exit Play Mode before running this.");
            return;
        }

        if (SceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        EnsureEventSystem();
        var canvas = GetOrCreateCanvas();

        var entry = BuildEntryScreen(canvas);
        var createRoom = BuildCreateRoomScreen(canvas);
        var waitingRoom = BuildWaitingRoomScreen(canvas);
        var joinRoom = BuildJoinRoomScreen(canvas);
        var joinByCode = BuildJoinByCodeScreen(canvas);
        var browseRooms = BuildBrowseRoomsScreen(canvas);
        var errorBanner = BuildErrorBanner(canvas);

        var navigatorGO = GetOrCreate("Navigator", canvas, typeof(ScreenNavigator)).gameObject;
        var navigator = navigatorGO.GetComponent<ScreenNavigator>();
        SetObjectArray(navigator, "screens", new[]
        {
            entry.gameObject, createRoom.gameObject, waitingRoom.gameObject,
            joinRoom.gameObject, joinByCode.gameObject, browseRooms.gameObject
        });

        var waitingRoomComp = waitingRoom.GetComponent<WaitingRoomScreen>();

        WireEntryScreen(entry, navigator, createRoom.gameObject, joinRoom.gameObject);
        WireCreateRoomScreen(createRoom, navigator, entry.gameObject, waitingRoom.gameObject, waitingRoomComp, errorBanner);
        WireWaitingRoomScreen(waitingRoomComp, navigator, entry.gameObject);
        WireJoinRoomScreen(joinRoom, navigator, entry.gameObject, joinByCode.gameObject, browseRooms.gameObject);
        WireJoinByCodeScreen(joinByCode, navigator, joinRoom.gameObject, waitingRoom.gameObject, waitingRoomComp, errorBanner);
        WireBrowseRoomsScreen(browseRooms, navigator, joinRoom.gameObject, waitingRoom.gameObject, waitingRoomComp, errorBanner);

        entry.gameObject.SetActive(true);
        createRoom.gameObject.SetActive(false);
        waitingRoom.gameObject.SetActive(false);
        joinRoom.gameObject.SetActive(false);
        joinByCode.gameObject.SetActive(false);
        browseRooms.gameObject.SetActive(false);

        var activeScene = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);

        Debug.Log("[LobbyUISceneSetup] Lobby scene built: Entry → Create Room / Join Room → Waiting Room. " +
                   "Backend is LocalRoomService (see Services.cs) — press Play to test the full flow.");
    }

    // ─── Screen builders ─────────────────────────────────────────────────

    static RectTransform BuildEntryScreen(Transform canvas)
    {
        var panel = Panel("EntryScreen", canvas);
        var content = VList("Content", panel);
        Text("Title", content, "Capture the Flag", 56, TextAlignmentOptions.Center, 80);
        Btn("CreateRoomButton", content, "Create Room");
        Btn("JoinRoomButton", content, "Join Room");

        AddComponentIfMissing<CtfEntryScreen>(panel.gameObject);
        return panel;
    }

    static RectTransform BuildCreateRoomScreen(Transform canvas)
    {
        var panel = Panel("CreateRoomScreen", canvas);
        var content = VList("Content", panel, 700);
        Text("Title", content, "Create Room", 44, TextAlignmentOptions.Center, 64);
        InputField("RoomNameField", content, "Room name");

        var countRow = HRow("PlayerCountRow", content);
        var countGroup = GetOrCreateToggleGroup(countRow);
        CreateToggle("Players4Toggle", countRow, "4 Players", countGroup);
        CreateToggle("Players6Toggle", countRow, "6 Players", countGroup);
        CreateToggle("Players8Toggle", countRow, "8 Players", countGroup);

        var visRow = HRow("VisibilityRow", content);
        var visGroup = GetOrCreateToggleGroup(visRow);
        CreateToggle("VisibleToggle", visRow, "Visible", visGroup, 300);
        CreateToggle("InvisibleToggle", visRow, "Invisible", visGroup, 300);

        Btn("CreateButton", content, "Create");
        Btn("BackButton", content, "Back");

        AddComponentIfMissing<CreateRoomScreen>(panel.gameObject);
        return panel;
    }

    static RectTransform BuildWaitingRoomScreen(Transform canvas)
    {
        var panel = Panel("WaitingRoomScreen", canvas);
        var content = VList("Content", panel, 700);
        Text("RoomNameText", content, "Room Name", 40, TextAlignmentOptions.Center, 56);
        Text("PlayerCountText", content, "Players: 0/0", 28, TextAlignmentOptions.Center, 44);

        var codeRow = HRow("RoomCodePanel", content, 56, 16, expandChildren: false);
        Text("RoomCodeText", codeRow, "Room Code: -----", 26, TextAlignmentOptions.MidlineLeft, 44)
            .GetComponent<LayoutElement>().preferredWidth = 360;
        Btn("MakeVisibleButton", codeRow, "Make it visible");

        Btn("StartButton", content, "Start");
        Btn("LeaveButton", content, "Leave");

        AddComponentIfMissing<WaitingRoomScreen>(panel.gameObject);
        return panel;
    }

    static RectTransform BuildJoinRoomScreen(Transform canvas)
    {
        var panel = Panel("JoinRoomScreen", canvas);
        var content = VList("Content", panel);
        Text("Title", content, "Join Room", 44, TextAlignmentOptions.Center, 64);
        Btn("JoinByCodeButton", content, "Join Using a Code");
        Btn("BrowseRoomsButton", content, "Search for Available Seat");
        Btn("BackButton", content, "Back");

        AddComponentIfMissing<JoinRoomScreen>(panel.gameObject);
        return panel;
    }

    static RectTransform BuildJoinByCodeScreen(Transform canvas)
    {
        var panel = Panel("JoinByCodeScreen", canvas);
        var content = VList("Content", panel, 700);
        Text("Title", content, "Join by Code", 44, TextAlignmentOptions.Center, 64);
        InputField("CodeField", content, "Room code");
        Btn("JoinButton", content, "Join");
        Btn("BackButton", content, "Back");

        AddComponentIfMissing<JoinByCodeScreen>(panel.gameObject);
        return panel;
    }

    static RectTransform BuildBrowseRoomsScreen(Transform canvas)
    {
        var panel = Panel("BrowseRoomsScreen", canvas);
        var content = VList("Content", panel, 820);
        Text("Title", content, "Available Rooms", 44, TextAlignmentOptions.Center, 64);

        var scrollRt = GetOrCreate("ScrollView", content, typeof(Image), typeof(ScrollRect), typeof(LayoutElement));
        scrollRt.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.25f);
        var scrollLe = scrollRt.GetComponent<LayoutElement>();
        scrollLe.preferredHeight = 420;
        scrollLe.minHeight = 420;

        var viewportRt = GetOrCreate("Viewport", scrollRt, typeof(Image), typeof(Mask));
        Stretch(viewportRt);
        viewportRt.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.01f);
        viewportRt.GetComponent<Mask>().showMaskGraphic = false;

        var listContentRt = GetOrCreate("ListContent", viewportRt, typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        listContentRt.anchorMin = new Vector2(0f, 1f);
        listContentRt.anchorMax = new Vector2(1f, 1f);
        listContentRt.pivot = new Vector2(0.5f, 1f);
        listContentRt.anchoredPosition = Vector2.zero;
        listContentRt.sizeDelta = Vector2.zero;
        var listVlg = listContentRt.GetComponent<VerticalLayoutGroup>();
        listVlg.spacing = 10;
        listVlg.padding = new RectOffset(8, 8, 8, 8);
        listVlg.childControlWidth = true;
        listVlg.childControlHeight = false;
        listVlg.childForceExpandWidth = true;
        listVlg.childForceExpandHeight = false;
        var listFitter = listContentRt.GetComponent<ContentSizeFitter>();
        listFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scrollRect = scrollRt.GetComponent<ScrollRect>();
        scrollRect.viewport = viewportRt;
        scrollRect.content = listContentRt;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;

        Btn("RefreshButton", content, "Refresh");
        Btn("BackButton", content, "Back");

        BuildRoomListCardTemplate(panel);

        AddComponentIfMissing<BrowseRoomsScreen>(panel.gameObject);
        return panel;
    }

    static void BuildRoomListCardTemplate(Transform panel)
    {
        var cardRt = GetOrCreate("RoomCardTemplate", panel, typeof(Image), typeof(LayoutElement));
        cardRt.GetComponent<Image>().color = FieldColor;
        var cardLe = cardRt.GetComponent<LayoutElement>();
        cardLe.preferredHeight = 64;
        cardLe.minHeight = 64;

        var row = HRow("Row", cardRt, 64, 12, expandChildren: true);
        Stretch(row);

        var nameText = Text("RoomNameText", row, "Room Name", 24, TextAlignmentOptions.MidlineLeft, 44);
        nameText.GetComponent<LayoutElement>().flexibleWidth = 2;

        var countText = Text("PlayerCountText", row, "0/0", 22, TextAlignmentOptions.Center, 44);
        countText.GetComponent<LayoutElement>().flexibleWidth = 1;

        var joinBtn = Btn("JoinButton", row, "Join", 44);
        joinBtn.GetComponent<LayoutElement>().flexibleWidth = 1;

        var card = AddComponentIfMissing<RoomListCard>(cardRt.gameObject);
        SetField(card, "roomNameText", nameText);
        SetField(card, "playerCountText", countText);
        SetField(card, "joinButton", joinBtn);

        cardRt.gameObject.SetActive(false);
    }

    static TimedErrorBanner BuildErrorBanner(Transform canvas)
    {
        var rt = GetOrCreate("ErrorBanner", canvas, typeof(Image));
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(760, 70);
        rt.anchoredPosition = new Vector2(0, -40);
        rt.GetComponent<Image>().color = new Color(0.6f, 0.15f, 0.15f, 0.95f);

        var textRt = GetOrCreate("MessageText", rt, typeof(TextMeshProUGUI));
        Stretch(textRt);
        var tmp = textRt.GetComponent<TextMeshProUGUI>();
        tmp.text = "Error";
        tmp.fontSize = 24;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;

        var banner = AddComponentIfMissing<TimedErrorBanner>(rt.gameObject);
        SetField(banner, "panel", rt.gameObject);
        SetField(banner, "messageText", tmp);

        rt.gameObject.SetActive(false);
        return banner;
    }

    // ─── Wiring ──────────────────────────────────────────────────────────

    static void WireEntryScreen(RectTransform panel, ScreenNavigator navigator, GameObject createRoomScreen, GameObject joinRoomScreen)
    {
        var comp = panel.GetComponent<CtfEntryScreen>();
        SetField(comp, "navigator", navigator);
        SetField(comp, "createRoomScreen", createRoomScreen);
        SetField(comp, "joinRoomScreen", joinRoomScreen);
        SetField(comp, "createRoomButton", panel.Find("Content/CreateRoomButton").GetComponent<Button>());
        SetField(comp, "joinRoomButton", panel.Find("Content/JoinRoomButton").GetComponent<Button>());
    }

    static void WireCreateRoomScreen(RectTransform panel, ScreenNavigator navigator, GameObject entryScreen, GameObject waitingRoomScreen, WaitingRoomScreen waitingRoom, TimedErrorBanner errorBanner)
    {
        var comp = panel.GetComponent<CreateRoomScreen>();
        SetField(comp, "navigator", navigator);
        SetField(comp, "entryScreen", entryScreen);
        SetField(comp, "waitingRoomScreen", waitingRoomScreen);
        SetField(comp, "waitingRoom", waitingRoom);
        SetField(comp, "errorBanner", errorBanner);
        SetField(comp, "roomNameField", panel.Find("Content/RoomNameField").GetComponent<TMP_InputField>());
        SetField(comp, "players4Toggle", panel.Find("Content/PlayerCountRow/Players4Toggle").GetComponent<Toggle>());
        SetField(comp, "players6Toggle", panel.Find("Content/PlayerCountRow/Players6Toggle").GetComponent<Toggle>());
        SetField(comp, "players8Toggle", panel.Find("Content/PlayerCountRow/Players8Toggle").GetComponent<Toggle>());
        SetField(comp, "visibleToggle", panel.Find("Content/VisibilityRow/VisibleToggle").GetComponent<Toggle>());
        SetField(comp, "invisibleToggle", panel.Find("Content/VisibilityRow/InvisibleToggle").GetComponent<Toggle>());
        SetField(comp, "createButton", panel.Find("Content/CreateButton").GetComponent<Button>());
        SetField(comp, "backButton", panel.Find("Content/BackButton").GetComponent<Button>());
    }

    static void WireWaitingRoomScreen(WaitingRoomScreen comp, ScreenNavigator navigator, GameObject entryScreen)
    {
        var panel = comp.transform;
        SetField(comp, "navigator", navigator);
        SetField(comp, "entryScreen", entryScreen);
        SetField(comp, "roomNameText", panel.Find("Content/RoomNameText").GetComponent<TMP_Text>());
        SetField(comp, "playerCountText", panel.Find("Content/PlayerCountText").GetComponent<TMP_Text>());
        SetField(comp, "roomCodePanel", panel.Find("Content/RoomCodePanel").gameObject);
        SetField(comp, "roomCodeText", panel.Find("Content/RoomCodePanel/RoomCodeText").GetComponent<TMP_Text>());
        SetField(comp, "makeVisibleButton", panel.Find("Content/RoomCodePanel/MakeVisibleButton").GetComponent<Button>());
        SetField(comp, "startButton", panel.Find("Content/StartButton").GetComponent<Button>());
        SetField(comp, "leaveButton", panel.Find("Content/LeaveButton").GetComponent<Button>());
    }

    static void WireJoinRoomScreen(RectTransform panel, ScreenNavigator navigator, GameObject entryScreen, GameObject joinByCodeScreen, GameObject browseRoomsScreen)
    {
        var comp = panel.GetComponent<JoinRoomScreen>();
        SetField(comp, "navigator", navigator);
        SetField(comp, "entryScreen", entryScreen);
        SetField(comp, "joinByCodeScreen", joinByCodeScreen);
        SetField(comp, "browseRoomsScreen", browseRoomsScreen);
        SetField(comp, "joinByCodeButton", panel.Find("Content/JoinByCodeButton").GetComponent<Button>());
        SetField(comp, "browseRoomsButton", panel.Find("Content/BrowseRoomsButton").GetComponent<Button>());
        SetField(comp, "backButton", panel.Find("Content/BackButton").GetComponent<Button>());
    }

    static void WireJoinByCodeScreen(RectTransform panel, ScreenNavigator navigator, GameObject joinRoomScreen, GameObject waitingRoomScreen, WaitingRoomScreen waitingRoom, TimedErrorBanner errorBanner)
    {
        var comp = panel.GetComponent<JoinByCodeScreen>();
        SetField(comp, "navigator", navigator);
        SetField(comp, "joinRoomScreen", joinRoomScreen);
        SetField(comp, "waitingRoomScreen", waitingRoomScreen);
        SetField(comp, "waitingRoom", waitingRoom);
        SetField(comp, "errorBanner", errorBanner);
        SetField(comp, "codeField", panel.Find("Content/CodeField").GetComponent<TMP_InputField>());
        SetField(comp, "joinButton", panel.Find("Content/JoinButton").GetComponent<Button>());
        SetField(comp, "backButton", panel.Find("Content/BackButton").GetComponent<Button>());
    }

    static void WireBrowseRoomsScreen(RectTransform panel, ScreenNavigator navigator, GameObject joinRoomScreen, GameObject waitingRoomScreen, WaitingRoomScreen waitingRoom, TimedErrorBanner errorBanner)
    {
        var comp = panel.GetComponent<BrowseRoomsScreen>();
        SetField(comp, "navigator", navigator);
        SetField(comp, "joinRoomScreen", joinRoomScreen);
        SetField(comp, "waitingRoomScreen", waitingRoomScreen);
        SetField(comp, "waitingRoom", waitingRoom);
        SetField(comp, "errorBanner", errorBanner);
        SetField(comp, "listContent", panel.Find("Content/ScrollView/Viewport/ListContent"));
        SetField(comp, "cardTemplate", panel.Find("RoomCardTemplate").GetComponent<RoomListCard>());
        SetField(comp, "refreshButton", panel.Find("Content/RefreshButton").GetComponent<Button>());
        SetField(comp, "backButton", panel.Find("Content/BackButton").GetComponent<Button>());
    }

    // ─── Low-level element builders ─────────────────────────────────────

    static RectTransform Panel(string name, Transform parent)
    {
        var rt = GetOrCreate(name, parent, typeof(Image));
        Stretch(rt);
        rt.GetComponent<Image>().color = PanelColor;
        return rt;
    }

    static RectTransform VList(string name, Transform parent, float width = 640, float spacing = 18)
    {
        var rt = GetOrCreate(name, parent, typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
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

        var fitter = rt.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        return rt;
    }

    static RectTransform HRow(string name, Transform parent, float height = 56, float spacing = 16, bool expandChildren = false)
    {
        var rt = GetOrCreate(name, parent, typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        var hlg = rt.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = spacing;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = expandChildren;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = expandChildren;
        hlg.childForceExpandHeight = true;

        var le = rt.GetComponent<LayoutElement>();
        le.preferredHeight = height;

        return rt;
    }

    static TMP_Text Text(string name, Transform parent, string content, float size, TextAlignmentOptions align, float height)
    {
        var rt = GetOrCreate(name, parent, typeof(TextMeshProUGUI), typeof(LayoutElement));
        var tmp = rt.GetComponent<TextMeshProUGUI>();
        tmp.text = content;
        tmp.fontSize = size;
        tmp.alignment = align;
        tmp.color = Color.white;

        var le = rt.GetComponent<LayoutElement>();
        le.preferredHeight = height;
        le.minHeight = height;

        return tmp;
    }

    static Button Btn(string name, Transform parent, string label, float height = 56)
    {
        var rt = GetOrCreate(name, parent, typeof(Image), typeof(Button), typeof(LayoutElement));
        var img = rt.GetComponent<Image>();
        img.color = ButtonColor;

        var le = rt.GetComponent<LayoutElement>();
        le.preferredHeight = height;
        le.minHeight = height;

        var labelRt = GetOrCreate("Label", rt, typeof(TextMeshProUGUI));
        Stretch(labelRt);
        var tmp = labelRt.GetComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 26;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;

        var button = rt.GetComponent<Button>();
        button.targetGraphic = img;
        return button;
    }

    static TMP_InputField InputField(string name, Transform parent, string placeholder, float height = 56)
    {
        var rt = GetOrCreate(name, parent, typeof(Image), typeof(TMP_InputField), typeof(LayoutElement));
        var bg = rt.GetComponent<Image>();
        bg.color = FieldColor;

        var le = rt.GetComponent<LayoutElement>();
        le.preferredHeight = height;
        le.minHeight = height;

        var textAreaRt = GetOrCreate("Text Area", rt, typeof(RectMask2D));
        textAreaRt.anchorMin = Vector2.zero;
        textAreaRt.anchorMax = Vector2.one;
        textAreaRt.offsetMin = new Vector2(16, 6);
        textAreaRt.offsetMax = new Vector2(-16, -6);

        var placeholderRt = GetOrCreate("Placeholder", textAreaRt, typeof(TextMeshProUGUI));
        Stretch(placeholderRt);
        var placeholderTmp = placeholderRt.GetComponent<TextMeshProUGUI>();
        placeholderTmp.text = placeholder;
        placeholderTmp.fontSize = 24;
        placeholderTmp.fontStyle = FontStyles.Italic;
        placeholderTmp.color = new Color(1f, 1f, 1f, 0.4f);
        placeholderTmp.alignment = TextAlignmentOptions.MidlineLeft;

        var textRt = GetOrCreate("Text", textAreaRt, typeof(TextMeshProUGUI));
        Stretch(textRt);
        var textTmp = textRt.GetComponent<TextMeshProUGUI>();
        textTmp.fontSize = 24;
        textTmp.color = Color.white;
        textTmp.alignment = TextAlignmentOptions.MidlineLeft;

        var input = rt.GetComponent<TMP_InputField>();
        input.textViewport = textAreaRt;
        input.textComponent = textTmp;
        input.placeholder = placeholderTmp;
        input.targetGraphic = bg;

        return input;
    }

    static Toggle CreateToggle(string name, Transform parent, string label, ToggleGroup group, float width = 200, float height = 52)
    {
        var rt = GetOrCreate(name, parent, typeof(Image), typeof(Toggle), typeof(LayoutElement));
        var bg = rt.GetComponent<Image>();
        bg.color = FieldColor;

        var le = rt.GetComponent<LayoutElement>();
        le.preferredWidth = width;
        le.preferredHeight = height;

        var checkRt = GetOrCreate("Checkmark", rt, typeof(Image));
        checkRt.anchorMin = new Vector2(0f, 0.5f);
        checkRt.anchorMax = new Vector2(0f, 0.5f);
        checkRt.pivot = new Vector2(0f, 0.5f);
        checkRt.sizeDelta = new Vector2(24, 24);
        checkRt.anchoredPosition = new Vector2(10, 0);
        checkRt.GetComponent<Image>().color = AccentColor;

        var labelRt = GetOrCreate("Label", rt, typeof(TextMeshProUGUI));
        labelRt.anchorMin = new Vector2(0f, 0f);
        labelRt.anchorMax = new Vector2(1f, 1f);
        labelRt.offsetMin = new Vector2(44, 0);
        labelRt.offsetMax = new Vector2(-6, 0);
        var labelTmp = labelRt.GetComponent<TextMeshProUGUI>();
        labelTmp.text = label;
        labelTmp.fontSize = 22;
        labelTmp.alignment = TextAlignmentOptions.MidlineLeft;
        labelTmp.color = Color.white;

        var toggle = rt.GetComponent<Toggle>();
        toggle.targetGraphic = bg;
        toggle.graphic = checkRt.GetComponent<Image>();
        if (group != null) toggle.group = group;

        return toggle;
    }

    static ToggleGroup GetOrCreateToggleGroup(RectTransform row)
    {
        var tg = row.GetComponent<ToggleGroup>();
        if (tg == null) tg = row.gameObject.AddComponent<ToggleGroup>();
        return tg;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static RectTransform GetOrCreate(string name, Transform parent, params Type[] components)
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

    static T AddComponentIfMissing<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    static RectTransform GetOrCreateCanvas()
    {
        var existing = GameObject.Find("LobbyCanvas");
        if (existing != null) return existing.GetComponent<RectTransform>();

        var go = new GameObject("LobbyCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
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

    // ─── SerializedObject wiring helpers ────────────────────────────────

    static void SetField(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
    {
        var so = new SerializedObject(target);
        var prop = so.FindProperty(propertyName);
        if (prop == null)
        {
            Debug.LogError($"[LobbyUISceneSetup] Field '{propertyName}' not found on {target.GetType().Name}.");
            return;
        }
        prop.objectReferenceValue = value;
        so.ApplyModifiedProperties();
    }

    static void SetObjectArray(UnityEngine.Object target, string propertyName, GameObject[] values)
    {
        var so = new SerializedObject(target);
        var prop = so.FindProperty(propertyName);
        prop.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        so.ApplyModifiedProperties();
    }
}
