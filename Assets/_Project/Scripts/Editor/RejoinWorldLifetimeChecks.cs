using System;
using System.Reflection;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEditor;
using UnityEngine;
using UnityEngine.LowLevel;
using ConnectionUI = Unity.Multiplayer.Center.NetcodeForEntitiesSetup.ConnectionUI;

// Exercises the same client replacement method used by Rejoin, without scene/UI coroutines.
public static class RejoinWorldLifetimeChecks
{
    [MenuItem("Tools/RTS/Check Rejoin World Lifetime")]
    public static void Run()
    {
        var ownedField = typeof(ConnectionUI).GetField("ownedClient", BindingFlags.Instance | BindingFlags.NonPublic);
        var replace = typeof(ConnectionUI).GetMethod("ReplaceOwnedClientWorld", BindingFlags.Instance | BindingFlags.NonPublic);
        if (ownedField == null || replace == null) throw new InvalidOperationException("The UI client replacement entry point is missing.");

        var originalLoop = PlayerLoop.GetCurrentPlayerLoop();
        var originalDefault = World.DefaultGameObjectInjectionWorld;
        ushort originalPort = ClientServerBootstrap.AutoConnectPort;
        var netcodeType = typeof(ClientServerBootstrap).Assembly.GetType("Unity.NetCode.Netcode");
        var netcodeInstance = netcodeType?.GetField("s_Instance", BindingFlags.Static | BindingFlags.NonPublic);
        bool ownsNetcodeInitialization = netcodeInstance != null && netcodeInstance.GetValue(null) == null;
        World server = null, previous = null, sibling = null, replacement = null;
        GameObject owner = null;
        int checks = 0;
        try
        {
            ClientServerBootstrap.AutoConnectPort = 0;
            using var minimal = new NativeList<SystemTypeIndex>(Allocator.Temp);
            server = ClientServerBootstrap.CreateServerWorld("Rejoin lifetime server", minimal);
            previous = ClientServerBootstrap.CreateClientWorld("Rejoin lifetime previous client");
            sibling = ClientServerBootstrap.CreateClientWorld("Rejoin lifetime sibling client", minimal);
            ulong serverSequence = server.SequenceNumber, previousSequence = previous.SequenceNumber;
            var match = server.EntityManager.CreateEntity(typeof(MatchSession), typeof(RejoinLifetimeCounter));
            server.EntityManager.SetComponentData(match, new MatchSession { Id = 0x7234UL });
            var counter = server.GetOrCreateSystemManaged<RejoinLifetimeCounterSystem>();
            counter.Update();
            Check(server.EntityManager.GetComponentData<RejoinLifetimeCounter>(match).Ticks == 1,
                "The authoritative server must update before reconnect.");

            owner = new GameObject("Rejoin lifetime UI owner"); owner.SetActive(false);
            var ui = owner.AddComponent<ConnectionUI>();
            ownedField.SetValue(ui, previous);
            World.DefaultGameObjectInjectionWorld = previous;
            replacement = (World)replace.Invoke(ui, null);

            Check(!previous.IsCreated, "Rejoin must dispose its previous owned client world.");
            Check(replacement != null && replacement.IsCreated && replacement.IsClient() && !replacement.IsServer()
                && replacement.SequenceNumber != previousSequence, "Rejoin must create a distinct client world.");
            Check(ReferenceEquals(ownedField.GetValue(ui), replacement)
                && ReferenceEquals(World.DefaultGameObjectInjectionWorld, replacement),
                "The UI and presentation default must refer to the replacement client.");
            Check(server.IsCreated && server.SequenceNumber == serverSequence
                && server.EntityManager.Exists(match)
                && server.EntityManager.GetComponentData<MatchSession>(match).Id == 0x7234UL,
                "Replacing a host client must preserve the exact server world, match and state.");
            Check(sibling.IsCreated, "Replacing one client must preserve sibling client worlds.");
            counter.Update();
            Check(server.EntityManager.GetComponentData<RejoinLifetimeCounter>(match).Ticks == 2,
                "The same authoritative server must continue updating after client replacement.");

            // A corrupted UI ownership reference must fail closed instead of disposing the server.
            ownedField.SetValue(ui, server);
            bool rejected = false;
            try { replace.Invoke(ui, null); }
            catch (TargetInvocationException error) when (error.InnerException is InvalidOperationException) { rejected = true; }
            Check(rejected && server.IsCreated && server.SequenceNumber == serverSequence
                && server.EntityManager.Exists(match) && replacement.IsCreated && sibling.IsCreated,
                "A server world assigned as ownedClient must be rejected before any world is disposed.");
            Debug.Log($"Rejoin world lifetime checks passed: {checks} assertions.");
        }
        finally
        {
            if (owner) UnityEngine.Object.DestroyImmediate(owner);
            // Restore the exact original player loop: the test appended all of these worlds synchronously.
            PlayerLoop.SetPlayerLoop(originalLoop);
            Cleanup(replacement); Cleanup(previous); Cleanup(sibling); Cleanup(server);
            // Edit-mode batch has no EnteredEditMode callback to release the package singleton.
            // Only release it if this fixture initialized it, preserving any existing session.
            if (ownsNetcodeInitialization)
                netcodeType.GetMethod("DisposeAfterEnterEditMode", BindingFlags.Static | BindingFlags.NonPublic)?.Invoke(null, null);
            ClientServerBootstrap.AutoConnectPort = originalPort;
            World.DefaultGameObjectInjectionWorld = originalDefault != null && originalDefault.IsCreated ? originalDefault : null;
        }

        void Check(bool condition, string message)
        {
            checks++;
            if (!condition) throw new InvalidOperationException(message);
        }
    }

    static void Cleanup(World world)
    {
        if (world == null) return;
        if (world.IsCreated) world.Dispose();
        // Worlds created with an intentionally empty system list have no NetCode tracker teardown system.
        ClientServerBootstrap.ClientWorlds.Remove(world);
        ClientServerBootstrap.ServerWorlds.Remove(world);
        ClientServerBootstrap.ThinClientWorlds.Remove(world);
    }
}

public struct RejoinLifetimeCounter : IComponentData { public int Ticks; }

[DisableAutoCreation]
public partial class RejoinLifetimeCounterSystem : SystemBase
{
    protected override void OnUpdate()
    {
        using var query = EntityManager.CreateEntityQuery(typeof(RejoinLifetimeCounter));
        if (!query.TryGetSingleton<RejoinLifetimeCounter>(out var value)) return;
        value.Ticks++;
        EntityManager.SetComponentData(query.GetSingletonEntity(), value);
    }
}
