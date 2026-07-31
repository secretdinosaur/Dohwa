using UnityEngine;
using System;
using System.Collections;

[RequireComponent(typeof(SkillSelector))]
public class Bandit5AI : EnemyAIBase
{
    [Header("Behavior Tuning")]
    public float walkApproachFactor = 1.5f;
    [Range(0f, 1f)] public float walkApproachChance = 0.2f; // 멀어졌을 때 걸어갈 확률
    private bool isChoosingWalkApproach = false;            // 결정된 이동 방식 유지용

    [Header("Strafe & Evasion Specific")]
    public float backWalkTriggerFactor = 0.7f;
    [Range(0f, 100f)] public float strafeOnCloseChance = 30f;
    private bool lastActionWasBackWalk = false;

    [Header("Run Attack Specific")]
    public SkillBase runAttackSkill;
    public float runDuration = 0.6f;
    public float runStopOffset = 1.5f;

    [Header("Jump Attack Specific")]
    public SkillBase jumpAttackSkill;
    public AnimationCurve jumpCurve = new AnimationCurve(new Keyframe(0, 0), new Keyframe(0.5f, 1), new Keyframe(1, 0));
    public float jumpHeight = 3f;
    public float jumpDuration = 0.8f;
    public float jumpLandingOffset = 1.5f;

    private SkillSelector skillSelector;

    protected override void Awake()
    {
        base.Awake();
        skillSelector = GetComponent<SkillSelector>();
    }

    protected override void HandleBehaviorLogic(float distToLastPos)
    {
        if (isAttacking || core.currentState == EnemyState.BackWalk || core.currentState == EnemyState.CooldownWalk || core.currentState == EnemyState.Dead) return;
        if (core.IsDead) return;

        if (isMemorizingPlayer)
        {
            StopActionRoutine();

            if (selectedSkill == null)
            {
                selectedSkill = skillSelector.GetNextSkill(distToLastPos);
            }

            if (selectedSkill == null)
            {
                if (distToLastPos > 5f)
                {
                    UpdateChaseTarget();
                }
                else
                {
                    actionCoroutine = StartCoroutine(StrafeRoutine(UnityEngine.Random.Range(strafeMinDuration, strafeMaxDuration)));
                }
                return;
            }

            float executeRange = GetSkillRange(selectedSkill) * skillRangeMultiplier;
            bool canAttackGlobal = isDetectedPlayer && (Time.time >= nextAttackTime || skillSelector.isNextSkillCombo);
            bool hasEnoughMP = core.QueryHasEnoughMP?.Invoke(selectedSkill.MP_cost) ?? false;

            if (canAttackGlobal && hasEnoughMP)
            {
                if (selectedSkill == jumpAttackSkill)
                {
                    if (distToLastPos <= executeRange)
                        actionCoroutine = StartCoroutine(JumpAttackSequence(selectedSkill));
                    else
                        HandleDistantApproach(executeRange);
                }
                else if (selectedSkill == runAttackSkill)
                {
                    // Running Attack은 대상 방향으로 달리고 있을 때만 발동합니다.
                    if (distToLastPos <= executeRange && core.currentState == EnemyState.Chase && isRunningForward)
                    {
                        actionCoroutine = StartCoroutine(RunAttackSequence(selectedSkill));
                    }
                    else
                    {
                        UpdateChaseTarget(); // 강제로 앞달리기 실행
                    }
                }
                else if (distToLastPos <= executeRange)
                {
                    actionCoroutine = StartCoroutine(AttackSequence(selectedSkill));
                }
                else if (distToLastPos <= executeRange * walkApproachFactor)
                {
                    UpdateWalkApproachTarget();
                }
                else
                {
                    HandleDistantApproach(executeRange);
                }
            }
            else // 쿨타임 또는 마나 부족
            {
                if (distToLastPos > executeRange * walkApproachFactor)
                {
                    HandleDistantApproach(executeRange);
                }
                else
                {
                    float backWalkRange = executeRange * backWalkTriggerFactor;
                    if (distToLastPos < backWalkRange && !lastActionWasBackWalk)
                    {
                        if (UnityEngine.Random.Range(0f, 100f) < strafeOnCloseChance)
                        {
                            actionCoroutine = StartCoroutine(StrafeRoutine(UnityEngine.Random.Range(strafeMinDuration, strafeMaxDuration)));
                        }
                        else
                        {
                            actionCoroutine = StartCoroutine(BackWalkRoutine(backWalkDist));
                        }
                        lastActionWasBackWalk = true;
                    }
                    else
                    {
                        actionCoroutine = StartCoroutine(StrafeRoutine(UnityEngine.Random.Range(strafeMinDuration, strafeMaxDuration)));
                        lastActionWasBackWalk = false;
                    }
                }
            }
        }
        else
        {
            core.ChangeState(EnemyState.Idle);
            StartWander();
        }
    }

    private void HandleDistantApproach(float executeRange)
    {
        float currentDist = Vector3.Distance(transform.position, lastKnownPlayerPos);

        if (Time.time >= nextTargetUpdateTime)
        {
            if (currentDist > executeRange * walkApproachFactor * 1.5f)
            {
                isChoosingWalkApproach = UnityEngine.Random.value < walkApproachChance;
            }
            else
            {
                isChoosingWalkApproach = true;
            }
        }

        if (isChoosingWalkApproach)
        {
            UpdateWalkApproachTarget();
        }
        else
        {
            UpdateChaseTarget();
        }
    }
    private void ChainNextAttackOrIdle()
    {
        if (skillSelector.HasPendingCombo)
        {
            float currentDist = Vector3.Distance(transform.position, Player.Instance.transform.position);
            selectedSkill = skillSelector.GetNextSkill(currentDist);

            if (selectedSkill == jumpAttackSkill)
            {
                actionCoroutine = StartCoroutine(JumpAttackSequence(selectedSkill));
            }
            else if (selectedSkill == runAttackSkill)
            {
                actionCoroutine = StartCoroutine(RunAttackSequence(selectedSkill));
            }
            else
            {
                actionCoroutine = StartCoroutine(AttackSequence(selectedSkill));
            }
        }
        else
        {
            isAttacking = false;
            selectedSkill = null;
            core.ChangeState(EnemyState.Idle);
            actionCoroutine = null;
        }
    }

    private IEnumerator JumpAttackSequence(SkillBase skill)
    {
        isAttacking = true;
        core.CmdStopMovement?.Invoke();
        core.ChangeState(EnemyState.Attack);
        isRunningForward = false;

        bool attackFinished = false;

        Action<float> onComplete = (nextTime) => {
            float delay = skillSelector.GetLastSkillDelay();
            nextAttackTime = Time.time + delay;
            attackFinished = true;
        };

        core.OnAttackCompleted += onComplete;
        core.CmdPerformAttack?.Invoke(skill);

        float prepTime = 0f;
        while (prepTime < jumpDuration * 0.3f)
        {
            if (skillSelector.isInstantComboTriggered) break;
            prepTime += Time.deltaTime;
            yield return null;
        }

        Vector3 startPos = transform.position;
        Vector3 playerPos = Player.Instance.transform.position;

        Vector3 dirToPlayer = (playerPos - startPos).normalized;
        dirToPlayer.y = 0f;

        Vector3 targetPos = playerPos - (dirToPlayer * jumpLandingOffset);
        targetPos.y = startPos.y;

        float elapsed = 0f;
        float moveDuration = jumpDuration * (0.7916f - 0.3f);

        while (elapsed < moveDuration)
        {
            if (skillSelector.isInstantComboTriggered) break;

            float t = elapsed / moveDuration;
            Vector3 currentPos = Vector3.Lerp(startPos, targetPos, t);
            currentPos.y += jumpCurve.Evaluate(t) * jumpHeight;
            transform.position = currentPos;
            elapsed += Time.deltaTime;

            yield return null;
        }

        if (!skillSelector.isInstantComboTriggered)
        {
            core.CmdLookAt?.Invoke(Player.Instance.transform.position);
            transform.position = targetPos;
            core.CmdTrySnapToNavMesh?.Invoke();
        }

        yield return new WaitUntil(() => attackFinished || skillSelector.isInstantComboTriggered);

        core.OnAttackCompleted -= onComplete;

        if (skillSelector.isInstantComboTriggered)
        {
            skillSelector.isInstantComboTriggered = false;
        }

        ChainNextAttackOrIdle();
    }

    private IEnumerator RunAttackSequence(SkillBase skill)
    {
        isAttacking = true;
        core.CmdStopMovement?.Invoke();
        core.ChangeState(EnemyState.Attack);
        isRunningForward = false;

        bool attackFinished = false;

        Action<float> onComplete = (nextTime) => {
            float delay = skillSelector.GetLastSkillDelay();
            nextAttackTime = Time.time + delay;
            attackFinished = true;
        };

        core.OnAttackCompleted += onComplete;
        core.CmdPerformAttack?.Invoke(skill);

        float elapsed = 0f;
        yield return new WaitForSeconds(1);

        Vector3 startPos = transform.position;

        while (elapsed < runDuration)
        {
            if (skillSelector.isInstantComboTriggered) break;

            Vector3 currentPlayerPos = Player.Instance.transform.position;
            Vector3 dirToPlayer = (currentPlayerPos - startPos).normalized;
            dirToPlayer.y = 0f;

            Vector3 targetPos = currentPlayerPos - (dirToPlayer * runStopOffset);
            targetPos.y = startPos.y;

            float t = elapsed / runDuration;
            transform.position = Vector3.Lerp(startPos, targetPos, t);
            core.CmdLookAt?.Invoke(currentPlayerPos);

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (!skillSelector.isInstantComboTriggered)
        {
            core.CmdLookAt?.Invoke(Player.Instance.transform.position);
            core.CmdTrySnapToNavMesh?.Invoke();
        }

        yield return new WaitUntil(() => attackFinished || skillSelector.isInstantComboTriggered);

        core.OnAttackCompleted -= onComplete;

        if (skillSelector.isInstantComboTriggered)
        {
            skillSelector.isInstantComboTriggered = false;
        }

        ChainNextAttackOrIdle();
    }

    private IEnumerator AttackSequence(SkillBase skill)
    {
        isAttacking = true;
        core.CmdStopMovement?.Invoke();
        core.ChangeState(EnemyState.Attack);
        isRunningForward = false;

        bool attackFinished = false;

        Action<float> onComplete = (nextTime) => {
            float delay = skillSelector.GetLastSkillDelay();
            nextAttackTime = Time.time + delay;
            attackFinished = true;
        };

        core.OnAttackCompleted += onComplete;
        core.CmdPerformAttack?.Invoke(skill);

        yield return new WaitUntil(() => attackFinished || skillSelector.isInstantComboTriggered);

        core.OnAttackCompleted -= onComplete;

        if (skillSelector.isInstantComboTriggered)
        {
            skillSelector.isInstantComboTriggered = false;
        }

        ChainNextAttackOrIdle();
    }


#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
    }
#endif
}