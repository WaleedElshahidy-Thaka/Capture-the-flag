using System;
using UnityEngine;

// This client's identity. There is no sign-in yet, so the display name is the OS user name
// (replace LocalDisplayName's source once the launcher's --token identity is wired in).
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
            name += " (Editor)"; // tells Editor and build apart when testing both on one PC
#endif
            return name;
        }
    }

    public static void SetLocalDisplayName(string name) => overrideName = name;

    // Stable per-install id, kept in PlayerPrefs. Sent as Fusion's connection token and stamped on
    // our car (PlayerMatchState.OwnerToken) so a new host can give it back after a host migration.
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
