using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyMovement : MonoBehaviour
{
    public float baseMoveSpeed = 3f;
    public float rotationSpeed = 5f;

    private NavMeshAgent agent;
    private Animator animator;
    private EnemyCore core;

    public Vector3 OriginalPosition { get; private set; }
    private Quaternion originalRotation;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        core = GetComponent<EnemyCore>();

        OriginalPosition = transform.position;
        originalRotation = transform.rotation;

        if (agent != null) { agent.speed = baseMoveSpeed; agent.enabled = false; }
        core.BaseMovementSpeed = baseMoveSpeed;
        TrySnapToNavMesh();
        if (agent != null) agent.enabled = true;
    }

    private void OnEnable()
    {
        if (core == null) return;
        core.CmdMoveTo += MoveTo;
        core.CmdStopMovement += StopMovement;
        core.CmdLookAt += LookAtTarget;
        core.CmdMoveDirection += MoveDirection;
        core.CmdWarpToOriginal += WarpToOriginal;
        core.CmdTrySnapToNavMesh += () => TrySnapToNavMesh();

        core.OnDeath += StopMovement;
        core.QueryIsPathPending = IsPathPendingOrMoving;
        core.QueryOriginalPosition = () => OriginalPosition;
    }

    private void OnDisable()
    {
        if (core == null) return;
        core.CmdMoveTo -= MoveTo;
        core.CmdStopMovement -= StopMovement;
        core.CmdLookAt -= LookAtTarget;
        core.CmdMoveDirection -= MoveDirection;
        core.CmdWarpToOriginal -= WarpToOriginal;
        core.CmdTrySnapToNavMesh -= () => TrySnapToNavMesh();

        core.OnDeath -= StopMovement;

        core.QueryIsPathPending = null;
        core.QueryOriginalPosition = null;
    }

    private float checkRadius = 0.1f;
    private float checkHeight = 1.0f;
    private void OnAnimatorMove()
    {
        if (animator == null) return;

        Vector3 delta = animator.deltaPosition;
        float moveDist = delta.magnitude;

        if (moveDist < 0.0001f) return;

        Vector3 direction = delta / moveDist;
        Vector3 origin = transform.position + (Vector3.up * checkHeight);
        if (core == null || !core.isIgnoringPlayer)
        {
            if (Physics.SphereCast(origin, checkRadius, direction, out RaycastHit hit, moveDist, LayerMask.GetMask("Player")))
            {
                transform.rotation *= animator.deltaRotation;
                return;
            }
        }

        if (agent != null && agent.isActiveAndEnabled)
        {
            Vector3 nextPos = transform.position + delta;
            if (NavMesh.SamplePosition(nextPos, out _, 2f, NavMesh.AllAreas))
                agent.nextPosition = nextPos;
        }
        else
        {
            transform.position += delta;
        }

        transform.rotation *= animator.deltaRotation;
    }

    private void MoveTo(Vector3 destination, float speedMultiplier)
    {
        if (agent == null || !agent.isActiveAndEnabled) return;
        agent.isStopped = false;
        agent.speed = baseMoveSpeed * speedMultiplier;
        agent.SetDestination(destination);
    }

    private void StopMovement()
    {
        if (agent == null || !agent.isActiveAndEnabled) return;
        core.CmdTrySnapToNavMesh?.Invoke();
        agent.isStopped = true;
        agent.velocity = Vector3.zero;
        agent.ResetPath();
    }

    private void LookAtTarget(Vector3 targetPos)
    {
        Vector3 dir = (targetPos - transform.position).normalized;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.1f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * rotationSpeed);
    }

    private void MoveDirection(Vector3 direction, float speedMultiplier)
    {
        transform.position += direction.normalized * (baseMoveSpeed * speedMultiplier) * Time.deltaTime;
    }

    private void WarpToOriginal()
    {
        transform.position = OriginalPosition;
        transform.rotation = originalRotation;
        if (agent != null) { agent.enabled = true; agent.Warp(OriginalPosition); agent.ResetPath(); }
    }

    private bool IsPathPendingOrMoving()
    {
        if (agent == null || !agent.isActiveAndEnabled) return false;
        return agent.pathPending || agent.remainingDistance > 0.5f;
    }

    private bool TrySnapToNavMesh(float maxSearch = 6f, float initialSampleRadius = 0.5f, int areaMask = NavMesh.AllAreas)
    {
        if (agent == null) return false;
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, maxSearch, areaMask))
        {
            if (agent.enabled) agent.Warp(hit.position);
            else transform.position = hit.position;
            return true;
        }
        return false;
    }
}