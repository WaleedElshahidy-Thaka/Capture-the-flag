using System.Collections.Generic;
using UnityEngine;

// Shared by CarRobotAppearance (networked cars) and LobbySeatPreview (lobby stand-ins):
// enables only the child of `botsParent` named like the robot (names exactly as in BotSkinLibrary)
// and dresses it with the shared BotSkins.Apply. Children not named after a library robot are left alone.
public static class RobotSwitcher
{
    // Returns the robot now shown, or null when botName isn't under botsParent (nothing is changed then).
    public static GameObject Show(Transform botsParent, BotSkinLibrary library, string botName, string skinName)
    {
        if (botsParent == null || library == null || string.IsNullOrEmpty(botName)) return null;

        GameObject active = null;
        var robots = new List<GameObject>();
        var known = new HashSet<string>(library.BotNamesInLibrary());
        foreach (Transform child in botsParent)
        {
            if (!known.Contains(child.name)) continue;
            robots.Add(child.gameObject);
            if (child.name == botName) active = child.gameObject;
        }

        if (active == null)
        {
            Debug.LogWarning($"[RobotSwitcher] No robot '{botName}' under '{botsParent.name}'; keeping the current one.");
            return null;
        }

        foreach (var robot in robots) robot.SetActive(robot == active);
        BotSkins.Apply(library, botName, skinName, active);
        return active;
    }
}
