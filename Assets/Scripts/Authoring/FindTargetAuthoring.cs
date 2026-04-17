using UnityEngine;
using Unity.Entities;
public class FindTargetAuthoring : MonoBehaviour
{
    public float range;
    public int playerID;
    public float timerMax;
    public class Baker : Baker<FindTargetAuthoring>
    {
            
        public override void Bake(FindTargetAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new FindTarget {
                range = authoring.range,
                playerID = authoring.playerID,
                timerMax = authoring.timerMax
            });
        }
    }
}
public struct FindTarget : IComponentData
{
    public float range;
    public int playerID;
    public float timer;
    public float timerMax;
}