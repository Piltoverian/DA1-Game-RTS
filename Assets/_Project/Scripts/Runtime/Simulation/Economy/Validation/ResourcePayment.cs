using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

public static class ResourcePayment
{
    public static bool CanAfford(EntityManager em, int playerId, NativeList<ResourcePair> costs)
    {
        if (playerId < 0 || PlayerContextHelper.GetPlayerContextEntity(em, playerId, out _, out _) != FunctionResult.Success)
            return false;
        for (int i = 0; i < costs.Length; i++)
        {
            var cost = costs[i];
            // SO costs are normalized before baking. Refuse a malformed runtime
            // buffer so two rows cannot each pass against the same balance.
            for (int j = 0; j < i; j++)
                if (costs[j].Type == cost.Type) return false;
            if (!math.isfinite(cost.Amount) || cost.Amount < 0
                || PlayerContextHelper.GetPlayerResourceByType(em, playerId, cost.Type, out float balance) != FunctionResult.Success
                || !math.isfinite(balance) || balance < cost.Amount) return false;
        }
        return true;
    }

    public static bool TryPay(EntityManager em, int playerId, NativeList<ResourcePair> costs, out NativeList<ResourcePair> paid)
    {
        paid = new NativeList<ResourcePair>(Allocator.Temp);
        if (!CanAfford(em, playerId, costs)) return false;
        for (int i = 0; i < costs.Length; i++)
        {
            var cost = costs[i];
            if (PlayerContextHelper.AddPlayerResource(em, playerId, cost.Type, -cost.Amount) != FunctionResult.Success)
            {
                Refund(em, playerId, paid);
                paid.Clear();
                return false;
            }
            paid.Add(cost);
        }
        return true;
    }

    public static void Refund(EntityManager em, int playerId, NativeList<ResourcePair> paid)
    {
        for (int i = 0; i < paid.Length; i++)
            PlayerContextHelper.AddPlayerResource(em, playerId, paid[i].Type, paid[i].Amount);
    }
}
