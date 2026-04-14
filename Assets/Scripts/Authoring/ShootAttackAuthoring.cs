using Unity.Entities;
using UnityEngine;

public class ShootAttackAuthoring : MonoBehaviour
{
    public float timerMax;
    public class Baker : Baker<ShootAttackAuthoring>
    {
        
        public override void Bake(ShootAttackAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new ShootAttack {
                timerMax = authoring.timerMax
            });
        }
    }
}
public struct ShootAttack : IComponentData
{
    public float timer;
    public float timerMax;
}