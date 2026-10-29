using UnityEditor;
using UnityEngine;

// Adds BotBrain to PlayerCar.prefab. Every car gets one - a player's car is handed to a bot when
// its owner drops (HandToBot), and the brain only runs while the car has no player. Safe to run again.
static class BotBrainCarBuilder
{
    const string CarPrefabPath = "Assets/_DEV/Game/Player/Resources/PlayerCar.prefab";

    [MenuItem("Game/Add Bot Brain To Car")]
    static void Build()
    {
        GameObject car = PrefabUtility.LoadPrefabContents(CarPrefabPath);
        try
        {
            if (car.GetComponent<BotBrain>() == null)
                car.AddComponent<BotBrain>();

            PrefabUtility.SaveAsPrefabAsset(car, CarPrefabPath);
            Debug.Log($"[BotBrainCarBuilder] BotBrain is on {CarPrefabPath}.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(car);
        }
    }
}
