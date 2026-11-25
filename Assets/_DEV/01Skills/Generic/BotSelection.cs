using System;
using System.Collections.Generic;
using UnityEngine;

// Shows the one robot the player picked in the lobby (--bot=) and dresses it in the skin they
// picked (--skin=).
//
// Robots can be listed explicitly in `data`, or simply parented under `botsParent` and named
// after the robot — any child whose name matches an entry in the skin library counts. The
// by-name path exists so adding a robot to a game is just dragging its model in and naming it;
// forgetting to also add a list entry used to leave the robot permanently visible, on top of
// whichever robot the player had actually chosen.
//
// Duplicated verbatim in every game. Keep the copies identical.
public class BotSelection : MonoBehaviour
{
    [SerializeField] List<BotSelectionData> data;

    [Header("Discovery")]
    [Tooltip("Children named after a robot are treated as that robot. Defaults to this object.")]
    [SerializeField] Transform botsParent;

    [Header("Skins")]
    [Tooltip("Catalogue used to resolve --skin=. Leave empty to ignore skins entirely.")]
    [SerializeField] BotSkinLibrary skinLibrary;

    private void Start()
    {
        string selectedBot = DataTransferHandler.selectedBotName;

        List<KeyValuePair<string, GameObject>> bots = new List<KeyValuePair<string, GameObject>>(CollectBots());

        GameObject active = null;
        string activeName = selectedBot;
        foreach (KeyValuePair<string, GameObject> bot in bots)
        {
            bool isSelected = bot.Key == selectedBot;
            bot.Value.SetActive(isSelected);
            if (isSelected) active = bot.Value;
        }

        // A robot the lobby can offer but this scene has not had wired up yet would otherwise
        // leave the player looking at nothing at all. Showing the first robot instead is wrong,
        // but it is visibly wrong and still playable, where an invisible player is neither.
        if (active == null && bots.Count > 0)
        {
            Debug.LogWarning($"[BotSelection] No robot named '{selectedBot}' in this scene; " +
                             $"falling back to '{bots[0].Key}'. Wire '{selectedBot}' up to fix this.");
            active = bots[0].Value;
            activeName = bots[0].Key;
            active.SetActive(true);
        }

        if (active == null)
        {
            Debug.LogWarning("[BotSelection] No robots are wired up at all; nothing to show.");
            return;
        }

        // Keyed on the robot actually on screen, not the one that was asked for — skins are
        // per-robot, so a fallback robot must be dressed from its own list.
        BotSkins.Apply(skinLibrary, activeName, DataTransferHandler.selectedSkinName, active);
    }

    // Explicit entries keep their own botName, which is the value --bot= is matched against and
    // is not required to equal the GameObject's name. Discovered children are keyed by their
    // name, which is the only thing identifying them. A robot reachable both ways is returned
    // once, so it is never activated by one pass and deactivated by the other.
    private IEnumerable<KeyValuePair<string, GameObject>> CollectBots()
    {
        HashSet<GameObject> seen = new HashSet<GameObject>();

        if (data != null)
        {
            foreach (BotSelectionData d in data)
            {
                if (d.botObject == null || !seen.Add(d.botObject)) continue;
                yield return new KeyValuePair<string, GameObject>(d.botName, d.botObject);
            }
        }

        if (skinLibrary == null) yield break;

        Transform parent = botsParent != null ? botsParent : transform;
        HashSet<string> knownNames = new HashSet<string>(skinLibrary.BotNamesInLibrary());
        foreach (Transform child in parent)
        {
            if (!knownNames.Contains(child.name)) continue;
            if (!seen.Add(child.gameObject)) continue;
            yield return new KeyValuePair<string, GameObject>(child.name, child.gameObject);
        }
    }
}

[Serializable]
public struct BotSelectionData
{
    public string botName;
    public GameObject botObject;
}
