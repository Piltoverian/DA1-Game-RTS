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
        image.enabled = false;
        var selectUnit= SelectHelper.GetFirstSelectedEntityByplayerID(GameManager.Instance.GetModule<SelectManager>().currentContext.playerId);
        var world= World.DefaultGameObjectInjectionWorld;
        var entityManager = world.EntityManager;
        if (selectUnit != Entity.Null)
        {
            if (entityManager.HasComponent<EntityOwner>(selectUnit))
            {

                image.sprite = EntityPresentation.Icon(entityManager, selectUnit);
                image.enabled = true;
            }
        }
    }
}

