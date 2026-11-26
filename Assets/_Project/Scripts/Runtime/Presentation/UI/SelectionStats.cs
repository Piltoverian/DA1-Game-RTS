using System.Globalization;
using System.Text;
using Unity.Entities;

/// <summary>Read-only capability details, shared by units and valid building entities.</summary>
public static class SelectionStats
{
    public static string Read(EntityManager em, Entity entity, out string summary, GameDataRegistry registry = null)
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
                string storage = accepted.ToString(); text.AppendLine(storage);
                text.AppendLine("Drop-off point; resources enter the player's shared stock.");
                summary = string.IsNullOrEmpty(summary) ? storage : summary + "\n" + storage;
            }
        }
        if (em.HasBuffer<ProductionElement>(entity))
        {
            var offers = em.GetBuffer<ProductionElement>(entity, true);
            int capacity = em.HasComponent<EntityWork>(entity) ? em.GetComponentData<EntityWork>(entity).JobCapacity : 0;
            int queued = em.HasBuffer<ProductionQueueElement>(entity) ? em.GetBuffer<ProductionQueueElement>(entity, true).Length : 0;
            if (offers.Length > 0)
            {
                text.AppendLine("Production offers:");
                for (int i = 0; i < offers.Length; i++)
                    text.Append(offers[i].Kind == ProductionKind.Train ? "Train " : "Research ")
                        .AppendLine(EntityPresentation.JobName(offers[i], registry));
                text.Append("Queue: ").Append(queued).Append('/').Append(capacity).AppendLine();
                if (string.IsNullOrEmpty(summary)) summary = "Production available\nQueue: " + queued + "/" + capacity;
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
                    var prefabs = em.HasBuffer<RegistryPrefabElement>(registryEntity)
                        ? em.GetBuffer<RegistryPrefabElement>(registryEntity, true) : default;
                    if (prefabs.IsCreated && weapon.ProjectilePrefabIndex >= 0 && weapon.ProjectilePrefabIndex < prefabs.Length)
                    {
                        var projectile = prefabs[weapon.ProjectilePrefabIndex].Prefab;
                        if (em.Exists(projectile) && em.HasComponent<ArtilleryBullet>(projectile))
                            text.Append("  Area radius: ").Append(em.GetComponentData<ArtilleryBullet>(projectile).aoeRadius
                                .ToString("0.#", CultureInfo.CurrentCulture)).AppendLine();
                    }
                }
                if (attack.Value.Weapons.Length > 3) text.AppendLine("Additional weapons not shown.");
                if (attack.Value.Weapons.Length == 1)
                    summary = string.Format(CultureInfo.CurrentCulture, "DMG {0:0.#} | RNG {1}\nCD {2:0.##}s",
                        attack.Value.Weapons[0].Damage, range, attack.Value.Weapons[0].Cooldown);
                else if (attack.Value.Weapons.Length > 1)
                {
                    bool same = true;
                    for (int i = 1; i < attack.Value.Weapons.Length; i++)
                        same &= attack.Value.Weapons[i].Damage == attack.Value.Weapons[0].Damage &&
                            attack.Value.Weapons[i].Cooldown == attack.Value.Weapons[0].Cooldown;
                    summary = same ? string.Format(CultureInfo.CurrentCulture, "{0} x {1:0.#} DMG | RNG {2}\nCD {3:0.##}s",
                        attack.Value.Weapons.Length, attack.Value.Weapons[0].Damage, range, attack.Value.Weapons[0].Cooldown)
                        : "Range: " + range + " | Weapons: " + attack.Value.Weapons.Length + "\nSee weapon details";
                }
                if (em.HasComponent<Target>(entity))
                {
                    var target = em.GetComponentData<Target>(entity).targetEntity;
                    bool alive = em.Exists(target) && em.HasComponent<EntityHealth>(target) && em.GetComponentData<EntityHealth>(target).CurrentHP > 0;
                    string state = alive ? "Target acquired" : "No target";
                    text.AppendLine(state);
                }
            }
        }
        return text.ToString().TrimEnd();
    }
}
