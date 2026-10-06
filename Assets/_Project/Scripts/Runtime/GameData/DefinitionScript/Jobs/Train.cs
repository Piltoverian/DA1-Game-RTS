using UnityEngine;

[CreateAssetMenu(fileName = "NewTrain", menuName = "ScriptableObjects/Train", order = 4)]
public class Train : Job
{
    public UnitSO OutputUnit;

    private void OnValidate()
    {
       if(OutputUnit!=null)
        {
            this.Icon = OutputUnit.basedSO.Icon;
            this.name = OutputUnit.basedSO.Name;
        }    
    }
}
