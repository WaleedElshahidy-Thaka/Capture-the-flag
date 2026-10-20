using UnityEngine;

// Temporary on-screen readout of the local car's drift & boost state, for tuning.
// Drop it on any scene object. Needs this property on PlayerMovement:
//   public DriftPhase CurrentDriftPhase => (DriftPhase)DriftPhaseValue;
public class DriftDebugHud : MonoBehaviour
{
    void OnGUI()
    {
        var local = PlayerRoster.Local;
        if (local == null || !local.HasState) return;

        var move = local.GetComponent<PlayerMovement>();
        if (move == null) return;

        GUI.Label(new Rect(16f, 16f, 480f, 110f),
            $"Phase: {move.CurrentDriftPhase}   Reserve: {move.DriftReserveValue:0}   Stage: {move.DriftStage}\n" +
            $"Sliding: {move.IsSliding}   Boosting: {move.IsBoosting}   Chained: {move.IsChained}\n" +
            $"Speed: {move.ForwardSpeed:0.0} m/s   Grounded: {move.IsGrounded}");
    }
}