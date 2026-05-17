using UnityEngine;

public class SelectableObject : MonoBehaviour
{
    public GameObject visualGameObject;
    public float showScale = 1.5f;
    [SerializeField] protected int playerID;
    private void OnValidate()
    { 
        if(gameObject.GetComponent<UnitAuthoring>()==null)
        { 
            return;
        }
        playerID = gameObject.GetComponent<UnitAuthoring>().playerID;
    }
}
