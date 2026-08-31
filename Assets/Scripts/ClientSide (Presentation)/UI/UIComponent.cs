using Unity.IO.LowLevel.Unsafe;
using UnityEngine;

public class UIComponent : MonoBehaviour
{
    [SerializeField] protected int playerId;
    protected virtual void Start()
    {
        UIManager uiManager = FindAnyObjectByType<UIManager>();
        if(uiManager != null)
        {
            playerId = uiManager.PlayerId;
            Debug.Log($"UIComponent found UIManager with PlayerId: {playerId}");
        }
        else
        {
            Debug.LogWarning("UIComponent could not find a UIManager in the scene.");
        }   
    }
}
