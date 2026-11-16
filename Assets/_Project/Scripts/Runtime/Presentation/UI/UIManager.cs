using UnityEngine;
using Unity.Entities;
using Unity.NetCode;

public class UIManager : MonoBehaviour
{
    [SerializeField] private int playerId;
    
    public int PlayerId { get { return playerId; } set{ playerId = value; } }

    public static bool TryGetPlayer(out EntityManager em, out int id)
    {
        em = default;
        id = -1;
        World client = null, local = null;
        foreach (var world in World.All)
        {
            if (!world.IsCreated) continue;
            if (world.IsClient())
            {
                // A single UI cannot choose between multiple client sessions.
                if (client != null) return false;
                client = world;
            }
            else if (!world.IsServer() && (world.Flags & WorldFlags.Game) == WorldFlags.Game)
            {
                if (local != null) return false;
                local = world;
            }
        }
        if (client != null)
        {
            if (!PlayerNetwork.TryLocal(client, out _, out var player)) return false;
            em = client.EntityManager;
            id = player.PlayerId;
            return true;
        }
        var manager = FindAnyObjectByType<UIManager>();
        if (local == null || manager == null) return false;
        em = local.EntityManager;
        id = manager.PlayerId;
        return id >= 0;
    }
}
