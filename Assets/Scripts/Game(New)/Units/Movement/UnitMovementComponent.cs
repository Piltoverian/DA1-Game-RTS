using Unity.Entities;

public struct UnitMovementComponent : IComponentData
{
    public float speed;
    public bool hastarget;
    public Entity FieldEntity;
}
