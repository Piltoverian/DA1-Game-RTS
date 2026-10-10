using Unity.Entities;

// Presentation matrices never enter the following frame's simulation or physics.
[UpdateInGroup(typeof(InitializationSystemGroup), OrderFirst=true)]
public partial class TerrainVisualRestoreSystem : SystemBase
{
    protected override void OnUpdate(){World.GetExistingSystemManaged<TerrainVisualOffsetSystem>()?.RestoreLogicalMatrices();}
}
