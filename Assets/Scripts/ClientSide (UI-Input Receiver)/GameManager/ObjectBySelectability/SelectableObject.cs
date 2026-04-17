using UnityEngine;

public class SelectableObject : MonoBehaviour
{
    public GameObject visualGameObject;
    public float showScale = 1.5f;
    private void Start()
    {
       if(GetComponent<CommandReceiver>()==null)
       {
            Debug.LogWarning(gameObject.name+" do not have CommandReceiver.\n"+"If not have CommandReceiver,this is the view only object,which means it can't receive any command");
       }
    }
}
