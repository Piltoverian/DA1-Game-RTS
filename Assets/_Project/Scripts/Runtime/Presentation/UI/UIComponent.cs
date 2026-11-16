using Unity.Entities;
using UnityEngine;

public class UIComponent : MonoBehaviour
{
    [SerializeField] protected int playerId;
    protected bool TryGetPlayer(out EntityManager em)
    {
        return UIManager.TryGetPlayer(out em, out playerId);
    }
}
