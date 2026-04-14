using Unity.Entities;

public enum FieldState : byte
{
    Requested,          // FlowFieldAssignmentSystem just created it
    CalculatingCost,    // IntergrationFieldSystem is running BFS
    CalculatingDirection, // FlowDirectionSystem is running vectors
    Ready,               // Finished! Units can now move.
    PendingRecalculation,    // Something changed and the flow field needs to be recalculated.
}

public struct FlowFieldStatus : IComponentData
{
    public FieldState Value;
}
