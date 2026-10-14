using System;
using System.Collections.Generic;

namespace RTS.DataValidation
{
    public static class ResourceCostNormalizer
    {
        public static bool TryNormalize(IReadOnlyList<ResourcePair> source, out List<ResourcePair> normalized)
        {
            normalized = new List<ResourcePair>();
            if (source == null) return false;
            var positions = new Dictionary<ResourceType, int>();
            var totals = new Dictionary<ResourceType, double>();
            for (int i = 0; i < source.Count; i++)
            {
                var cost = source[i];
                if (!Enum.IsDefined(typeof(ResourceType), cost.Type)
                    || float.IsNaN(cost.Amount) || float.IsInfinity(cost.Amount) || cost.Amount < 0)
                    return false;
                if (positions.TryGetValue(cost.Type, out int position))
                {
                    double sum = totals[cost.Type] + cost.Amount;
                    if (sum > float.MaxValue) return false;
                    totals[cost.Type] = sum;
                    normalized[position] = new ResourcePair(cost.Type, (float)sum);
                }
                else
                {
                    positions.Add(cost.Type, normalized.Count);
                    totals.Add(cost.Type, cost.Amount);
                    normalized.Add(cost);
                }
            }
            return true;
        }

        public static bool IsNormalized(IReadOnlyList<ResourcePair> source)
        {
            if (!TryNormalize(source, out var result) || source.Count != result.Count) return false;
            for (int i = 0; i < source.Count; i++)
                if (source[i].Type != result[i].Type || source[i].Amount != result[i].Amount) return false;
            return true;
        }
    }
}
