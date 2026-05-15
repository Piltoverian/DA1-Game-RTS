using Unity.Entities;
using UnityEngine;

public class PlayerResourceAuthoring : MonoBehaviour
{
    public int StartGold = 500;
    public int StartWood = 300;
    public int StartFood = 0;

    class Baker : Baker<PlayerResourceAuthoring>
    {
        public override void Bake(PlayerResourceAuthoring src)
        {
            Entity e = GetEntity(TransformUsageFlags.None);

            AddComponent(e, new PlayerResourceData
            {
                Gold = src.StartGold,
                Wood = src.StartWood,
                Food = src.StartFood
            });
        }
    }
}

public struct PlayerResourceData : IComponentData
{
    public int Gold;
    public int Wood;
    public int Food;
}
