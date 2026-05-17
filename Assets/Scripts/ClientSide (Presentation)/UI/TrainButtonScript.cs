using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.UI;

public class TrainButtonScript : MonoBehaviour
{
    private Button button;
    private EntityQuery selectedQuery;
    private int buttonIndexInPrefabList = 0; // Assuming this button trains the first unit in the prefab list
    private void Awake()
    {
        button = GetComponent<Button>();
    }

    private void Start()
    {
        var world = World.DefaultGameObjectInjectionWorld;

        if (world == null || !world.IsCreated)
        {
            Debug.LogError("No ECS world found!");
            enabled = false;
            return;
        }

        selectedQuery = world.EntityManager.CreateEntityQuery(
            ComponentType.ReadOnly<Selected>()
        );
    }

    public void OnButtonClick()
    {
        var world = World.DefaultGameObjectInjectionWorld;

        if (world == null || !world.IsCreated)
            return;

        var entityManager = world.EntityManager;

        using var selectedEntities = selectedQuery.ToEntityArray(Allocator.Temp);

        if (selectedEntities.Length == 0)
        {
            Debug.LogWarning("No entity selected!");
            return;
        }

        var entity = selectedEntities[0];

        if (!entityManager.Exists(entity))
        {
            Debug.LogWarning("Selected entity no longer exists!");
            return;
        }

        TrainUnitHelper.TrainUnit(entityManager, entity, buttonIndexInPrefabList);
    }

    private void Update()
    {
        var world = World.DefaultGameObjectInjectionWorld;

        if (world == null || !world.IsCreated)
        {
            button.interactable = false;
            return;
        }

        var entityManager = world.EntityManager;

        using var selectedEntities = selectedQuery.ToEntityArray(Allocator.Temp);

        if (selectedEntities.Length == 0)
        {
            button.interactable = false;
            return;
        }

        var entity = selectedEntities[0];

        if (!entityManager.Exists(entity))
        {
            button.interactable = false;
            return;
        }

        if (!entityManager.HasComponent<BuildingData>(entity))
        {
            button.interactable = false;
            return;
        }

        var buildingData =
            entityManager.GetComponentData<BuildingData>(entity);

        button.interactable =
            buildingData.Type == BuildingType.Barracks;
    }
}