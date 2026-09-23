using Unity.Collections;
using Unity.Entities;

public enum TechUnlockStatus : byte
{
    Available,
    Pending,
    Completed,
    Locked,
    NotFound
}

public enum UnitUnlockStatus : byte
{
    Available,
    Locked,
    NotFound,
}

public static class TechUnlockHelper
{
    public static bool IsTechCompleted(EntityManager em, Entity playerEntity, in FixedString64Bytes techId)
    {
        if (!em.Exists(playerEntity) || !em.HasBuffer<PlayerTechnology>(playerEntity)) return false;
        var techs = em.GetBuffer<PlayerTechnology>(playerEntity);
        for (int i = 0; i < techs.Length; i++)
        {
            if (techs[i].ID == techId) return true;
        }
        return false;
    }

    public static bool IsTechPending(EntityManager em, Entity playerEntity, in FixedString64Bytes techId, out Entity producer)
    {
        producer = Entity.Null;
        if (!em.Exists(playerEntity) || !em.HasBuffer<PlayerPendingTech>(playerEntity)) return false;
        var pending = em.GetBuffer<PlayerPendingTech>(playerEntity);
        for (int i = 0; i < pending.Length; i++)
        {
            if (pending[i].TechID == techId)
            {
                producer = pending[i].Producer;
                return true;
            }
        }
        return false;
    }

    public static TechUnlockStatus CheckTechUnlockStatus(EntityManager em, Entity playerEntity, in FixedString64Bytes techId)
    {
        if (!em.Exists(playerEntity) || !em.HasComponent<PlayerContext>(playerEntity))
            return TechUnlockStatus.NotFound;

        var context = em.GetComponentData<PlayerContext>(playerEntity);

        if (IsTechCompleted(em, playerEntity, techId)) return TechUnlockStatus.Completed;
        if (IsTechPending(em, playerEntity, techId, out _)) return TechUnlockStatus.Pending;

        if (!ProductionJobs.TryRegistry(em, out var registryEntity)) return TechUnlockStatus.NotFound;
        var registry = em.GetBuffer<RegistryBlobElement>(registryEntity, true);
        var civ = registry.GetBlobByID<CivBlob>(context.CivID, out var foundCiv);
        if (foundCiv != FunctionResult.Success) return TechUnlockStatus.NotFound;

        var tree = registry.GetBlobByID<TechTreeBlob>(civ.Value.TechTreeID, out var foundTree);
        if (foundTree != FunctionResult.Success) return TechUnlockStatus.NotFound;

        bool foundNode = false;
        for (int i = 0; i < tree.Value.Nodes.Length; i++)
        {
            ref var node = ref tree.Value.Nodes[i];
            if (node.TechID == techId)
            {
                foundNode = true;
                for (int p = 0; p < node.Prerequisites.Length; p++)
                {
                    if (!IsTechCompleted(em, playerEntity, node.Prerequisites[p]))
                        return TechUnlockStatus.Locked;
                }
                break;
            }
        }

        if (foundNode) return TechUnlockStatus.Available;

        return TechUnlockStatus.NotFound;
    }

    public static TechUnlockStatus CheckTechUnlockStatus(EntityManager em, int playerId, in FixedString64Bytes techId)
    {
        if (PlayerContextHelper.GetPlayerContextEntity(em, playerId, out var playerEntity, out _) != FunctionResult.Success)
            return TechUnlockStatus.NotFound;

        return CheckTechUnlockStatus(em, playerEntity, techId);
    }

    public static UnitUnlockStatus CheckUnitUnlockStatus(EntityManager em, Entity playerEntity, in FixedString64Bytes unitId)
    {
        if (!em.Exists(playerEntity) || !em.HasComponent<PlayerContext>(playerEntity))
            return UnitUnlockStatus.NotFound;

        var context = em.GetComponentData<PlayerContext>(playerEntity);

        if (!ProductionJobs.TryRegistry(em, out var registryEntity)) return UnitUnlockStatus.NotFound;
        var registry = em.GetBuffer<RegistryBlobElement>(registryEntity, true);
        var civ = registry.GetBlobByID<CivBlob>(context.CivID, out var foundCiv);
        if (foundCiv != FunctionResult.Success) return UnitUnlockStatus.NotFound;

        for (int i = 0; i < civ.Value.UnitUnlocks.Length; i++)
        {
            ref var unlock = ref civ.Value.UnitUnlocks[i];
            if (unlock.UnitID == unitId)
            {
                for (int p = 0; p < unlock.Prerequisites.Length; p++)
                {
                    if (!IsTechCompleted(em, playerEntity, unlock.Prerequisites[p]))
                        return UnitUnlockStatus.Locked;
                }
                return UnitUnlockStatus.Available;
            }
        }

        return UnitUnlockStatus.NotFound;
    }

    public static UnitUnlockStatus CheckUnitUnlockStatus(EntityManager em, int playerId, in FixedString64Bytes unitId)
    {
        if (PlayerContextHelper.GetPlayerContextEntity(em, playerId, out var playerEntity, out _) != FunctionResult.Success)
            return UnitUnlockStatus.NotFound;

        return CheckUnitUnlockStatus(em, playerEntity, unitId);
    }

    public static void GetMissingPrerequisitesForTech(EntityManager em, Entity playerEntity, in FixedString64Bytes techId, ref NativeList<FixedString64Bytes> missingList)
    {
        missingList.Clear();
        if (!em.Exists(playerEntity) || !em.HasComponent<PlayerContext>(playerEntity)) return;
        var context = em.GetComponentData<PlayerContext>(playerEntity);

        if (!ProductionJobs.TryRegistry(em, out var registryEntity)) return;
        var registry = em.GetBuffer<RegistryBlobElement>(registryEntity, true);
        var civ = registry.GetBlobByID<CivBlob>(context.CivID, out var foundCiv);
        if (foundCiv != FunctionResult.Success) return;

        var tree = registry.GetBlobByID<TechTreeBlob>(civ.Value.TechTreeID, out var foundTree);
        if (foundTree != FunctionResult.Success) return;

        for (int i = 0; i < tree.Value.Nodes.Length; i++)
        {
            ref var node = ref tree.Value.Nodes[i];
            if (node.TechID == techId)
            {
                for (int p = 0; p < node.Prerequisites.Length; p++)
                {
                    if (!IsTechCompleted(em, playerEntity, node.Prerequisites[p]))
                        missingList.Add(node.Prerequisites[p]);
                }
                break;
            }
        }
    }

    public static void GetMissingPrerequisitesForTech(EntityManager em, int playerId, in FixedString64Bytes techId, ref NativeList<FixedString64Bytes> missingList)
    {
        if (PlayerContextHelper.GetPlayerContextEntity(em, playerId, out var playerEntity, out _) != FunctionResult.Success)
        {
            missingList.Clear();
            return;
        }

        GetMissingPrerequisitesForTech(em, playerEntity, techId, ref missingList);
    }

    public static void GetMissingPrerequisitesForUnit(EntityManager em, Entity playerEntity, in FixedString64Bytes unitId, ref NativeList<FixedString64Bytes> missingList)
    {
        missingList.Clear();
        if (!em.Exists(playerEntity) || !em.HasComponent<PlayerContext>(playerEntity)) return;
        var context = em.GetComponentData<PlayerContext>(playerEntity);

        if (!ProductionJobs.TryRegistry(em, out var registryEntity)) return;
        var registry = em.GetBuffer<RegistryBlobElement>(registryEntity, true);
        var civ = registry.GetBlobByID<CivBlob>(context.CivID, out var foundCiv);
        if (foundCiv != FunctionResult.Success) return;

        for (int i = 0; i < civ.Value.UnitUnlocks.Length; i++)
        {
            ref var unlock = ref civ.Value.UnitUnlocks[i];
            if (unlock.UnitID == unitId)
            {
                for (int p = 0; p < unlock.Prerequisites.Length; p++)
                {
                    if (!IsTechCompleted(em, playerEntity, unlock.Prerequisites[p]))
                        missingList.Add(unlock.Prerequisites[p]);
                }
                break;
            }
        }
    }

    public static void GetMissingPrerequisitesForUnit(EntityManager em, int playerId, in FixedString64Bytes unitId, ref NativeList<FixedString64Bytes> missingList)
    {
        if (PlayerContextHelper.GetPlayerContextEntity(em, playerId, out var playerEntity, out _) != FunctionResult.Success)
        {
            missingList.Clear();
            return;
        }

        GetMissingPrerequisitesForUnit(em, playerEntity, unitId, ref missingList);
    }
}
