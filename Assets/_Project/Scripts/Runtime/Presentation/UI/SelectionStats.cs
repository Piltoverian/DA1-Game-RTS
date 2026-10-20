using System.Globalization;
using System.Text;
using Unity.Entities;

/// <summary>Read-only capability details, shared by units and valid building entities.</summary>
public static class SelectionStats
{
    public static string Read(EntityManager em, Entity entity, out string summary)
    {
        summary = string.Empty;
        var text = new StringBuilder();
        if (em.HasBuffer<BuildingTagElement>(entity))
        {
            text.Append("Type: ");
            foreach (var tag in em.GetBuffer<BuildingTagElement>(entity, true)) text.Append(tag.Value).Append(' ');
            text.AppendLine();
        }
        if (em.HasComponent<EntityOwner>(entity)) text.Append("Owner: Player ").Append(em.GetComponentData<EntityOwner>(entity).PlayerID).AppendLine();
        if (em.HasComponent<BuildingComponent>(entity))
        {
            int capacity = em.GetComponentData<BuildingComponent>(entity).PopulationCapacity;
            if (capacity > 0) { summary = "Population +" + capacity; text.AppendLine(summary); }
        }
        if (em.HasBuffer<StorageResource>(entity))
        {
            var resources = em.GetBuffer<StorageResource>(entity, true);
            if (resources.Length > 0)
            {
                var accepted = new StringBuilder("Accepts: ");
                for (int i = 0; i < resources.Length; i++)
                { if (i > 0) accepted.Append(" / "); accepted.Append(resources[i].Type); }
                summary = accepted.ToString(); text.AppendLine(summary);
            }
        }
        // ShootAttack is enableable, but immutable attack stats remain meaningful when idle.
        if (em.HasComponent<ShootAttack>(entity) && ProductionJobs.TryRegistry(em, out var registryEntity))
        {
            var blobs = em.GetBuffer<RegistryBlobElement>(registryEntity, true);
            var attack = blobs.GetBlobByID<AttackBlob>(em.GetComponentData<ShootAttack>(entity).IDs, out var found);
            if (found == FunctionResult.Success)
            {
                string range = attack.Value.Range.ToString("0.#", CultureInfo.CurrentCulture);
                summary = "Range: " + range;
                text.AppendLine(summary);
                for (int i = 0; i < attack.Value.Weapons.Length && i < 3; i++)
                {
                    ref var weapon = ref attack.Value.Weapons[i];
                    text.Append("Weapon ").Append(i + 1).Append(": ")
                        .Append(weapon.Damage.ToString("0.#", CultureInfo.CurrentCulture)).Append(" dmg / ")
                        .Append(weapon.Cooldown.ToString("0.##", CultureInfo.CurrentCulture)).AppendLine("s");
                }
                if (attack.Value.Weapons.Length > 3) text.AppendLine("Additional weapons not shown.");
                if (attack.Value.Weapons.Length == 1)
                    summary = string.Format(CultureInfo.CurrentCulture, "DMG {0:0.#} | RNG {1}", attack.Value.Weapons[0].Damage, range);
                if (em.HasComponent<Target>(entity))
                    text.AppendLine(em.Exists(em.GetComponentData<Target>(entity).targetEntity) ? "Target acquired" : "No target");
            }
        }
        return text.ToString().TrimEnd();
    }
}
