using System;
using System.Collections.Generic;
using UnityEngine;

// The lobby's skin catalogue, mirrored so a game can dress the chosen robot without talking to
// the lobby. The lobby picks a skin by writing three textures into one material per robot
// (see SkinCardHolder.ApplySkinTextures); those edits live in the lobby's own process and are
// gone by the time a game starts, so the game has to re-apply the same textures itself.
//
// Entries are keyed by the same strings the lobby uses — BotNames for the robot, and
// SkinDataHolder.M_SkinName for the skin — because those are what arrive on the command line
// as --bot= and --skin=. Generated from the lobby scene; regenerate rather than hand-editing
// if the lobby's skin lists change.
//
// Duplicated verbatim in every game. Keep the copies identical.
[CreateAssetMenu(fileName = "BotSkinLibrary", menuName = "ZeroOne/Bot Skin Library")]
public class BotSkinLibrary : ScriptableObject
{
    public List<BotSkinSet> bots = new List<BotSkinSet>();

    public BotSkinSet FindBot(string botName)
    {
        if (string.IsNullOrEmpty(botName)) return null;
        for (int i = 0; i < bots.Count; i++)
            if (bots[i] != null && bots[i].botName == botName)
                return bots[i];
        return null;
    }

    /// <summary>Robot names this library knows about — used to spot robot objects in a scene by name.</summary>
    public IEnumerable<string> BotNamesInLibrary()
    {
        for (int i = 0; i < bots.Count; i++)
            if (bots[i] != null && !string.IsNullOrEmpty(bots[i].botName))
                yield return bots[i].botName;
    }
}

[Serializable]
public class BotSkinSet
{
    public string botName;

    // The material the lobby drives for this robot. Used as a filter: only renderer slots
    // already using it get re-textured, so a robot's lights, decals and bolt-on extras keep
    // their own materials instead of being painted with the body skin.
    public Material targetMaterial;

    public List<BotSkin> skins = new List<BotSkin>();

    public BotSkin FindSkin(string skinName)
    {
        if (string.IsNullOrEmpty(skinName)) return null;
        for (int i = 0; i < skins.Count; i++)
            if (skins[i] != null && skins[i].skinName == skinName)
                return skins[i];
        return null;
    }
}

[Serializable]
public class BotSkin
{
    public string skinName;
    public Texture baseMap;
    public Texture normalMap;
    public Texture metallicMap;
}
