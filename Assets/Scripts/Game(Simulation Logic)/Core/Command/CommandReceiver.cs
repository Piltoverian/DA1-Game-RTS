using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;

public abstract class CommandReceiver: MonoBehaviour
{
    Ability currentAbility;

    public void SetAbility(Ability ability)
    {
        if (ability == null)
        {
            Debug.LogError("Attempted to set a null ability.");
            return;
        }
        if(currentAbility == ability)
        {
            Debug.LogWarning("Attempted to set the same ability that's already active.");
            return;
        }
        if (currentAbility != null)
        {
            currentAbility.ReturnToIdle();
        }
        currentAbility = ability;
    }

    public void ClearAbility()
    {
        if (currentAbility != null)
        {
            currentAbility.ReturnToIdle();
            currentAbility = null;
        }
    }

    public bool IsBusy()
    {
        return currentAbility != null;
    }

    public abstract void ReceiveCommand(ICommand command);
}
