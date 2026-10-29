using UnityEngine;
using UnityEngine.InputSystem;

// Temporary on-screen readout of power-up state, for testing FD-04. Same idea as DriftDebugHud:
// drop it on any scene object. Not the real HUD - that's the FD-04 slot/reveal/event feed.
//
// Bottom centre: YOUR slot (held item) and any effect running on your car.
// Top right (toggle with F1): every car - name, held item, active effects, Crown holder.
// Host only, editor/dev builds only: keys 1-5 put an item in your own empty slot, so you don't have
// to drive to a crate. Clients can't do this - there is deliberately no RPC for it.
public class PowerUpDebugHud : MonoBehaviour
{
    [SerializeField] bool showAllCars = true;

    GUIStyle slotStyle;
    GUIStyle smallStyle;

    void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.f1Key.wasPressedThisFrame) showAllCars = !showAllCars;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (keyboard.digit1Key.wasPressedThisFrame) GiveLocal(PowerUpType.Overdrive);
        if (keyboard.digit2Key.wasPressedThisFrame) GiveLocal(PowerUpType.GrappleField);
        if (keyboard.digit3Key.wasPressedThisFrame) GiveLocal(PowerUpType.DamperField);
        if (keyboard.digit4Key.wasPressedThisFrame) GiveLocal(PowerUpType.OilSlick);
        if (keyboard.digit5Key.wasPressedThisFrame) GiveLocal(PowerUpType.RepulsePulse);
#endif
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // Works on the host only: TryStoreItem refuses without State Authority, and refuses a full slot.
    static void GiveLocal(PowerUpType item)
    {
        var local = PlayerRoster.Local;
        if (local == null || !local.HasState) return;

        var inventory = local.GetComponent<PowerUpInventory>();
        if (inventory == null) return;

        bool stored = inventory.TryStoreItem(item);
        Debug.Log(stored
            ? $"[PowerUpDebugHud] Gave {item} to {local.name}."
            : $"[PowerUpDebugHud] Couldn't give {item}: slot full, or this peer isn't the host.");
    }
#endif

    void OnGUI()
    {
        EnsureStyles();

        var local = PlayerRoster.Local;
        if (local != null && local.HasState)
            DrawLocalSlot(local);

        if (showAllCars)
            DrawAllCars();
    }

    void DrawLocalSlot(PlayerMatchState local)
    {
        var inventory = local.GetComponent<PowerUpInventory>();
        PowerUpType held = inventory != null ? inventory.HeldItem : PowerUpType.None;

        string slot = held == PowerUpType.None ? "[ empty ]" : $"[ {held} ]   press F";
        string effects = Effects(local);

        float width = 420f;
        float x = (Screen.width - width) * 0.5f;
        float y = Screen.height - 90f;

        GUI.Box(new Rect(x, y, width, 74f), GUIContent.none);

        var previous = GUI.color;
        GUI.color = ColorFor(held);
        GUI.Label(new Rect(x, y + 6f, width, 32f), slot, slotStyle);
        GUI.color = previous;

        GUI.Label(new Rect(x, y + 40f, width, 26f),
                  string.IsNullOrEmpty(effects) ? "no active effect" : effects, smallStyle);
    }

    void DrawAllCars()
    {
        var cars = PlayerRoster.Active;
        float width = 520f;
        float x = Screen.width - width - 16f;
        float y = 16f;
        float row = 22f;

        GUI.Box(new Rect(x - 8f, y - 6f, width + 16f, row * (cars.Count + 2) + 8f), GUIContent.none);
        GUI.Label(new Rect(x, y, width, row), "Power-ups (F1 to hide)   host: keys 1-5 give an item", smallStyle);
        y += row;

        CrownMatchState crown = CrownMatchState.Instance;

        for (int i = 0; i < cars.Count; i++)
        {
            PlayerMatchState car = cars[i];
            if (car == null || !car.HasState) continue;

            var inventory = car.GetComponent<PowerUpInventory>();
            PowerUpType held = inventory != null ? inventory.HeldItem : PowerUpType.None;

            string name = car.IsBot ? $"{car.DisplayName} (bot)" : car.DisplayName.ToString();
            if (string.IsNullOrEmpty(name)) name = car.name;
            if (car == PlayerRoster.Local) name += " (you)";
            if (crown != null && crown.IsHolder(car.Object)) name += "  [CROWN]";

            string effects = Effects(car);
            string line = $"{name,-28} {held,-13} {effects}";

            var previous = GUI.color;
            GUI.color = ColorFor(held);
            GUI.Label(new Rect(x, y, width, row), line, smallStyle);
            GUI.color = previous;
            y += row;
        }
    }

    // Effects currently running on a car, read from the replicated state each component exposes.
    static string Effects(PlayerMatchState car)
    {
        string text = "";

        var damper = car.GetComponent<DamperField>();
        if (damper != null && damper.IsActive) text += "Damper ON  ";

        var grapple = car.GetComponent<GrappleField>();
        if (grapple != null && grapple.IsActive) text += "Grapple ON  ";

        var repulse = car.GetComponent<RepulsePulse>();
        if (repulse != null && repulse.IsCharging) text += "Repulse charging  ";

        var movement = car.GetComponent<PlayerMovement>();
        if (movement != null)
        {
            if (movement.IsBoosting) text += "Boosting  ";
            if (movement.IsBeingShoved) text += "Shoved  ";
            if (movement.CurrentSurface == SurfaceType.Oil) text += "On oil  ";
        }

        return text.TrimEnd();
    }

    static Color ColorFor(PowerUpType item)
    {
        switch (item)
        {
            case PowerUpType.Overdrive: return new Color(1f, 0.6f, 0.2f);
            case PowerUpType.GrappleField: return new Color(0.4f, 1f, 0.4f);
            case PowerUpType.DamperField: return new Color(0.5f, 0.7f, 1f);
            case PowerUpType.OilSlick: return new Color(0.8f, 0.6f, 1f);
            case PowerUpType.RepulsePulse: return new Color(0.4f, 1f, 1f);
            default: return Color.white;
        }
    }

    void EnsureStyles()
    {
        if (slotStyle != null) return;

        slotStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 22,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
        };

        smallStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            alignment = TextAnchor.MiddleLeft,
        };
    }
}
