using Unity.Entities;
using UnityEngine;

public class EnemySpawnerAuthoring : MonoBehaviour
{
    public float timerMax;
    public class Baker : Baker<EnemySpawnerAuthoring>
    {
        
        public override void Bake(EnemySpawnerAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new EnemySpawner
            {
                timerMax = authoring.timerMax
            });
        }
    }
}
public struct EnemySpawner : IComponentData
{
    public float timer;
    public float timerMax;
}