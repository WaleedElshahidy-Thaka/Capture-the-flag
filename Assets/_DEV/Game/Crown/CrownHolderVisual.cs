using Fusion;
using UnityEngine;

public class CrownHolderVisual : NetworkBehaviour
{
    [SerializeField] private GameObject crownIndicator;

    private CrownMatchState crownState;

    public override void Spawned()
    {
        crownState = FindCrownState();
        RefreshVisual();
    }

    public override void Render()
    {
        if (crownState == null)
            crownState = FindCrownState();

        RefreshVisual();
    }

    private CrownMatchState FindCrownState()
    {
        return FindFirstObjectByType<CrownMatchState>();
    }

    private void RefreshVisual()
    {
        if (crownIndicator == null || crownState == null || Object == null)
            return;

        bool isHolder = crownState.IsHolder(Object);

        if (crownIndicator.activeSelf != isHolder)
            crownIndicator.SetActive(isHolder);
    }
}