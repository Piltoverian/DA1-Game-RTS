using Unity.Entities;
using UnityEngine;
using Unity.Mathematics;
public class EnemySpawnerAuthoring : MonoBehaviour
{
    public float timerMax;
    [SerializeField] private float3 rallyPoint;
    public class Baker : Baker<EnemySpawnerAuthoring>
    {
        
        public override void Bake(EnemySpawnerAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new EnemySpawner
            {
                timerMax = authoring.timerMax,
                RallyPoint = float3.zero
            });
        }
    }
}
public struct EnemySpawner : IComponentData
{
    public float timer;
    public float timerMax;
    public float3 RallyPoint;
}