using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyMovement : MonoBehaviour
{
    private System.Action snapHandler; // 람다는 같은 인스턴스로만 구독 해제가 되므로 보관
    public float baseMoveSpeed = 3f;
    public float rotationSpeed = 10f;

    private NavMeshAgent agent;
    private Animator animator;
    private EnemyCore core;
    private CharacterController characterController;

    public Vector3 OriginalPosition { get; private set; }
    private Quaternion originalRotation;

    private float overrideLookTimer = 0f;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        core = GetComponent<EnemyCore>();
        characterController = GetComponent<CharacterController>();

        OriginalPosition = transform.position;
        originalRotation = transform.rotation;

        if (agent != null)
        {
            agent.speed = baseMoveSpeed;
            agent.enabled = false;
            agent.updateRotation = false; // NavMesh 자체 회전 끄기
        }

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
        snapHandler ??= () => TrySnapToNavMesh();
        core.CmdTrySnapToNavMesh += snapHandler;
        core.CmdFastLookAt += FastLookAtTarget;
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
        core.CmdTrySnapToNavMesh -= snapHandler;
        core.CmdFastLookAt -= FastLookAtTarget;
        core.OnDeath -= StopMovement;
        core.QueryIsPathPending = null;
        core.QueryOriginalPosition = null;
    }

    private void Update()
    {
        if (overrideLookTimer > 0f)
        {
            overrideLookTimer -= Time.deltaTime;
            return;
        }

        // 💡 [핵심 해결 로직]
        // 공격 중이거나 스턴 상태일 때는 딱 붙어있어도 억지로 방향을 덮어씌우지 않도록 회전 차단!
        // 이 코드가 있어야 루트모션 회전이 씹히지 않고 온전히 360도 돌아갑니다.
        if (core != null && (core.currentState == EnemyState.Attack || core.currentState == EnemyState.Stun))
        {
            return;
        }

        if (agent != null && agent.isActiveAndEnabled && !agent.isStopped && agent.desiredVelocity.sqrMagnitude > 0.1f)
        {
            Vector3 moveDir = agent.desiredVelocity.normalized;
            moveDir.y = 0f;
            if (moveDir != Vector3.zero)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(moveDir), Time.deltaTime * rotationSpeed);
            }
        }
    }

    private float checkRadius = 0.1f;
    private float checkHeight = 1.0f;

    private void OnAnimatorMove()
    {
        if (animator == null) return;

        // 1. 회전(루트모션)은 이동(충돌)과 무관하게 무조건 최우선 적용
        transform.rotation *= animator.deltaRotation;

        Vector3 delta = animator.deltaPosition;
        // 2. 이동 거리가 0에 가깝다면 (제자리 360도 회전 등) 충돌 연산을 할 필요 없이 즉시 종료
        if (delta.magnitude < 0.0001f) return;

        if (core == null || !core.isIgnoringPlayer)
        {
            Vector3 direction = delta.normalized;
            Vector3 origin = transform.position + (Vector3.up * checkHeight);

            if (Physics.SphereCast(origin, checkRadius, direction, out RaycastHit hit, delta.magnitude, LayerMask.GetMask("Player", "Enemy", "Default")))
            {
                delta = Vector3.ProjectOnPlane(delta, hit.normal);
                delta.y = 0f;
            }
        }

        if (agent != null && agent.isActiveAndEnabled)
        {
            Vector3 nextPos = transform.position + delta;
            if (NavMesh.SamplePosition(nextPos, out _, 2f, NavMesh.AllAreas))
            {
                agent.nextPosition = nextPos;
            }
        }
        else
        {
            transform.position += delta;
        }
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
        if (core != null && core.isTakingDoT) return;
        Vector3 dir = (targetPos - transform.position).normalized;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.1f)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * rotationSpeed);
            overrideLookTimer = 0.1f;
        }
    }

    private void FastLookAtTarget(Vector3 targetPos)
    {
        Vector3 dir = (targetPos - transform.position).normalized;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.1f)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * (rotationSpeed * 2f));
            overrideLookTimer = 0.1f;
        }
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