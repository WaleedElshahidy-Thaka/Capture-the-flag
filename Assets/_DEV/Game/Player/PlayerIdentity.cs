using System;

// Where this client's display name comes from. There is no sign-in in this project yet - the
// launcher hands a --token= across (see Future_DEV/Scripts/Core), and when that identity is
// wired in, this is the one place to read the real account name from. Until then: the OS user
// name, which is at least distinct per machine when testing with two PCs.
public static class PlayerIdentity
{
    const int MaxLength = 20;

    static string overrideName;

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
}
