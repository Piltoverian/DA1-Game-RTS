using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

public enum PlayerContextUpdateType
{
   ResourceUpdate,
   All
}

public enum Age
{
    Industrial,
    Modern,
    Future
}
[System.Serializable]
public struct ResourcePair:IBufferElementData
{
    public ResourceType Type;
    public float Amount;
   public ResourcePair(ResourceType type, float amount)
   {
      Type = type;
      Amount = amount;
   }
}

public enum PlayerContextDataType
{
   PlayerId,
   CivilizationId,
   Resources,
   Age,
   All
}

[System.Serializable]
public struct PlayerContext:IComponentData
{
   public int PlayerId;
   public int CIVILIZATION_ID;
   public Age age;

   public PlayerContext(int playerId, int civilizationId, Age age)
   {
      PlayerId = playerId;
      CIVILIZATION_ID = civilizationId;
      this.age = age;
   }
}

public static class PlayerContextHelper
{
    public static FunctionResult GetContextData(EntityManager entityManager,int playerId ,out PlayerContext playerContext)
    {
        var query = entityManager.CreateEntityQuery(typeof(PlayerContext));
        playerContext = new PlayerContext();

        if (query.IsEmpty)
        {
            return FunctionResult.Failure; // Return false if no entities found
        }
        var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
        for (int i = 0; i < entities.Length; i++)
        {
            PlayerContext context = entityManager.GetComponentData<PlayerContext>(entities[i]);
            if (context.PlayerId == playerId)
            {
                playerContext = context;
                return FunctionResult.Success;
            }
        }
        playerContext = new PlayerContext();
        return FunctionResult.Failure;
    }

    public static FunctionResult CreatePlayerContext(EntityManager entityManager, int playerId, int civilizationId, NativeList<ResourcePair> resources, Age age)
    {
        var entity = entityManager.CreateEntity();
        PlayerContext playerContext = new PlayerContext(playerId, civilizationId, age);
        entityManager.AddComponentData(entity, playerContext);
        entityManager.AddBuffer<ResourcePair>(entity).ResizeUninitialized(Enum.GetNames(typeof(ResourceType)).Length);
        
        return FunctionResult.Success;
    }

    public static FunctionResult UpdatePlayerContext(EntityManager entityManager, int playerId, PlayerContextDataType dataType, object value)
    {

        var query = entityManager.CreateEntityQuery(typeof(PlayerContext));
        var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
        Entity targetEntity = Entity.Null;
        for (int i = 0; i < entities.Length; i++)
        {
            PlayerContext context = entityManager.GetComponentData<PlayerContext>(entities[i]);
            if (context.PlayerId == playerId)
            {
                targetEntity = entities[i];
                break;
            }
        }
        if (targetEntity == Entity.Null)
        {
            return FunctionResult.Failure; // Return false if player context not found
        }
        PlayerContext playerContext = entityManager.GetComponentData<PlayerContext>(targetEntity);
        switch (dataType)
        {
            case PlayerContextDataType.PlayerId:
                playerContext.PlayerId = (int)value;
                break;
            case PlayerContextDataType.CivilizationId:
                playerContext.CIVILIZATION_ID = (int)value;
                break;
            case PlayerContextDataType.Age:
                playerContext.age = (Age)value;
                break;
            case PlayerContextDataType.All:
                PlayerContext newContext = (PlayerContext)value;
                playerContext.PlayerId = newContext.PlayerId;
                playerContext.CIVILIZATION_ID = newContext.CIVILIZATION_ID;
                playerContext.age = newContext.age;
                break;
            default:
                return FunctionResult.Failure; // Return false for unsupported data types
        }

        entityManager.SetComponentData(targetEntity, playerContext);
        return FunctionResult.Success;
    }

    public static FunctionResult DeletePlayerContext(EntityManager entityManager, int playerId)
    {
        var query = entityManager.CreateEntityQuery(typeof(PlayerContext));
        var unitquery = entityManager.CreateEntityQuery(typeof(PlayerContext));
        if (query.IsEmpty)
        {
            return FunctionResult.Failure; // Return false if no entities found
        }
        var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
        for (int i = 0; i < entities.Length; i++)
        {
            PlayerContext context = entityManager.GetComponentData<PlayerContext>(entities[i]);
            if (context.PlayerId == playerId)
            {
                foreach (var unit in unitquery.ToEntityArray(Unity.Collections.Allocator.Temp))
                {
                    if (entityManager.HasComponent<Unit>(unit))
                    {
                        Unit unitComponent = entityManager.GetComponentData<Unit>(unit);
                        if (unitComponent.playerID == playerId)
                        {
                            entityManager.DestroyEntity(unit);
                        }
                    }
                }
                entityManager.DestroyEntity(entities[i]);
                return FunctionResult.Success;
            }
        }
        return FunctionResult.Failure;
    }
}
