using Unity.Entities;
using UnityEngine;

public class HealthAuthoring : MonoBehaviour
{
    public float healthAmount;
    public class Baker : Baker<HealthAuthoring>
    {
        public override void Bake(HealthAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new Health
            {
                healthAmount = authoring.healthAmount
            });
        }
    }
     
}
public struct Health : IComponentData
{
    public float healthAmount;
}
