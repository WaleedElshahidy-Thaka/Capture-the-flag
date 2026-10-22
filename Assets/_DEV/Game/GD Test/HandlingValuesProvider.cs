
using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

public static class HandlingValuesProvider
{
    public static bool UseJsonValues = true;

    public static HandlingValues Current { get; private set; }

    private static string FilePath =>
        Path.Combine(
            Directory.GetParent(Application.dataPath).FullName,
            "handling_values.json"
        );

    public static void Initialize()
    {
        // Create your original class instance.
        HandlingValues defaultValues = new HandlingValues();

        if (!UseJsonValues)
        {
            Current = defaultValues;
            return;
        }

        try
        {
            if (!File.Exists(FilePath))
            {
                File.WriteAllText(
                    FilePath,
                    JsonConvert.SerializeObject(
                        defaultValues,
                        Formatting.Indented
                    )
                );

                Current = defaultValues;
                return;
            }

            string json = File.ReadAllText(FilePath);

            HandlingValues loadedValues =
                JsonConvert.DeserializeObject<HandlingValues>(json);

            if (loadedValues == null)
                throw new Exception("Invalid handling JSON.");

            Current = loadedValues;
        }
        catch (Exception e)
        {
            Debug.LogError($"Handling config error: {e.Message}");
            Current = defaultValues;
        }
    }
}
