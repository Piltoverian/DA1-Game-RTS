using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[Serializable]
public struct WeaponDefinitionConfig
{
    public GameObject ProjectilePrefab;
    [Min(0)] public float Damage;
    [Min(0.001f)] public float Cooldown;
    [Min(0.001f)] public float ProjectileSpeed;
    public float3 SpawnOffset;
}
[CreateAssetMenu(menuName = "ScriptableObjects/Abilities/Attack")]
public class AttackAbilityDefinition : AbilityDefinition
{
    [Min(0)] public float Range = 10f;
    [Min(0)] public float RotationSpeed = 8f;
    public List<WeaponDefinitionConfig> Weapons = new();
}

public struct WeaponDefinitionBlob
{
    // Index into RegistryPrefabElement; Entity references must be remapped outside blobs.
    public int ProjectilePrefabIndex;
    public float Damage;
    public float Cooldown;
    public float ProjectileSpeed;
    public float3 SpawnOffset;
}