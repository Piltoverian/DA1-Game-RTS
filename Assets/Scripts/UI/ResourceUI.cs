using TMPro;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

public class ResourceUI : MonoBehaviour
{
    public TMP_Text resourceText;

    private EntityManager entityManager;
    private EntityQuery query;

    void Start()
    {
        entityManager =
            World.DefaultGameObjectInjectionWorld.EntityManager;

        query =
            entityManager.CreateEntityQuery(typeof(PlayerResourceData));
    }

    void Update()
    {
        if (query.IsEmpty)
            return;

        PlayerResourceData res =
            query.GetSingleton<PlayerResourceData>();

        resourceText.text =
            $"Gold: {res.Gold} | Wood: {res.Wood} | Food: {res.Food}";
    }
}