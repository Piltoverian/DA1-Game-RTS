using UnityEngine;
using UnityEngine.AI;



public class MoveAbilityComponent : MonoBehaviour, Ability
{
    NavMeshAgent agent = null;
    private void Awake()
    {
        agent = gameObject.GetComponent<NavMeshAgent>();
        agent.radius = 0.5f;
        agent.avoidancePriority = Random.Range(30, 60);
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;
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
    }

    bool IsMoving(NavMeshAgent agent)
    {
        return !agent.pathPending &&
               agent.remainingDistance <= agent.stoppingDistance &&
               (!agent.hasPath || agent.velocity.sqrMagnitude == 0f);
    }


    public void FixedUpdate()
    {
    }
}