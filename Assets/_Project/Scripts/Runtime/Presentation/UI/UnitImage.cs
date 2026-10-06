using UnityEngine;
using Unity.Entities;
public class UnitImage : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        var image = GetComponent<UnityEngine.UI.Image>();
        if (image == null) return;
        image.enabled = false;
        if (!SelectHelper.TryGetLocalPlayerId(out int playerId)) return;
        var selectUnit= SelectHelper.GetFirstSelectedEntityByplayerID(playerId);
        var world= World.DefaultGameObjectInjectionWorld;
        if (world == null || !world.IsCreated) return;
        var entityManager = world.EntityManager;
        if (selectUnit != Entity.Null && entityManager.Exists(selectUnit))
        {
            if (entityManager.HasComponent<EntityOwner>(selectUnit))
            {

                image.sprite = EntityPresentation.Icon(entityManager, selectUnit);
                image.enabled = true;
            }
        }
    }
}

