using UnityEngine;

public class UIManager : MonoBehaviour
{
    [SerializeField] private int playerId;
    
    public int PlayerId { get { return playerId; } set{ playerId = value; } }
}
