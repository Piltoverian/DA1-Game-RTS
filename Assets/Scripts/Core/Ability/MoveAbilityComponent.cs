using UnityEngine;
using UnityEngine.AI;

public class MoveAbilityComponent : MonoBehaviour, Ability
{
    NavMeshAgent agent = null;
    private void Awake()
    {
        agent = gameObject.GetComponent<NavMeshAgent>();
    }
    public void OnMoveToScreen(Vector2 position)
    {
        agent.isStopped = false;
        Vector3 pos = Utility.ConvertCamToWorld(position);

        if (pos != Vector3.zero)
        {
            gameObject.GetComponent<NavMeshAgent>().SetDestination(pos);
        }
    }

    public void OnMoveToWorld(Vector3 position)
    {
        agent.isStopped = false;
        NavMeshHit hit;
        if (NavMesh.SamplePosition(position, out hit, 5.0f, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
        }
        else
        {
            Debug.Log("SamplePosition FAILED at: " + position);
        }
    }

    public void ReturnToIdle()
    {
    }

    public void Stop()
    {
        agent.isStopped=true;
        agent.ResetPath();
    }

    public void FixedUpdate()
    {
    }
}