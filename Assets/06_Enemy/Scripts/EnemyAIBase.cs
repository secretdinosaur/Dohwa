using UnityEngine;
using UnityEngine.AI;
using System.Collections;

[RequireComponent(typeof(EnemyCore))]
public abstract class EnemyAIBase : MonoBehaviour
{
    [Header("Optimization")]
    public float disableLogicDistance = 100f;

    [Header("Range & Vision")]
    public float detectionRange = 10f;
    public float detectionRangeWithoutWatch = 3f;
    public float detectionMemoryDuration = 10f;
    public float chaseRange = 15f;
    public float wanderSearchRadius = 10f;
    public float fieldOfView = 90f;

    [Header("Movement Tuning")]
    public float chaseSpeedMultiplier = 1.5f;
    public float backWalkSpeedMultiplier = 0.6f;
    public float backWalkDist = 3f;
    public float backWalkMaxDuration = 3f;

    [Header("Combat Common")]
    public float skillRangeMultiplier = 0.9f;
    public float targetUpdateInterval = 0.2f;
    public float firstDetectionDelay = 1.0f;

    protected EnemyCore core;
    public bool isDetectedPlayer = false;
    public bool isMemorizingPlayer = false;
    protected float detectedPlayerTime = -1f;
    protected Coroutine actionCoroutine;
    protected LayerMask wallLayer;

    protected SkillBase selectedSkill;
    protected float nextAttackTime = 0f;
    protected float nextTargetUpdateTime = 0f;
    protected bool isAttacking = false;
    protected Vector3 lastKnownPlayerPos;

    protected bool isCulled = false;

    protected Renderer[] childRenderers;
    protected Animator animator;
    HpBarCanvas hpbar;
    protected virtual void Awake()
    {
        core = GetComponent<EnemyCore>();
        wallLayer = LayerMask.GetMask("Default", "Ground", "Wall");

        childRenderers = GetComponentsInChildren<Renderer>();
        animator = GetComponentInChildren<Animator>();
        hpbar = GetComponentInChildren<HpBarCanvas>();
        core.OnStunned += HandleInterrupt;
        core.OnParried += HandleInterrupt;
        core.OnDeath += HandleDeath;
        core.OnRevived += HandleRevive;
        core.OnStunEnd += HandleStunEnd;
    }

    protected virtual void OnDestroy()
    {
        if (core != null)
        {
            core.OnStunned -= HandleInterrupt;
            core.OnParried -= HandleInterrupt;
            core.OnDeath -= HandleDeath;
            core.OnRevived -= HandleRevive;
            core.OnStunEnd -= HandleStunEnd;
        }
    }

    void HandleStunEnd()
    {
        isAttacking = false;
    }

    private void HandleInterrupt()
    {
        StopActionRoutine();
        core.CmdStopMovement?.Invoke();
    }

    private void HandleDeath()
    {
        HandleInterrupt();
        enabled = false;
    }

    private void HandleRevive()
    {
        enabled = true;
        isDetectedPlayer = false;
        core.CmdWarpToOriginal?.Invoke();
    }

    protected virtual void Update()
    {
        if (core.IsDead || core.currentState == EnemyState.Stun) return;

        if (Player.Instance == null || !Player.Instance.IsAlive)
        {
            if (core.currentState != EnemyState.Idle || isAttacking || isDetectedPlayer)
            {
                StopActionRoutine();
                core.CmdCancelAttack?.Invoke();
                core.CmdStopMovement?.Invoke();
                core.ChangeState(EnemyState.Idle);
                core.SetMoveAnimations(false, false, false);

                isAttacking = false;
                isDetectedPlayer = false;
                isMemorizingPlayer = false;
            }
            return;
        }

        float sqrDistToPlayer = (Player.Instance.transform.position - transform.position).sqrMagnitude;
        float cullSqrDist = disableLogicDistance * disableLogicDistance;

        if (sqrDistToPlayer > cullSqrDist)
        {
            if (!isCulled)
            {
                isCulled = true;
                StopActionRoutine();
                core.CmdStopMovement?.Invoke();
                core.ChangeState(EnemyState.Idle);
                SetVisualsEnabled(false);
            }
            return;
        }

        if (isCulled)
        {
            isCulled = false;
            SetVisualsEnabled(true);
        }

        if (core.currentState == EnemyState.Attack) return;

        bool isCurrentlySeeing = core.isForceDetected || CheckForPlayer();

        if (isCurrentlySeeing && core.isForceDetected && (isDetectedPlayer || isMemorizingPlayer))
        {
            core.isForceDetected = false;
        }

        if (isCurrentlySeeing)
        {
            if (!isDetectedPlayer)
            {
                StopActionRoutine();
                core.CmdStopMovement?.Invoke();
                nextTargetUpdateTime = 0f;

                if (!isMemorizingPlayer)
                {
                    core.TriggerPlayerDetected();
                    nextAttackTime = Mathf.Max(nextAttackTime, Time.time + firstDetectionDelay);
                }
            }

            isDetectedPlayer = true;
            isMemorizingPlayer = true;
            detectedPlayerTime = Time.time;
            lastKnownPlayerPos = Player.Instance.transform.position;

            core.SetPlayerDetection(true);
        }
        else
        {
            isDetectedPlayer = false;

            if (isMemorizingPlayer && (Time.time - detectedPlayerTime > detectionMemoryDuration))
            {
                isMemorizingPlayer = false;
                core.SetPlayerDetection(false);
            }
        }

        if (isDetectedPlayer || isMemorizingPlayer)
        {
            core.CmdLookAt?.Invoke(lastKnownPlayerPos);
        }

        HandleBehaviorLogic(Vector3.Distance(transform.position, Player.Instance.transform.position));
    }

    private void SetVisualsEnabled(bool state)
    {
        if (animator != null)
        {
            animator.enabled = state;
        }

        for (int i = 0; i < childRenderers.Length; i++)
        {
            if (childRenderers[i] != null)
            {
                childRenderers[i].enabled = state;
            }
        }
        if(hpbar)
            hpbar.gameObject.SetActive(state);

    }

    protected abstract void HandleBehaviorLogic(float distance);

    protected bool CheckForPlayer()
    {
        float distToPlayer = Vector3.Distance(transform.position, Player.Instance.transform.position);

        if (distToPlayer <= detectionRangeWithoutWatch)
            return !Physics.Linecast(transform.position + Vector3.up * 2, Player.Instance.transform.position + Vector3.up, wallLayer);

        Vector3 dir = (Player.Instance.transform.position - transform.position).normalized;

        float activeRange = (isDetectedPlayer || isMemorizingPlayer) ? chaseRange : detectionRange;

        if (distToPlayer <= activeRange && (Vector3.Angle(transform.forward, dir) < fieldOfView / 2f || isMemorizingPlayer))
        {
            return !Physics.Linecast(transform.position + Vector3.up * 2, Player.Instance.transform.position + Vector3.up, wallLayer);
        }

        return false;
    }

    protected void StartWander() { if (actionCoroutine == null) actionCoroutine = StartCoroutine(WanderRoutine()); }
    protected void StopActionRoutine() { if (actionCoroutine != null) { StopCoroutine(actionCoroutine); actionCoroutine = null; } }

    private IEnumerator WanderRoutine()
    {
        while (true)
        {
            core.ChangeState(EnemyState.Idle);
            core.CmdStopMovement?.Invoke();
            core.SetMoveAnimations(false, false, false);

            yield return new WaitForSeconds(UnityEngine.Random.Range(2f, 4f));

            Vector3 origin = core.QueryOriginalPosition?.Invoke() ?? transform.position;
            Vector3 target = UnityEngine.Random.insideUnitSphere * wanderSearchRadius + origin;

            if (NavMesh.SamplePosition(target, out NavMeshHit hit, wanderSearchRadius, NavMesh.AllAreas))
            {
                core.CmdMoveTo?.Invoke(hit.position, 1f);
                core.SetMoveAnimations(false, true, false);

                float timeout = 5f;
                float elapsed = 0f;

                while (elapsed < timeout && (core.QueryIsPathPending?.Invoke() ?? false))
                {
                    elapsed += Time.deltaTime;
                    yield return null;
                }
            }
        }
    }

    public IEnumerator BackWalkRoutine(float distance)
    {
        core.ChangeState(EnemyState.BackWalk);
        core.CmdStopMovement?.Invoke();
        core.SetMoveAnimations(false, false, true);

        float movedDistance = 0f;
        float elapsedDuration = 0f;
        Vector3 startPos = transform.position;

        while (movedDistance < distance && elapsedDuration < backWalkMaxDuration)
        {
            elapsedDuration += Time.deltaTime;
            Vector3 backDir = (-transform.forward).normalized;

            float step = core.BaseMovementSpeed * backWalkSpeedMultiplier * Time.deltaTime;

            if (!Physics.SphereCast(transform.position + Vector3.up, 0.3f, backDir, out _, step, wallLayer))
            {
                core.CmdMoveDirection?.Invoke(backDir, backWalkSpeedMultiplier);
                movedDistance += step;
            }
            else
            {
                break;
            }

            yield return null;
        }

        core.SetMoveAnimations(false, false, false);
        core.CmdTrySnapToNavMesh?.Invoke();
        core.ChangeState(EnemyState.Idle);
        actionCoroutine = null;
    }

    public void LookPlayer(float lookDuration)
    {
        StartCoroutine(LookAtPlayerRoutine(lookDuration));
    }

    private IEnumerator LookAtPlayerRoutine(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (Player.Instance != null && core != null)
            {
                core.CmdLookAt?.Invoke(Player.Instance.transform.position);
            }
            elapsed += Time.deltaTime;
            yield return null;
        }
    }
}