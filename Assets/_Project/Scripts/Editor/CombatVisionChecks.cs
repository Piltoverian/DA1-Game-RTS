using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;

public static class CombatVisionChecks
{
    static int checks;
    static void Assert(bool condition, string message)
    {
        checks++;
        if (!condition) throw new InvalidOperationException(message);
    }

    [MenuItem("Tools/RTS/Check Combat Vision")]
    public static void Run()
    {
        checks = 0;
        using var world = new World("Combat vision", WorldFlags.Game);
        var em = world.EntityManager;
        var grid = new GridComponent { width = 32, height = 32, cellsize = 1, initialized = true };
        em.CreateSingleton(grid);
        using var visible = new NativeParallelMultiHashMap<int, int>(32, Allocator.Persistent);
        using var explored = new NativeParallelMultiHashMap<int, int>(32, Allocator.Persistent);
        em.CreateSingleton(new FogOfWarSpatialSystemData { Visible = visible, Explored = explored, Ready = true });
        using var bucket = new NativeParallelMultiHashMap<int, Entity>(32, Allocator.Persistent);
        em.CreateSingleton(new UnitSpatialBucket { Bucket = bucket });
        using var builder = new BlobBuilder(Allocator.Temp);
        ref var attack = ref builder.ConstructRoot<AttackBlob>();
        attack.ID = "vision-check"; attack.Range = 2; attack.RotationSpeed = 100;
        var weapons = builder.Allocate(ref attack.Weapons, 1);
        weapons[0] = new WeaponDefinitionBlob { Cooldown = 1, ProjectilePrefabIndex = 0 };
        using var blob = builder.CreateBlobAssetReference<AttackBlob>(Allocator.Persistent);
        var registry = em.CreateEntity(typeof(GameDataRegistryComponent));
        em.AddBuffer<RegistryBlobElement>(registry).Add(RegistryBlobElement.Create(blob));
        var projectile = em.CreateEntity(typeof(Prefab), typeof(LocalTransform), typeof(Target));
        em.AddBuffer<RegistryPrefabElement>(registry).Add(new RegistryPrefabElement { Prefab = projectile });
        var enemy = em.CreateEntity(typeof(LocalTransform), typeof(EntityOwner), typeof(EntityHealth));
        em.SetComponentData(enemy, new EntityOwner { PlayerID = 1 });
        em.SetComponentData(enemy, new EntityHealth { CurrentHP = 100 });
        em.SetComponentData(enemy, LocalTransform.FromPosition(8, 0, 8));
        var unit = em.CreateEntity(typeof(LocalTransform), typeof(EntityOwner), typeof(Target),
            typeof(ShootAttack), typeof(MovementAgentComponent), typeof(MovementSteeringComponent), typeof(MoveOverride));
        em.SetComponentData(unit, LocalTransform.FromPosition(2, 0, 2));
        em.SetComponentData(unit, new Target { targetEntity = enemy });
        em.SetComponentData(unit, new ShootAttack { IDs = "vision-check" });
        em.AddBuffer<UnitWeaponSlot>(unit).Add(default);
        em.SetComponentEnabled<MoveOverride>(unit, false);
        var ecb = world.GetOrCreateSystemManaged<BeginFixedStepSimulationEntityCommandBufferSystem>();
        var shoot = world.GetOrCreateSystem<ShootAttackSystem>();
        void Tick() { shoot.Update(world.Unmanaged); ecb.Update(); }
        visible.Add(8 * 32 + 8, 0);
        Tick();
        Assert(math.all(em.GetComponentData<MovementAgentComponent>(unit).currentworldtarget == new float3(8, 0, 8)), "Visible target must be chased.");
        visible.Clear();
        em.SetComponentData(enemy, LocalTransform.FromPosition(15, 0, 15));
        Tick();
        Assert(math.all(em.GetComponentData<MovementAgentComponent>(unit).currentworldtarget == new float3(8, 0, 8)), "Hidden target must not update chase position.");
        visible.Add(15 * 32 + 15, 0);
        Tick();
        Assert(math.all(em.GetComponentData<MovementAgentComponent>(unit).currentworldtarget == new float3(15, 0, 15)), "Revealed target must resume chase.");
        visible.Clear();
        em.DestroyEntity(enemy);
        Tick();
        Assert(em.GetComponentData<Target>(unit).targetEntity == enemy, "Hidden death must not reveal itself before reaching last seen position.");
        em.SetComponentData(unit, LocalTransform.FromPosition(15, 0, 15));
        Tick();
        Assert(em.GetComponentData<Target>(unit).targetEntity == Entity.Null, "Arrival without sight must forget target.");
        Assert(!em.GetComponentData<MovementAgentComponent>(unit).hastarget, "Arrival must stop chase movement.");

        enemy = em.CreateEntity(typeof(LocalTransform), typeof(EntityOwner), typeof(EntityHealth));
        em.SetComponentData(enemy, new EntityOwner { PlayerID = 1 });
        em.SetComponentData(enemy, new EntityHealth { CurrentHP = 100 });
        em.SetComponentData(enemy, LocalTransform.FromPosition(15, 0, 16));
        em.SetComponentData(unit, new Target { targetEntity = enemy });
        Tick();
        using (var query = em.CreateEntityQuery(typeof(Target), ComponentType.Exclude<Prefab>()))
            Assert(query.CalculateEntityCount() == 1, "Hidden target must not cause projectile spawn.");
        Assert(em.GetComponentData<Target>(unit).targetEntity == Entity.Null, "Target without a visible observation must be rejected.");

        em.AddComponentData(unit, new FindTarget { range = 10, timerMax = 0 });
        bucket.Add(16 * 32 + 15, enemy);
        var find = world.GetOrCreateSystem<FindTargetSystem>();
        find.Update(world.Unmanaged);
        Assert(em.GetComponentData<Target>(unit).targetEntity == Entity.Null, "Automatic acquisition must ignore hidden enemies.");
        visible.Add(16 * 32 + 15, 0);
        find.Update(world.Unmanaged);
        Assert(em.GetComponentData<Target>(unit).targetEntity == enemy, "Automatic acquisition must accept visible enemies.");
        // Face the target so the visible firing assertion exercises projectile creation.
        em.SetComponentData(unit, LocalTransform.FromPositionRotation(new float3(15, 0, 15), quaternion.identity));
        Tick();
        using (var query = em.CreateEntityQuery(typeof(Target), ComponentType.Exclude<Prefab>()))
            Assert(query.CalculateEntityCount() == 2, "Visible target in range must still be fired at.");
        visible.Clear();
        var weaponSlots = em.GetBuffer<UnitWeaponSlot>(unit);
        weaponSlots[0] = default;
        em.SetComponentData(enemy, LocalTransform.FromPosition(20, 0, 20));
        Tick();
        Assert(em.GetComponentData<MovementAgentComponent>(unit).hastarget, "Losing sight in firing range must start last-seen travel.");
        Assert(math.all(em.GetComponentData<MovementAgentComponent>(unit).currentworldtarget == new float3(15, 0, 16)), "Last-seen travel must use the observation made while firing.");
        using (var query = em.CreateEntityQuery(typeof(Target), ComponentType.Exclude<Prefab>()))
            Assert(query.CalculateEntityCount() == 2, "Losing sight must stop firing even when weapon is ready.");
        find.Update(world.Unmanaged);
        Assert(em.GetComponentData<Target>(unit).targetEntity == enemy, "Acquisition must preserve an ongoing hidden pursuit.");
        var commands = world.GetOrCreateSystem(typeof(ShootAttackSystem).Assembly.GetType("CommandQueue"));
        CommandDataHelper.AddCommandToQueue(em, 0, unit, CommandType.Move, position: new float3(25, 0, 25));
        commands.Update(world.Unmanaged);
        Tick();
        Assert(em.GetComponentData<Target>(unit).targetEntity == Entity.Null, "Move override must cancel pursuit.");
        Assert(em.GetComponentData<Target>(unit).lastSeenEntity == Entity.Null, "Move override must forget the observation.");
        Assert(math.all(em.GetComponentData<MoveOverride>(unit).targetPosition == new float3(25, 0, 25)), "Combat must preserve the replacement Move command.");
        em.SetComponentEnabled<MoveOverride>(unit, false);
        visible.Add(20 * 32 + 20, 0);
        CommandDataHelper.AddCommandToQueue(em, 0, unit, CommandType.TargetTo, targetEntity: enemy);
        commands.Update(world.Unmanaged);
        Assert(em.GetComponentData<Target>(unit).lastSeenEntity == enemy
            && math.all(em.GetComponentData<Target>(unit).lastSeenPosition == new float3(20, 0, 20)), "Attack command must capture a visible observation immediately.");
        Tick();
        visible.Clear();
        var stopped = em.GetComponentData<MovementAgentComponent>(unit);
        stopped.hastarget = false; stopped.stopReason = MovementStopReason.Arrived;
        em.SetComponentData(unit, stopped);
        Tick();
        Assert(em.GetComponentData<Target>(unit).targetEntity == Entity.Null, "Resolved navigation arrival must finish hidden pursuit.");
        em.RemoveComponent<MovementAgentComponent>(unit);
        em.SetComponentData(unit, new Target { targetEntity = enemy, lastSeenEntity = enemy, lastSeenPosition = new float3(20, 0, 20) });
        Tick();
        Assert(em.GetComponentData<Target>(unit).targetEntity == Entity.Null, "Stationary weapon must forget a hidden target.");
        Debug.Log($"Combat vision checks passed: {checks} assertions.");
    }
}
