using UnityEngine;

// Shows this car's robot + skin from its PlayerLobbyProfile (networked), on every peer:
//   - shows that robot under `botsParent`, dressed in its skin (RobotSwitcher),
//   - points PlayerMovement's wheel animation and CarSkidmarks at that robot's RobotRig.
// It is the per-car, networked counterpart of BotSelection (which reads the single local --bot and
// so can only be used for the local-only lobby preview car).
//
// Visual only. Acts on changes, so the skin's material instances are made once per change.
// Until the profile arrives (or for an unknown robot) the robot as authored in the prefab stays.
// Put it on the PlayerCar root.
public class CarRobotAppearance : MonoBehaviour
{
    [SerializeField] PlayerLobbyProfile profile;
    [Tooltip("The 'Bots' object under View that holds one child per lobby robot.")]
    [SerializeField] Transform botsParent;
    [SerializeField] BotSkinLibrary skinLibrary;
    [SerializeField] PlayerMovement movement;
    [SerializeField] CarSkidmarks skidmarks;

    string shownBot, shownSkin;

    void Awake()
    {
        if (profile == null) profile = GetComponent<PlayerLobbyProfile>();
        if (movement == null) movement = GetComponent<PlayerMovement>();
        if (skidmarks == null) skidmarks = GetComponent<CarSkidmarks>();
    }

    void LateUpdate()
    {
        if (profile == null || botsParent == null || skinLibrary == null) return;
        if (profile.Object == null || !profile.Object.IsValid) return;

        string bot = profile.BotName.ToString();
        string skin = profile.SkinName.ToString();
        if (string.IsNullOrEmpty(bot) || (bot == shownBot && skin == shownSkin)) return;

        shownBot = bot;
        shownSkin = skin;
        Apply(bot, skin);
    }

    void Apply(string botName, string skinName)
    {
        GameObject active = RobotSwitcher.Show(botsParent, skinLibrary, botName, skinName);
        if (active == null) return;

        var rig = active.GetComponent<RobotRig>();
        if (rig == null)
        {
            Debug.LogWarning($"[CarRobotAppearance] Robot '{botName}' has no RobotRig; its wheels won't animate.");
            return;
        }
        if (movement != null) movement.SetTireVisuals(rig.frontLeft, rig.frontRight, rig.rearLeft, rig.rearRight);
        if (skidmarks != null) skidmarks.SetWheels(rig.skidmarkWheels);
    }
}
