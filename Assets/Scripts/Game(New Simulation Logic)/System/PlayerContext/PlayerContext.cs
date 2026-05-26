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
   currentPopulation,
   maxPopulation,
   All
}

[System.Serializable]
public struct PlayerContext:IComponentData
{
   public int PlayerId;
   public int CIVILIZATION_ID;
   public Age age;
   public int currentPopulation;
   public int maxPopulation;   
   public PlayerContext(int playerId, int civilizationId, Age age)
   {
      PlayerId = playerId;
      CIVILIZATION_ID = civilizationId;
      this.age = age;
      currentPopulation = 0;
      maxPopulation = 8;
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
                if (value is int newPlayerId)
                {
                    playerContext.PlayerId = (int)value;
                }
                else
                {
                    return FunctionResult.Failure; // Return false if value type is incorrect
                }
                break;
            case PlayerContextDataType.CivilizationId:
                if (value is int newCivilizationId)
                {
                    playerContext.CIVILIZATION_ID = (int)value;
                }
                else
                {
                    return FunctionResult.Failure; // Return false if value type is incorrect
                }
                break;
            case PlayerContextDataType.Age:
                if (value is Age newAge)
                {
                    playerContext.age = (Age)value;
                }
                else
                {
                    return FunctionResult.Failure; // Return false if value type is incorrect
                }
                break;
            case PlayerContextDataType.currentPopulation:
                if (value is int currentPopulation)
                {
                    playerContext.currentPopulation = (int)value;
                }
                else
                {
                    return FunctionResult.Failure; // Return false if value type is incorrect
                }
                break;
            case PlayerContextDataType.maxPopulation:
                if (value is int maxPopulation)
                {
                    playerContext.maxPopulation = (int)value;
                }
                else
                {
                    return FunctionResult.Failure; // Return false if value type is incorrect
                }   
                break;
            case PlayerContextDataType.All:
                if (value is PlayerContext newContext)
                {
                    playerContext.PlayerId = newContext.PlayerId;
                    playerContext.CIVILIZATION_ID = newContext.CIVILIZATION_ID;
                    playerContext.age = newContext.age;
                    playerContext.currentPopulation = newContext.currentPopulation;
                    playerContext.maxPopulation = newContext.maxPopulation;
                }
                else
                {
                    return FunctionResult.Failure; // Return false if value type is incorrect
                }
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
