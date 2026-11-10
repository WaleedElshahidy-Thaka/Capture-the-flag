using UnityEngine;

// Temporary on-screen readout of the local car's drift & boost state, for tuning.
// Drop it on any scene object.
public class DriftDebugHud : MonoBehaviour
{
    void OnGUI()
    {
        var local = PlayerRoster.Local;
        if (local == null || !local.HasState) return;

        var move = local.GetComponent<PlayerMovement>();
        if (move == null) return;

        string window = move.DriftWindowStage > 0 ? $"STAGE {move.DriftWindowStage} - PRESS SHIFT" : "-";
        GUI.Label(new Rect(16f, 16f, 520f, 130f),
            $"Phase: {move.CurrentDriftPhase}   Dir: {move.DriftDirectionValue}   Reserve: {move.DriftReserveValue:0}   Stage reached: {move.DriftStageReached}\n" +
            $"Window: {window}\n" +
            $"Sliding: {move.IsSliding}   Boosting: {move.IsBoosting}   Chained: {move.IsChained}\n" +
            $"Speed: {move.ForwardSpeed:0.0} m/s   Grounded: {move.IsGrounded}");
    }
}
