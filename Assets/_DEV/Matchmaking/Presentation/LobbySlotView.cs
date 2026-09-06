using TMPro;
using UnityEngine;

// Local-only placeholder for one occupied seat: a primitive shape stand-in for the player's
// robot (no art yet) plus a name tag and a "READY" label shown above it. Never networked -
// LobbySeatAssigner instantiates/positions/destroys these per client, independently of
// whatever every other client is doing with its own copies.
public class LobbySlotView : MonoBehaviour
{
    [SerializeField] TMP_Text nameText;
    [SerializeField] GameObject readyLabel;

    public void Bind(string displayName, bool isSelf)
    {
        nameText.text = isSelf ? $"{displayName} (You)" : displayName;
    }

    public void SetReady(bool ready)
    {
        readyLabel.SetActive(ready);
    }
}
