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
    public float strafeSpeedMultiplier = 1f;
    public float strafeMinDuration = 1.0f;
    public float strafeMaxDuration = 5.0f;

    [Header("Combat Common")]
    public float skillRangeMultiplier = 0.9f;
    public float targetUpdateInterval = 0.2f;
    public float firstDetectionDelay = 1.0f;

    [Header("Predictive Chase (강강술래 방지)")]
    public float maxPredictionTime = 0.7f;
    protected Vector3 prevPlayerPos;
    protected Vector3 playerVelocity;

    protected EnemyCore core;
    public bool isDetectedPlayer = false;
    public bool isMemorizingPlayer = false;
    protected bool isRunningForward = false;

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

    // === 새로 추가된 필드: 공격 공백(Idle) 시 스킬 재검색 설정 ===
    [Header("AI Behavior: Re-evaluate Skill")]
    [Tooltip("마지막 공격(완료)으로부터 이 시간(초) 이상 지나면 스킬을 재검색합니다.")]
    public float attackIdleReselectDelay = 3f;
    [Tooltip("스킬 재검색 발생 후 반복 호출을 방지하기 위한 최소 간격(초)")]
    public float reselectCooldown = 1f;

    protected float lastCompletedAttackTime = -9999f;
    protected float lastReselectTime = -9999f;

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

        // 공격 완료 이벤트 구독: 마지막 공격 완료 시각 기록
        core.OnAttackCompleted += HandleOnAttackCompleted;
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

            core.OnAttackCompleted -= HandleOnAttackCompleted;
        }
    }

    void HandleStunEnd()
    {
        isAttacking = false;
    }

    private void HandleInterrupt()
    {
        StopActionRoutine();
        selectedSkill = null;
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
        isMemorizingPlayer = false;
        isRunningForward = false;
        isAttacking = false;
        selectedSkill = null;
        nextAttackTime = 0f;

        core.CmdWarpToOriginal?.Invoke();
    }

    // 공격 완료 이벤트 처리: 마지막 공격 완료 시각 기록
    private void HandleOnAttackCompleted(float nextTime)
    {
        lastCompletedAttackTime = Time.time;
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
                core.SetMoveAnimations(0f, 0f, 0f);

                isAttacking = false;
                isDetectedPlayer = false;
                isMemorizingPlayer = false;
                isRunningForward = false;
            }
            return;
        }

        if (prevPlayerPos == Vector3.zero) prevPlayerPos = Player.Instance.transform.position;
        playerVelocity = (Player.Instance.transform.position - prevPlayerPos) / Time.deltaTime;

        if (playerVelocity.magnitude > 20f) playerVelocity = playerVelocity.normalized * 20f;

        prevPlayerPos = Player.Instance.transform.position;

        float sqrDistToPlayer = (Player.Instance.transform.position - transform.position).sqrMagnitude;
        float cullSqrDist = disableLogicDistance * disableLogicDistance;

        if (sqrDistToPlayer > cullSqrDist)
        {
            if (!isCulled)
            {
                isCulled = true;
                core.isCulled = true;

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
            core.isCulled = false;
            SetVisualsEnabled(true);
        }

        if (core.currentState == EnemyState.Attack) return;

        // 💡 수정: forceDetected가 true이면 무조건 시야에 있는 것으로 강제 처리
        bool isCurrentlySeeing = core.isForceDetected || CheckForPlayer();

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
            core.SetPlayerDetection(false);

            // 💡 수정: core.isForceDetected가 true이거나 기억 유지 조건 충족 시 절대 메모리가 풀리지 않도록 처리
            if (core.isForceDetected)
            {
                isMemorizingPlayer = true;
            }
            else if (isMemorizingPlayer && (Time.time - detectedPlayerTime > detectionMemoryDuration))
            {
                isMemorizingPlayer = false;
            }
        }

        // 공격 공백 검사 및 스킬 재검색
        if (Player.Instance != null && isMemorizingPlayer && !isAttacking)
        {
            float timeSinceLastAttack = Time.time - lastCompletedAttackTime;
            if (timeSinceLastAttack >= attackIdleReselectDelay && Time.time - lastReselectTime >= reselectCooldown)
            {
                float currentDist = Vector3.Distance(transform.position, Player.Instance.transform.position);
                if (core != null && core.skillSelector != null)
                {
                    SkillBase newSkill = core.skillSelector.GetNextSkill(currentDist);
                    if (newSkill != null && newSkill != selectedSkill)
                    {
                        selectedSkill = newSkill;
                    }
                }
                lastReselectTime = Time.time;
                lastCompletedAttackTime = Time.time;
            }
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
        if (hpbar)
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
            core.SetMoveAnimations(0f, 0f, 0f);
            isRunningForward = false;

            yield return new WaitForSeconds(UnityEngine.Random.Range(2f, 4f));

            Vector3 origin = core.QueryOriginalPosition?.Invoke() ?? transform.position;
            Vector3 target = UnityEngine.Random.insideUnitSphere * wanderSearchRadius + origin;

            if (NavMesh.SamplePosition(target, out NavMeshHit hit, wanderSearchRadius, NavMesh.AllAreas))
            {
                core.CmdMoveTo?.Invoke(hit.position, 1f);
                core.SetMoveAnimations(0f, 1f, 1f);

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

    // 💡 수정됨: 뒤로 걷기 시 어는 버그 해결
    public IEnumerator BackWalkRoutine(float distance)
    {
        core.ChangeState(EnemyState.BackWalk);
        core.CmdStopMovement?.Invoke();
        core.SetMoveAnimations(0f, -1f, 1f);
        isRunningForward = false;

        float movedDistance = 0f;
        float elapsedDuration = 0f;

        while (movedDistance < distance && elapsedDuration < backWalkMaxDuration)
        {
            elapsedDuration += Time.deltaTime;
            Vector3 backDir = (-transform.forward).normalized;
            float step = core.BaseMovementSpeed * backWalkSpeedMultiplier * Time.deltaTime;

            bool isBlocked = false;

            // 1. 내비메시 영역을 벗어나는지 체크
            if (NavMesh.Raycast(transform.position, transform.position + backDir * 0.5f, out NavMeshHit navHit, NavMesh.AllAreas))
            {
                isBlocked = true;
            }
            // 2. 물리적 벽인지 체크 (자기 자신을 때리지 않도록 시작점을 살짝 뒤로 뺌)
            else if (Physics.SphereCast(transform.position + Vector3.up * 0.5f + backDir * 0.3f, 0.2f, backDir, out RaycastHit hit, 0.2f, wallLayer))
            {
                // 법선(normal.y)이 평탄하면 바닥으로 간주하여 무시
                if (hit.normal.y < 0.5f) isBlocked = true;
            }

            if (!isBlocked)
            {
                core.CmdMoveDirection?.Invoke(backDir, backWalkSpeedMultiplier);
                movedDistance += step;
            }
            else
            {
                break; // 막혔으면 뒤로 걷기 중단
            }

            if (Player.Instance != null)
            {
                core.CmdLookAt?.Invoke(Player.Instance.transform.position);
            }

            yield return null;
        }

        core.SetMoveAnimations(0f, 0f, 0f);
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

    protected void UpdateChaseTarget()
    {
        core.ChangeState(EnemyState.Chase);
        core.SetMoveAnimations(0f, 1f, 2f);
        isRunningForward = true;

        if (Time.time >= nextTargetUpdateTime)
        {
            nextTargetUpdateTime = Time.time + targetUpdateInterval;
            Vector3 targetPos = lastKnownPlayerPos;

            if (playerVelocity.sqrMagnitude > 1f)
            {
                Vector3 flatVelocity = new Vector3(playerVelocity.x, 0f, playerVelocity.z);
                float dist = Vector3.Distance(transform.position, targetPos);
                float predictionTime = Mathf.Clamp(dist / (core.BaseMovementSpeed * chaseSpeedMultiplier), 0f, maxPredictionTime);
                targetPos += flatVelocity * predictionTime;
            }

            core.CmdMoveTo?.Invoke(targetPos, chaseSpeedMultiplier);
        }
    }

    protected void UpdateWalkApproachTarget()
    {
        core.ChangeState(EnemyState.Chase);
        core.SetMoveAnimations(0f, 1f, 1f);
        isRunningForward = false;

        if (Time.time >= nextTargetUpdateTime)
        {
            nextTargetUpdateTime = Time.time + targetUpdateInterval;
            core.CmdMoveTo?.Invoke(lastKnownPlayerPos, 1f);
        }
    }

    protected IEnumerator StrafeRoutine(float duration, float run = 1f)
    {
        return WalkStrafeRoutine(duration, run);
    }

    // 💡 수정됨: 게걸음(Strafe) 시 어는 버그 해결
    protected IEnumerator WalkStrafeRoutine(float duration, float run)
    {
        core.ChangeState(EnemyState.CooldownWalk);
        core.CmdStopMovement?.Invoke();
        isRunningForward = false;

        float elapsed = 0f;
        float dirSign = UnityEngine.Random.value > 0.5f ? 1f : -1f;
        float flipCooldown = 0f; // ★ 덜덜거림 방지용 쿨다운

        core.SetMoveAnimations(dirSign, 0f, run);

        while (elapsed < duration)
        {
            if (flipCooldown > 0f) flipCooldown -= Time.deltaTime;

            if (Player.Instance != null)
            {
                Vector3 dirToPlayer = (Player.Instance.transform.position - transform.position).normalized;
                dirToPlayer.y = 0f;

                Vector3 lookDir = Quaternion.Euler(0, 60f * dirSign, 0) * dirToPlayer;
                core.CmdLookAt?.Invoke(transform.position + lookDir);

                Vector3 orbitDir = Vector3.Cross(Vector3.up, dirToPlayer).normalized * dirSign;
                float SpeedMultiplier = run >= 1.5f ? chaseSpeedMultiplier * 0.8f : strafeSpeedMultiplier;

                bool isBlocked = false;

                // 쿨다운이 없을 때만 벽 검사 수행
                if (flipCooldown <= 0f)
                {
                    // 1. 내비메시 영역을 벗어나는지 체크
                    if (NavMesh.Raycast(transform.position, transform.position + orbitDir * 0.5f, out NavMeshHit navHit, NavMesh.AllAreas))
                    {
                        isBlocked = true;
                    }
                    // 2. 물리적 벽인지 체크 (자기 자신을 때리지 않도록 시작점을 살짝 앞/위로 뺌)
                    else if (Physics.SphereCast(transform.position + Vector3.up * 0.5f + orbitDir * 0.3f, 0.2f, orbitDir, out RaycastHit hit, 0.2f, wallLayer))
                    {
                        if (hit.normal.y < 0.5f) isBlocked = true;
                    }
                }

                // 막히지 않았거나 방금 막 방향을 꺾었을 경우(쿨다운 중) 해당 방향으로 이동
                if (!isBlocked || flipCooldown > 0f)
                {
                    core.CmdMoveDirection?.Invoke(orbitDir, SpeedMultiplier);
                }
                else
                {
                    // 막혔을 경우 방향을 전환하고 쿨다운 0.5초 부여 (무한 덜덜거림 방지)
                    dirSign *= -1f;
                    core.SetMoveAnimations(dirSign, 0f, run);
                    flipCooldown = 0.5f;
                }
            }

            bool canAttack = isDetectedPlayer && Time.time >= nextAttackTime;

            if (elapsed > 1f && canAttack && UnityEngine.Random.value < 0.5f * Time.deltaTime)
            {
                break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        core.SetMoveAnimations(0f, 0f, 0f);
        core.CmdTrySnapToNavMesh?.Invoke();
        core.ChangeState(EnemyState.Idle);
        actionCoroutine = null;
    }

    protected float GetSkillRange(SkillBase skill)
    {
        if (skill is Skill_Melee m && m.hitDataList != null && m.hitDataList.Count > 0)
        {
            float maxDist = 0f;
            foreach (var h in m.hitDataList)
            {
                float dist = Mathf.Abs(h.centerOffset.z) + (h.boxSize.z / 2f);
                if (dist > maxDist) maxDist = dist;
            }
            return maxDist;
        }
        else if (skill is Skill_Range r)
        {
            return r.range;
        }
        return 2.5f;
    }

    protected bool IsPlayerInSkillBox(SkillBase skill, float scale = 0.9f)
    {
        if (Player.Instance == null) return false;

        if (skill is Skill_Melee m && m.hitDataList != null && m.hitDataList.Count > 0)
        {
            var h = m.hitDataList[0];

            Vector3 worldCenter = transform.position + (transform.rotation * h.centerOffset);
            Quaternion worldRotation = transform.rotation * Quaternion.Euler(h.boxRotation);

            Vector3 playerPos = Player.Instance.transform.position + (Vector3.up * 1f);
            Vector3 localPos = Quaternion.Inverse(worldRotation) * (playerPos - worldCenter);
            Vector3 extents = (h.boxSize * scale) / 2f;

            return Mathf.Abs(localPos.x) <= extents.x &&
                   Mathf.Abs(localPos.y) <= (extents.y + 1f) &&
                   Mathf.Abs(localPos.z) <= extents.z;
        }
        else if (skill is Skill_Range r)
        {
            return Vector3.Distance(transform.position, Player.Instance.transform.position) <= r.range * scale;
        }

        return Vector3.Distance(transform.position, Player.Instance.transform.position) <= 2.5f * scale;
    }

    protected void CancelCurrentSkillAction()
    {
        core.animator.CrossFade("Idle", 0.25f);
        core.ChangeState(EnemyState.Idle);
    }

    protected void ChainNextAttackOrIdle()
    {
        if (core.skillSelector != null && core.skillSelector.HasPendingCombo)
        {
            float currentDist = Vector3.Distance(transform.position, Player.Instance.transform.position);
            selectedSkill = core.skillSelector.GetNextSkill(currentDist);

            if (selectedSkill != null)
            {
                actionCoroutine = StartCoroutine(PerformAttackSequence(selectedSkill));
                return;
            }
        }

        EndAttackAndIdle();
    }

    protected virtual void EndAttackAndIdle()
    {
        isAttacking = false;
        selectedSkill = null;
        core.ChangeState(EnemyState.Idle);
        core.SetMoveAnimations(0f, 0f, 0f);
        actionCoroutine = null;
    }

    protected virtual IEnumerator PerformAttackSequence(SkillBase skill)
    {
        yield break;
    }
}