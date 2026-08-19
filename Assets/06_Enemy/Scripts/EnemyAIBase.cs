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

    // ★ 추가됨: 강강술래 방지용 예측 추적 변수
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
        isRunningForward = false;
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
                core.SetMoveAnimations(0f, 0f, 0f);

                isAttacking = false;
                isDetectedPlayer = false;
                isMemorizingPlayer = false;
                isRunningForward = false;
            }
            return;
        }

        // ★ 추가됨: 플레이어의 실시간 이동 속도(방향) 계산
        if (prevPlayerPos == Vector3.zero) prevPlayerPos = Player.Instance.transform.position;
        playerVelocity = (Player.Instance.transform.position - prevPlayerPos) / Time.deltaTime;

        // 너무 빠른 비정상 속도(순간이동 등)는 무시하도록 제한
        if (playerVelocity.magnitude > 20f) playerVelocity = playerVelocity.normalized * 20f;

        prevPlayerPos = Player.Instance.transform.position;

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
            core.SetPlayerDetection(false);

            if (isMemorizingPlayer && (Time.time - detectedPlayerTime > detectionMemoryDuration))
            {
                isMemorizingPlayer = false;
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

    public IEnumerator BackWalkRoutine(float distance)
    {
        core.ChangeState(EnemyState.BackWalk);
        core.CmdStopMovement?.Invoke();
        core.SetMoveAnimations(0f, -1f, 1f);
        isRunningForward = false;

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

            // ★ 수정됨: 플레이어의 이동 경로를 예측하여 앞길을 가로막음
            Vector3 targetPos = lastKnownPlayerPos;

            // 플레이어가 실제로 일정 속도 이상으로 움직이고 있을 때만 예측 적용
            if (playerVelocity.sqrMagnitude > 1f)
            {
                Vector3 flatVelocity = new Vector3(playerVelocity.x, 0f, playerVelocity.z);

                // 적과 플레이어 사이의 거리에 비례하여 얼마나 앞을 내다볼지 결정
                float dist = Vector3.Distance(transform.position, targetPos);

                // 멀리 있을수록 더 멀리 예측 (최대 maxPredictionTime까지만)
                float predictionTime = Mathf.Clamp(dist / (core.BaseMovementSpeed * chaseSpeedMultiplier), 0f, maxPredictionTime);

                // 플레이어가 가고 있는 방향의 앞쪽을 새로운 목적지로 설정
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

        core.SetMoveAnimations(dirSign, 0f, run);

        while (elapsed < duration)
        {
            if (Player.Instance != null)
            {
                Vector3 dirToPlayer = (Player.Instance.transform.position - transform.position).normalized;
                dirToPlayer.y = 0f;

                Vector3 lookDir = Quaternion.Euler(0, 60f * dirSign, 0) * dirToPlayer;
                core.CmdLookAt?.Invoke(transform.position + lookDir);

                Vector3 orbitDir = Vector3.Cross(Vector3.up, dirToPlayer).normalized * dirSign;

                float SpeedMultiplier = run >= 1.5f ? chaseSpeedMultiplier * 0.8f : strafeSpeedMultiplier;
                float step = core.BaseMovementSpeed * SpeedMultiplier * Time.deltaTime;

                if (!Physics.SphereCast(transform.position + Vector3.up, 0.3f, orbitDir, out _, step, wallLayer))
                {
                    core.CmdMoveDirection?.Invoke(orbitDir, SpeedMultiplier);
                }
                else
                {
                    dirSign *= -1f;
                    core.SetMoveAnimations(dirSign, 0f, run);
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

    protected float GetSkillOptimalDistance(SkillBase skill)
    {
        if (skill is Skill_Melee m && m.hitDataList != null && m.hitDataList.Count > 0)
        {
            return Mathf.Abs(m.hitDataList[0].centerOffset.z);
        }
        else if (skill is Skill_Range r)
        {
            return r.range * 0.8f;
        }
        return 2.5f;
    }

    protected float GetSkillBoxZExtents(SkillBase skill)
    {
        if (skill is Skill_Melee m && m.hitDataList != null && m.hitDataList.Count > 0)
        {
            return m.hitDataList[0].boxSize.z / 2f;
        }
        return 1.0f;
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