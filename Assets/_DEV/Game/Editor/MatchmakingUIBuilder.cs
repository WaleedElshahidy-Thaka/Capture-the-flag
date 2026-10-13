using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Builds the matchmaking canvas (one side panel per phase, two host-migration overlays, the
// fade), the local preview robot, and wires them to MatchmakingScreen / PlayerCamera.
static class MatchmakingUIBuilder
{
    class ScreenRefs
    {
        public GameObject idlePanel, searchingPanel, foundPanel, soloBotPanel, readyPanel, reconnectingPanel, resumingPanel, previewCar;
        public ScreenFade fade;
        public Button quickMatchButton, startWithBotsButton, readyToggleButton, cancelButton;
        public TMP_Text searchingText, playersFoundText, foundText, readyToggleLabel, resumingText;
    }

    public static void Build(MatchmakingFlowController flowController)
    {
        SetupUI.EnsureEventSystem();
        var canvas = SetupUI.GetOrCreateCanvas("MatchmakingCanvas");

        var refs = BuildScreen(canvas);
        WireScreen(canvas, refs, flowController);
        WireLobbyPresentation(flowController, refs.previewCar, refs.fade);
    }

    static ScreenRefs BuildScreen(RectTransform canvas)
    {
        var refs = new ScreenRefs();

        var root = SetupUI.GetOrCreateUI("MatchmakingScreen", canvas, typeof(MatchmakingScreen));
        SetupUI.Stretch(root);
        RemoveLegacyChildren(root);

        refs.previewCar = BuildPreviewCar();

        var idle = SetupUI.SidePanel("IdlePanel", root);
        refs.quickMatchButton = SetupUI.CreateButton("QuickMatchButton", idle, "Quick Match");
        refs.idlePanel = idle.gameObject;

        var searching = SetupUI.SidePanel("SearchingPanel", root);
        refs.searchingText = SetupUI.CreateText("SearchingText", searching, "Searching for players... 0s", 20, 36);
        refs.playersFoundText = SetupUI.CreateText("PlayersFoundText", searching, "2 / 6 players found", 18, 32);
        var soloBot = SetupUI.GetOrCreateUI("SoloBotPanel", searching, typeof(VerticalLayoutGroup));
        refs.startWithBotsButton = SetupUI.CreateButton("StartWithBotsButton", soloBot, "Start with computer players", 44, 18);
        refs.soloBotPanel = soloBot.gameObject;
        var ready = SetupUI.GetOrCreateUI("ReadyPanel", searching, typeof(VerticalLayoutGroup));
        refs.readyToggleButton = SetupUI.CreateButton("ReadyToggleButton", ready, "Play with computer players", 44, 18);
        refs.readyToggleLabel = refs.readyToggleButton.GetComponentInChildren<TMP_Text>();
        refs.readyPanel = ready.gameObject;
        refs.searchingPanel = searching.gameObject;

        var found = SetupUI.SidePanel("FoundPanel", root);
        refs.foundText = SetupUI.CreateText("FoundText", found, "Player found! Joining lobby in 3", 22, 36);
        refs.foundPanel = found.gameObject;

        var cancelBox = SetupUI.SidePanel("CancelPanel", root, anchoredY: -120f);
        refs.cancelButton = SetupUI.CreateButton("CancelButton", cancelBox, "Cancel", 40);
        cancelBox.GetComponent<Image>().enabled = false;

        // Host-migration overlays are dimmed, not black, so the frozen cars stay visible.
        var dim = new Color(0f, 0f, 0f, 0.6f);
        var reconnecting = SetupUI.FullScreenPanel("ReconnectingPanel", root, dim, "Host disconnected - reconnecting...", out _);
        refs.reconnectingPanel = reconnecting.gameObject;
        var resuming = SetupUI.FullScreenPanel("ResumingPanel", root, dim, "Game resumes in 3", out refs.resumingText);
        refs.resumingPanel = resuming.gameObject;

        // Draw order: side panels < overlays < fade.
        reconnecting.SetAsLastSibling();
        resuming.SetAsLastSibling();

        var fadeRt = SetupUI.GetOrCreateUI("ScreenFade", root, typeof(Image), typeof(ScreenFade));
        SetupUI.Stretch(fadeRt);
        fadeRt.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
        fadeRt.SetAsLastSibling();
        refs.fade = fadeRt.GetComponent<ScreenFade>();

        return refs;
    }

    // The local robot in seat 0 with your name over it (see LobbyPreviewCar). No physics.
    static GameObject BuildPreviewCar()
    {
        SetupUtils.DestroyIfExists("LobbyPreviewCar");

        var go = new GameObject("LobbyPreviewCar", typeof(LobbyPreviewCar));
        Undo.RegisterCreatedObjectUndo(go, SetupUtils.UndoLabel);
        go.transform.position = LobbyLayout.SlotPosition(0);
        go.transform.rotation = LobbyLayout.SlotRotation;

        // Zeroed so the robot sits on the root the camera and name tag are centred on.
        var visual = SetupUtils.InstantiateRobotArt(go.transform, out _);
        visual.transform.localPosition = Vector3.zero;
        foreach (var collider in visual.GetComponentsInChildren<Collider>())
            Object.DestroyImmediate(collider);

        var tagGO = SetupUI.WorldNameTag(go.transform, out var nameLabel);

        var preview = go.GetComponent<LobbyPreviewCar>();
        SetupUtils.SetField(preview, "visual", visual);
        SetupUtils.SetField(preview, "nameTag", tagGO);
        SetupUtils.SetField(preview, "nameLabel", nameLabel);
        return go;
    }

    // Objects from earlier UI layouts that find-or-create would otherwise leave behind.
    static void RemoveLegacyChildren(Transform root)
    {
        foreach (Transform panel in root)
        {
            var content = panel.Find("Content");
            if (content != null) Object.DestroyImmediate(content.gameObject);
        }

        foreach (var path in new[] { "SearchingPanel/CancelButton", "IdlePanel/LoadingText", "IdlePanel/FindMatchButton", "LobbyPanel", "StartingPanel", "LoadingPanel" })
        {
            var legacy = root.Find(path);
            if (legacy != null) Object.DestroyImmediate(legacy.gameObject);
        }
    }

    static void WireScreen(RectTransform canvas, ScreenRefs refs, MatchmakingFlowController flowController)
    {
        var screen = canvas.Find("MatchmakingScreen").GetComponent<MatchmakingScreen>();

        SetupUtils.SetField(screen, "flowController", flowController);
        SetupUtils.SetField(screen, "idlePanel", refs.idlePanel);
        SetupUtils.SetField(screen, "quickMatchButton", refs.quickMatchButton);
        SetupUtils.SetField(screen, "searchingPanel", refs.searchingPanel);
        SetupUtils.SetField(screen, "searchingText", refs.searchingText);
        SetupUtils.SetField(screen, "playersFoundText", refs.playersFoundText);
        SetupUtils.SetField(screen, "foundPanel", refs.foundPanel);
        SetupUtils.SetField(screen, "foundText", refs.foundText);
        SetupUtils.SetField(screen, "soloBotPanel", refs.soloBotPanel);
        SetupUtils.SetField(screen, "startWithBotsButton", refs.startWithBotsButton);
        SetupUtils.SetField(screen, "readyPanel", refs.readyPanel);
        SetupUtils.SetField(screen, "readyToggleButton", refs.readyToggleButton);
        SetupUtils.SetField(screen, "readyToggleLabel", refs.readyToggleLabel);
        SetupUtils.SetField(screen, "cancelButton", refs.cancelButton);
        SetupUtils.SetField(screen, "reconnectingPanel", refs.reconnectingPanel);
        SetupUtils.SetField(screen, "resumingPanel", refs.resumingPanel);
        SetupUtils.SetField(screen, "resumingText", refs.resumingText);
    }

    // The camera and the preview robot both read "solo or lobby view" from the flow controller.
    static void WireLobbyPresentation(MatchmakingFlowController flowController, GameObject previewCar, ScreenFade fade)
    {
        SetupUtils.SetField(previewCar.GetComponent<LobbyPreviewCar>(), "flow", flowController);

        var cameraGO = GameObject.Find(SetupUtils.MainCameraName);
        if (cameraGO == null || !cameraGO.TryGetComponent(out PlayerCamera playerCamera)) return;
        SetupUtils.SetField(playerCamera, "flow", flowController);
        SetupUtils.SetField(playerCamera, "previewCar", previewCar.transform);
        SetupUtils.SetField(playerCamera, "fade", fade);
    }
}
