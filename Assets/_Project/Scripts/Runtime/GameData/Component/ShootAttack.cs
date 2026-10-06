using Unity.Collections;
using Unity.Entities;
public struct ShootAttack : IComponentData,IEnableableComponent
{
    public FixedString64Bytes IDs;
}

[InternalBufferCapacity(4)]
public struct UnitWeaponSlot : IBufferElementData
{
    public float timer;
}
