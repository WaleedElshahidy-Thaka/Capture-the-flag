using UnityEngine;
using UnityEngine.UI;

// Makes the children of a container hop one after another, like a loading animation - e.g. the robot
// icons in WAITING FOR PLAYERS. Put it on the container (PlayerCountContainer).
// Icons switched off with UIHopOffset.Hop = false stand still - LobbyUI does that for found players,
// so only the empty seats keep hopping.
//
// Works inside a Horizontal/Vertical Layout Group: the hop moves each icon's drawing (UIHopOffset),
// not its RectTransform, so the layout never fights it.
public class UIHopWave : MonoBehaviour
{
    [Tooltip("How high each icon hops, in pixels.")]
    [SerializeField] float hopHeight = 10f;
    [Tooltip("Hops per second for each icon.")]
    [SerializeField, Min(0.1f)] float hopsPerSecond = 1.2f;
    [Tooltip("Delay between one icon and the next (seconds) - makes the wave.")]
    [SerializeField, Min(0f)] float delayBetweenIcons = 0.12f;
    [Tooltip("Part of each cycle spent in the air (0.2 = short quick hops, 1 = always bobbing).")]
    [SerializeField, Range(0.1f, 1f)] float airTime = 0.45f;

    void Update()
    {
        float cycle = 1f / hopsPerSecond;
        int index = 0;

        foreach (Transform child in transform)
        {
            if (!child.gameObject.activeSelf) continue;
            if (!child.TryGetComponent(out Graphic _)) continue; // needs an Image (or other Graphic) to move

            if (!child.TryGetComponent(out UIHopOffset hop))
                hop = child.gameObject.AddComponent<UIHopOffset>();

            // Each icon runs the same hop, shifted by its place in the row.
            float t = Mathf.Repeat(Time.unscaledTime - index * delayBetweenIcons, cycle) / cycle; // 0..1
            float inAir = t / airTime;                                                            // 0..1 while hopping
            float height = inAir < 1f ? Mathf.Sin(inAir * Mathf.PI) * hopHeight : 0f;              // up and down

            hop.SetOffset(hop.Hop ? height : 0f); // filled icons stand still
            index++;
        }
    }

    void OnDisable()
    {
        foreach (Transform child in transform)
            if (child.TryGetComponent(out UIHopOffset hop)) hop.SetOffset(0f);
    }
}
