using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

public struct PlayerContextCachePendingTag : IComponentData
{
}

public struct PlayerContextCache : IComponentData
{
    public int PlayerId;
    public int civilizationId;
    public FixedString64Bytes CivID;
    public Age age;
    public int currentPopulation;
    public int maxPopulation;

    public PlayerContextCache(PlayerContext context)
    {
        PlayerId = context.PlayerId;
        civilizationId = context.civilizationId;
        CivID = context.CivID;
        this.age = context.age;
        currentPopulation = context.currentPopulation;
        maxPopulation = context.maxPopulation;
    }

    public void UpdateFromContext(PlayerContext context)
    {
        PlayerId = context.PlayerId;
        civilizationId = context.civilizationId;
        CivID = context.CivID;
        age = context.age;
        currentPopulation = context.currentPopulation;
        maxPopulation = context.maxPopulation;
    }
}

public enum Age
{
    Industrial,
    Modern,
    Future
}

[System.Serializable]
public struct ResourcePair : IBufferElementData
{
    public ResourceType Type;
    public float Amount;

    public ResourcePair(ResourceType type, float amount)
    {
        Type = type;
        Amount = amount;
    }
}

public struct ResourcePairCache : IBufferElementData
{
    public ResourceType Type;
    public float Amount;

    public ResourcePairCache(ResourcePair resourcePair)
    {
        Type = resourcePair.Type;
        Amount = resourcePair.Amount;
    }
}

[System.Serializable]
public struct PlayerContext : IComponentData
{
    public int PlayerId;
    public int civilizationId;
    public FixedString64Bytes CivID;
    public Age age;
    public int currentPopulation;
    public int maxPopulation;

    public PlayerContext(int playerId, int civilizationId, Age age, FixedString64Bytes civId = default)
    {
        PlayerId = playerId;
        this.civilizationId = civilizationId;
        CivID = civId;
        this.age = age;
        currentPopulation = 0;
        maxPopulation = 0;
    }
}

public static class PlayerContextHelper
{
    public static FunctionResult GetContextData(EntityManager entityManager, int playerId, out PlayerContext playerContext)
    {
        return GetPlayerContextEntity(entityManager, playerId, out _, out playerContext);
    }

    public static FunctionResult GetPlayerContextEntity(EntityManager entityManager, int playerId, out Entity targetEntity, out PlayerContext playerContext)
    {
        targetEntity = Entity.Null;
        playerContext = default;

        using (var builder = new EntityQueryBuilder(Allocator.Temp).WithAll<PlayerContext>())
        {
            var query = entityManager.CreateEntityQuery(builder);
            using (var entities = query.ToEntityArray(Allocator.Temp))
            {
                for (int i = 0; i < entities.Length; i++)
                {
                    PlayerContext context = entityManager.GetComponentData<PlayerContext>(entities[i]);
                    if (context.PlayerId == playerId)
                    {
                        targetEntity = entities[i];
                        playerContext = context;
                        return FunctionResult.Success;
                    }
                }
            }
        }

        return FunctionResult.Failure;
    }

    public static FunctionResult SetCurrentPopulation(EntityManager entityManager, int playerId, int currentPopulation)
    {
        if (GetPlayerContextEntity(entityManager, playerId, out Entity entity, out PlayerContext context) == FunctionResult.Success)
        {
            context.currentPopulation = currentPopulation;
            entityManager.SetComponentData(entity, context);
            return FunctionResult.Success;
        }
        return FunctionResult.Failure;
    }

    public static FunctionResult SetMaxPopulation(EntityManager entityManager, int playerId, int maxPopulation)
    {
        if (GetPlayerContextEntity(entityManager, playerId, out Entity entity, out PlayerContext context) == FunctionResult.Success)
        {
            context.maxPopulation = maxPopulation;
            entityManager.SetComponentData(entity, context);
            return FunctionResult.Success;
        }
        return FunctionResult.Failure;
    }

    public static FunctionResult SetAge(EntityManager entityManager, int playerId, Age age)
    {
        if (GetPlayerContextEntity(entityManager, playerId, out Entity entity, out PlayerContext context) == FunctionResult.Success)
        {
            context.age = age;
            entityManager.SetComponentData(entity, context);
            return FunctionResult.Success;
        }
        return FunctionResult.Failure;
    }

    public static FunctionResult DeletePlayerContext(EntityManager entityManager, int playerId)
    {
        if (GetPlayerContextEntity(entityManager, playerId, out Entity contextEntity, out _) == FunctionResult.Success)
        {
            using (var builder = new EntityQueryBuilder(Allocator.Temp).WithAll<EntityOwner>())
            {
                var unitquery = entityManager.CreateEntityQuery(builder);
                using (var units = unitquery.ToEntityArray(Allocator.Temp))
                {
                    foreach (var unit in units)
                    {
                        EntityOwner unitComponent = entityManager.GetComponentData<EntityOwner>(unit);
                        if (unitComponent.PlayerID == playerId)
                        {
                            entityManager.DestroyEntity(unit);
                        }
                    }
                }
            }
            entityManager.DestroyEntity(contextEntity);
            return FunctionResult.Success;
        }
        return FunctionResult.Failure;
    }

    public static FunctionResult SetPlayerResource(EntityManager entityManager, int playerId, ResourceType resourceType, float amount)
    {
        if (GetPlayerContextEntity(entityManager, playerId, out Entity contextEntity, out _) == FunctionResult.Success)
        {
            var buffer = entityManager.GetBuffer<ResourcePair>(contextEntity);
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].Type == resourceType)
                {
                    buffer[i] = new ResourcePair(resourceType, amount);
                    return FunctionResult.Success;
                }
            }
            return FunctionResult.Failure;
        }
        return FunctionResult.Failure;
    }

    public static FunctionResult GetPlayerResourceByType(EntityManager entityManager, int playerId, ResourceType resourceType, out float amount)
    {
        amount = 0;
        if (GetPlayerContextEntity(entityManager, playerId, out Entity contextEntity, out _) == FunctionResult.Success)
        {
            var buffer = entityManager.GetBuffer<ResourcePair>(contextEntity);
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].Type == resourceType)
                {
                    amount = buffer[i].Amount;
                    return FunctionResult.Success;
                }
            }
            return FunctionResult.Failure;
        }
        return FunctionResult.Failure;
    }

    public static FunctionResult AddPlayerResource(EntityManager entityManager, int playerId, ResourceType resourceType, float amount)
    {
        if (GetPlayerContextEntity(entityManager, playerId, out Entity contextEntity, out _) == FunctionResult.Success)
        {
            var buffer = entityManager.GetBuffer<ResourcePair>(contextEntity);
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].Type == resourceType)
                {
                    buffer[i] = new ResourcePair(resourceType, buffer[i].Amount + amount);
                    return FunctionResult.Success;
                }
            }
            return FunctionResult.Failure;
        }
        return FunctionResult.Failure;
    }
}

