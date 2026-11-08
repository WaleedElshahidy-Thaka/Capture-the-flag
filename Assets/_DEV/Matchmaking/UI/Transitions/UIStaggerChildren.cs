using UnityEngine;

// Put on a container (PlayerSlotsContainer, PlayerCountContainer) to make its children animate in one
// after another instead of all at once. Each child needs a UITransition; it asks this component for
// its extra delay when it switches on, so it works whatever order Unity enables things in.
public class UIStaggerChildren : MonoBehaviour
{
    [Tooltip("Wait before the first child starts - lets the parent panel arrive first.")]
    [SerializeField, Min(0f)] float startDelay = 0.15f;
    [Tooltip("Gap between one child and the next.")]
    [SerializeField, Min(0f)] float step = 0.06f;

    // Position among the active children with a UITransition, times the step.
    public float DelayFor(Transform child)
    {
        int index = 0;
        foreach (Transform sibling in transform)
        {
            if (sibling == child) break;
            if (sibling.gameObject.activeSelf && sibling.GetComponent<UITransition>() != null) index++;
        }
        return startDelay + step * index;
    }
}
