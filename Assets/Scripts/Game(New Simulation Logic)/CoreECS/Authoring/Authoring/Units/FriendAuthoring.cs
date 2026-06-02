using UnityEngine;
using Unity.Entities;
public class FriendAuthoring : MonoBehaviour
{
    public class Baker: Baker<FriendAuthoring>
    {
        public override void Bake(FriendAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new Friend());
        }
    }

}
public struct Friend : IComponentData
{
}
