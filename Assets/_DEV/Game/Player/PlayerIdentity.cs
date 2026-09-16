using System;
using UnityEngine;

// Where this client's identity comes from. There is no sign-in in this project yet - the
// launcher hands a --token= across (see Future_DEV/Scripts/Core), and when that identity is
// wired in, this is the one place to read the real account name from. Until then: the OS user
// name, which is at least distinct per machine when testing with two PCs.
public static class PlayerIdentity
{
    const int MaxLength = 20;
    const string InstallIdKey = "glowtag.installId";

    static string overrideName;
    static Guid? installId;

    public static string LocalDisplayName
    {
        get
        {
            string name = string.IsNullOrWhiteSpace(overrideName) ? Environment.UserName : overrideName;
            if (string.IsNullOrWhiteSpace(name)) name = "Player";
            if (name.Length > MaxLength) name = name.Substring(0, MaxLength);
#if UNITY_EDITOR
            // Editor + build on one PC share the OS user name; tag the Editor so the two are
            // telling apart in a two-instance test.
            name += " (Editor)";
#endif
            return name;
        }
    }

    public static void SetLocalDisplayName(string name) => overrideName = name;

    // A stable id for this install, generated once and kept in PlayerPrefs. Sent to the host as
    // Fusion's connection token on every connect, and stamped on this player's car
    // (PlayerMatchState.OwnerToken). PlayerRefs are per-session - after a host migration every
    // player reconnects as a new PlayerRef - so this is what lets the new host hand each car
    // back to the person who was driving it. Editor and build keep separate PlayerPrefs, so a
    // two-instance test on one PC still gets two ids.
    public static Guid InstallId
    {
        get
        {
            if (installId.HasValue) return installId.Value;

            string stored = PlayerPrefs.GetString(InstallIdKey, "");
            if (!Guid.TryParse(stored, out var id))
            {
                id = Guid.NewGuid();
                PlayerPrefs.SetString(InstallIdKey, id.ToString("N"));
                PlayerPrefs.Save();
            }

            installId = id;
            return id;
        }
    }

    public static byte[] ConnectionToken => InstallId.ToByteArray();

    // 32 hex characters - fits NetworkString<_32> exactly.
    public static string TokenText(byte[] token)
    {
        if (token == null || token.Length != 16) return "";
        return new Guid(token).ToString("N");
    }
}
