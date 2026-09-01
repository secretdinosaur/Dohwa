using UnityEngine;
using UnityEngine.AI;
using System;
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

    [Header("Melee & Evasion Tactics (Common)")]
    [Tooltip("너무 가까울 때 공격 전 위치를 다시 잡을 확률 (0~100)")]
    [Range(0f, 100f)] public float moveBeforeAttackChance = 50f;
    [Tooltip("패링 당했을 때 뒤로 물러날 확률 (0~100)")]
    [Range(0f, 100f)] public float parriedBackWalkChance = 60f;
    [Tooltip("일반 공격 종료 후 뒤로 물러날 확률 (0~100)")]
    [Range(0f, 100f)] public float normalBackWalkChance = 20f;
    [Tooltip("위치 다시 잡기 발동 시, 뒤로 안 가고 옆(Strafe)으로 걸을 확률 (0~100)")]
    [Range(0f, 100f)] public float strafeOnCloseChance = 30f;
    public float backWalkTriggerFactor = 0.7f;
    [Tooltip("게걸음(Strafe)이나 뒷걸음(BackWalk) 도중 피격 시 즉각 반격할 확률")]
    [Range(0f, 100f)] public float evasionCounterAttackChance = 60f;
    [Header("Predictive Chase (강강술래 방지)")]
    public float maxPredictionTime = 0.7f;
    protected Vector3 prevPlayerPos;
    protected Vector3 playerVelocity;

    protected EnemyCore core;
    public bool isDetectedPlayer = false;
    public bool isMemorizingPlayer = false;
    protected bool isRunningForward = false;

    // 💡 모든 자식이 공통으로 쓰는 회피/스텝 플래그
    protected bool lastActionWasEvasion = false;
    protected Action<float> currentAttackCompleteHandler;

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

    [Header("AI Behavior: Re-evaluate Skill")]
    public float attackIdleReselectDelay = 3f;
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
        core.OnDamageTaken += HandleTakeDamage;
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
            core.OnDamageTaken -= HandleTakeDamage;
        }
        ClearAttackHandler();
    }

    protected void ClearAttackHandler()
    {
        if (currentAttackCompleteHandler != null && core != null)
        {
            core.OnAttackCompleted -= currentAttackCompleteHandler;
            currentAttackCompleteHandler = null;
        }
    }

    void HandleStunEnd()
    {
        isAttacking = false;
    }

    private void HandleInterrupt()
    {
        StopActionRoutine();
        ClearAttackHandler();
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

            if (core.isForceDetected)
            {
                isMemorizingPlayer = true;
            }
            else if (isMemorizingPlayer && (Time.time - detectedPlayerTime > detectionMemoryDuration))
            {
                isMemorizingPlayer = false;
            }
        }

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
    public virtual void HandleTakeDamage(float damage)
    {
        // 죽었거나, 이미 스턴에 걸렸거나, 이미 공격 중이면 무시
        if (core.IsDead || core.currentState == EnemyState.Stun || isAttacking) return;
        if (Player.Instance == null || !Player.Instance.IsAlive) return;

        // 현재 상태가 '게걸음(CooldownWalk)' 이거나 '뒷걸음(BackWalk)' 일 때만 반응
        if (core.currentState == EnemyState.CooldownWalk || core.currentState == EnemyState.BackWalk)
        {
            if (UnityEngine.Random.Range(0f, 100f) < evasionCounterAttackChance)
            {
                float distToPlayer = Vector3.Distance(transform.position, Player.Instance.transform.position);

                if (core.skillSelector != null)
                {
                    SkillBase counterSkill = core.skillSelector.GetNextSkill(distToPlayer);

                    if (counterSkill != null && (core.QueryHasEnoughMP?.Invoke(counterSkill.MP_cost) ?? false))
                    {
                        // 1. 진행 중이던 회피/걷기 코루틴 즉시 캔슬
                        StopActionRoutine();
                        core.CmdStopMovement?.Invoke();

                        // 2. 공격 쿨타임을 무시하고 즉시 공격 시퀀스 돌입
                        nextAttackTime = Time.time;
                        actionCoroutine = StartCoroutine(ExecuteSkillAndChain(counterSkill));
                    }
                }
            }
        }
    }
    private void SetVisualsEnabled(bool state)
    {
        if (animator != null) animator.enabled = state;

        for (int i = 0; i < childRenderers.Length; i++)
        {
            if (childRenderers[i] != null) childRenderers[i].enabled = state;
        }
        if (hpbar) hpbar.gameObject.SetActive(state);
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

            if (NavMesh.Raycast(transform.position, transform.position + backDir * 0.5f, out NavMeshHit navHit, NavMesh.AllAreas))
            {
                isBlocked = true;
            }
            else if (Physics.SphereCast(transform.position + Vector3.up * 0.5f + backDir * 0.3f, 0.2f, backDir, out RaycastHit hit, 0.2f, wallLayer))
            {
                if (hit.normal.y < 0.5f) isBlocked = true;
            }

            if (!isBlocked)
            {
                core.CmdMoveDirection?.Invoke(backDir, backWalkSpeedMultiplier);
                movedDistance += step;
            }
            else
            {
                break;
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

    protected IEnumerator WalkStrafeRoutine(float duration, float run)
    {
        core.ChangeState(EnemyState.CooldownWalk);
        core.CmdStopMovement?.Invoke();
        isRunningForward = false;

        float elapsed = 0f;
        float dirSign = UnityEngine.Random.value > 0.5f ? 1f : -1f;
        float flipCooldown = 0f;

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

                if (flipCooldown <= 0f)
                {
                    if (NavMesh.Raycast(transform.position, transform.position + orbitDir * 0.5f, out NavMeshHit navHit, NavMesh.AllAreas))
                    {
                        isBlocked = true;
                    }
                    else if (Physics.SphereCast(transform.position + Vector3.up * 0.5f + orbitDir * 0.3f, 0.2f, orbitDir, out RaycastHit hit, 0.2f, wallLayer))
                    {
                        if (hit.normal.y < 0.5f) isBlocked = true;
                    }
                }

                if (!isBlocked || flipCooldown > 0f)
                {
                    core.CmdMoveDirection?.Invoke(orbitDir, SpeedMultiplier);
                }
                else
                {
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

    // =========================================================================
    // 💡 2번, 3번 개선점: 완벽한 통합 Attack Sequence & Skill Routing
    // =========================================================================
    protected IEnumerator ExecuteSkillAndChain(SkillBase skill)
    {
        yield return RouteAndExecuteSkill(skill);
        ChainNextAttackOrIdle();
    }

    protected virtual IEnumerator RouteAndExecuteSkill(SkillBase skill)
    {
        yield return PerformAttackSequence(skill);
    }

    protected virtual IEnumerator PerformAttackSequence(SkillBase skill)
    {
        isAttacking = true;
        core.CmdStopMovement?.Invoke();
        core.ChangeState(EnemyState.Attack);
        isRunningForward = false;

        bool attackFinished = false;

        currentAttackCompleteHandler = (nextTime) => {
            nextAttackTime = Time.time + (core.skillSelector != null ? core.skillSelector.GetLastSkillDelay() : 0f);
            attackFinished = true;
        };

        core.OnAttackCompleted += currentAttackCompleteHandler;

        try
        {
            core.CmdPerformAttack?.Invoke(skill);

            yield return new WaitUntil(() =>
                attackFinished ||
                core.wasParried ||
                core.currentState != EnemyState.Attack || // Stun, Cancel, Dead 커버
                (core.skillSelector != null && core.skillSelector.isInstantComboTriggered)
            );
        }
        finally
        {
            ClearAttackHandler();
        }

        if (core.IsDead) yield break;

        StopAttackEffects(skill);

        if (core.skillSelector != null && core.skillSelector.isInstantComboTriggered)
        {
            core.skillSelector.isInstantComboTriggered = false;
            core.CmdCancelAttack?.Invoke();
        }

        float chance = core.wasParried ? parriedBackWalkChance : normalBackWalkChance;
        core.wasParried = false;

        if (UnityEngine.Random.Range(0f, 100f) < chance && (core.skillSelector == null || !core.skillSelector.HasPendingCombo))
        {
            yield return ExecuteEvasionAfterAttack();
        }
    }

    protected virtual void StopAttackEffects(SkillBase skill)
    {
        core.StopEffects(skill.name);
    }

    protected virtual IEnumerator ExecuteEvasionAfterAttack()
    {
        yield return BackWalkRoutine(backWalkDist);
        lastActionWasEvasion = true;
    }

    protected virtual void ChainNextAttackOrIdle()
    {
        if (core.IsDead) return;

        if (core.skillSelector != null && core.skillSelector.HasPendingCombo)
        {
            float currentDist = Vector3.Distance(transform.position, Player.Instance.transform.position);
            selectedSkill = core.skillSelector.GetNextSkill(currentDist);

            if (selectedSkill != null)
            {
                actionCoroutine = StartCoroutine(ExecuteSkillAndChain(selectedSkill));
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
}