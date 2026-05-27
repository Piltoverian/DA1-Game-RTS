using Unity.Entities;
using UnityEngine;
public class HouseAuthoring : UnityEngine.MonoBehaviour
{
    [SerializeField] int maxPopWillIncrease;
    class Baker : Baker<HouseAuthoring>
    {
        public override void Bake(HouseAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new HouseCompoent
            {
                maxPopWillIncrease = authoring.maxPopWillIncrease
            });
            AddComponent<HouseCleanUp>(entity);

            var world =World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;
            var entityQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<HouseCompoent>());

            if (entityManager.HasComponent<Unit>(entity))
            {
                PlayerContext context;
                var unit= entityManager.GetComponentData<Unit>(entity);
                PlayerContextHelper.GetContextData(entityManager, unit.playerID,out context);
                context.maxPopulation += authoring.maxPopWillIncrease;
            }
            else
            {
                Debug.Log("Entity does not have Unit component.");
            }
        }
    }
}

public struct HouseCompoent : IComponentData
{
    public int maxPopWillIncrease;
}

public struct HouseCleanUp : ICleanupComponentData
{
}