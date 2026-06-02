using Unity.Entities;
using UnityEngine;

public class BuildingBlocker : MonoBehaviour
{
    public Entity BuildingEntity;

    private EntityManager entityManager;
    private bool hasEntityManager;

    private void Start()
    {
        World world = World.DefaultGameObjectInjectionWorld;

        if (world == null)
            return;

        entityManager = world.EntityManager;
        hasEntityManager = true;
    }

    private void Update()
    {
        if (!hasEntityManager)
            return;

        if (BuildingEntity == Entity.Null)
        {
            Destroy(gameObject);
            return;
        }

        if (!entityManager.Exists(BuildingEntity))
        {
            Destroy(gameObject);
        }
    }
}