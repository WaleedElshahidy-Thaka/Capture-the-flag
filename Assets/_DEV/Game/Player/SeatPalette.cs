using UnityEngine;

// The one list of seat colours. The car's ring, the lobby list and the in-game scoreboard all read
// it, so a seat is the same colour everywhere and there is only one place to change it.
//
// Seat = PlayerMatchState.Slot, which is networked, so every machine picks the same colour for the
// same car with no extra syncing.
//
// Designers edit the colours on Resources/SeatPalette.asset (Create > Glowtag > Seat Palette, saved in
// any Resources folder with that name). Without the asset, the defaults below are used.
[CreateAssetMenu(menuName = "Glowtag/Seat Palette", fileName = "SeatPalette")]
public class SeatPalette : ScriptableObject
{
    [SerializeField] Color[] colors =
    {
        new Color(0.30f, 0.55f, 1.00f), // seat 0 blue
        new Color(1.00f, 0.90f, 0.10f), // seat 1 yellow
        new Color(1.00f, 0.20f, 0.45f), // seat 2 pink
        new Color(0.15f, 0.90f, 0.55f), // seat 3 green
        new Color(0.65f, 0.35f, 1.00f), // seat 4 purple
        new Color(1.00f, 0.55f, 0.15f), // seat 5 orange
    };

    static SeatPalette instance;
    static bool loaded;

    public static Color Get(int slot)
    {
        if (!loaded)
        {
            instance = Resources.Load<SeatPalette>("SeatPalette");
            if (instance == null) instance = CreateInstance<SeatPalette>(); // defaults
            loaded = true;
        }

        var list = instance.colors;
        return list != null && list.Length > 0 ? list[Mathf.Abs(slot) % list.Length] : Color.white;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { instance = null; loaded = false; }
}
