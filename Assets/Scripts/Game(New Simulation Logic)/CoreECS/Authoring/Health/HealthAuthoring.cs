using Unity.Entities;
using UnityEngine;

public class HealthAuthoring : MonoBehaviour
{
    public float healthAmount;
    public float maxHealthAmount;
    public class Baker : Baker<HealthAuthoring>
    {
        public override void Bake(HealthAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new Health
            {
                healthAmount = authoring.healthAmount,
                maxHealthAmount = authoring.maxHealthAmount,
                OnHealthChanged = true
            });
        }
    }
     
}
public struct Health : IComponentData
{
    public float healthAmount;
    public float maxHealthAmount;
    public bool OnHealthChanged;
}
