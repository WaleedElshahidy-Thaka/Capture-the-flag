using System;

// Reads --key=value pairs passed on the command line when an app in the cycle is launched
// as a standalone process, e.g. ZeroOneDigitalSkill.exe --launcher=true --token=eyJhbGciOi...
//
// Duplicated verbatim in the launcher, the lobby and every game. Keep the copies identical —
// the double-dash "--key=value" shape is the contract between processes, and a single-dash
// variant silently reads as "argument absent" on the receiving side.
public static class LaunchArgs
{
    public static string Get(string key, string defaultValue = null)
    {
        string prefix = "--" + key + "=";
        foreach (var arg in Environment.GetCommandLineArgs())
        {
            if (arg.StartsWith(prefix, StringComparison.Ordinal))
                return arg.Substring(prefix.Length);
        }
        return defaultValue;
    }
}
