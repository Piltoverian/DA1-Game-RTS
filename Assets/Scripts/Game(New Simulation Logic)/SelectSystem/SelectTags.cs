using Unity.Collections;
using Unity.Entities;

public struct Selectable :IComponentData
{
    public int playerID;
    public int GridIndex;
}

public struct DragSelectableEntity: IComponentData
{

}

public struct SingleSelectableEntity:IComponentData
{

}

public struct Selected : IComponentData, IEnableableComponent
{
    public Entity visualEntity;
    public float showScale;
}

public struct SelectableBucketContainer : IComponentData
{
    public NativeParallelMultiHashMap<int, Entity> Bucket;
}