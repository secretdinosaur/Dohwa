using System;
using System.Collections;
using UnityEngine;

public class Bandit4AI : EnemyAIBase
{
    [Header("Melee Specific")]
    [Tooltip("너무 가까울 때 공격 전 위치를 다시 잡을 확률 (0~100)")]
    [Range(0f, 100f)] public float moveBeforeAttackChance = 50f;

    [Tooltip("패링 당했을 때 뒤로 물러날 확률 (0~100)")]
    [Range(0f, 100f)] public float parriedBackWalkChance = 60f;

    [Tooltip("일반 공격 종료 후 뒤로 물러날 확률 (0~100)")]
    [Range(0f, 100f)] public float normalBackWalkChance = 20f;

    public float backWalkTriggerFactor = 0.7f;

    [Tooltip("위치 다시 잡기 발동 시, 뒤로 안 가고 옆(Strafe)으로 걸을 확률 (0~100)")]
    [Range(0f, 100f)] public float strafeOnCloseChance = 30f;

    [Header("Jump Attack Specific")]
    public SkillBase jumpAttackSkill;
    public AnimationCurve jumpCurve = new AnimationCurve(new Keyframe(0, 0), new Keyframe(0.5f, 1), new Keyframe(1, 0));
    public float jumpHeight = 3f;
    public float jumpDuration = 0.8f;
    public float jumpLandingOffset = 1.5f;

    private bool lastActionWasBackWalk = false;
    private Action<float> currentAttackCompleteHandler;

    protected override void HandleBehaviorLogic(float dist)
    {
        if (core.IsDead) return;
        if (isAttacking || core.currentState == EnemyState.BackWalk || core.currentState == EnemyState.CooldownWalk || core.currentState == EnemyState.Attack) return;

        if (isDetectedPlayer || (core.currentState == EnemyState.Chase && dist <= chaseRange))
        {
            StopActionRoutine();

            if (selectedSkill == null)
            {
                selectedSkill = core.skillSelector.GetNextSkill(dist);
            }

            if (selectedSkill == null)
            {
                UpdateChaseTarget();
                return;
            }

            float executeRange = GetSkillRange(selectedSkill) * skillRangeMultiplier;
            float backWalkRange = executeRange * backWalkTriggerFactor;

            bool canAttackGlobal = Time.time >= nextAttackTime || core.skillSelector.isNextSkillCombo;
            bool hasEnoughMP = core.QueryHasEnoughMP?.Invoke(selectedSkill.MP_cost) ?? false;

            bool isWithinAttackBox = IsPlayerInSkillBox(selectedSkill, skillRangeMultiplier);

            if (isWithinAttackBox)
            {
                if (selectedSkill == jumpAttackSkill && canAttackGlobal && hasEnoughMP)
                {
                    actionCoroutine = StartCoroutine(JumpAttackSequence(selectedSkill));
                    lastActionWasBackWalk = false;
                }
                else
                {
                    if (dist < backWalkRange && !lastActionWasBackWalk && !core.skillSelector.HasPendingCombo && UnityEngine.Random.Range(0f, 100f) < moveBeforeAttackChance)
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
                    else if (canAttackGlobal && hasEnoughMP)
                    {
                        actionCoroutine = StartCoroutine(PerformAttackSequence(selectedSkill));
                        lastActionWasBackWalk = false;
                    }
                    else
                    {
                        float rand = UnityEngine.Random.Range(0f, 100f);

                        if (rand < 60f)
                        {
                            actionCoroutine = StartCoroutine(StrafeRoutine(UnityEngine.Random.Range(strafeMinDuration, strafeMaxDuration)));
                        }

                        lastActionWasBackWalk = false;
                    }
                }
            }
            else
            {
                // 타격 박스 안에 없을 때, 거리가 너무 가깝다면 뒤로 빼거나 회피기동
                if (dist < backWalkRange)
                {
                    if (UnityEngine.Random.Range(0f, 100f) < strafeOnCloseChance || lastActionWasBackWalk)
                    {
                        actionCoroutine = StartCoroutine(StrafeRoutine(UnityEngine.Random.Range(strafeMinDuration, strafeMaxDuration)));
                        lastActionWasBackWalk = false;
                    }
                    else
                    {
                        actionCoroutine = StartCoroutine(BackWalkRoutine(backWalkDist));
                        lastActionWasBackWalk = true;
                    }
                }
                else
                {
                    // 거리가 멀거나 각도가 빗나간 경우 추적하여 간격 좁히기
                    UpdateChaseTarget();
                    lastActionWasBackWalk = false;
                }
            }
        }
        else
        {
            core.ChangeState(EnemyState.Idle);
            StartWander();
        }
    }

    protected override IEnumerator PerformAttackSequence(SkillBase skill)
    {
        isAttacking = true;
        bool attackFinished = false;

        currentAttackCompleteHandler = (nextTime) => {
            nextAttackTime = Time.time + core.skillSelector.GetLastSkillDelay();
            attackFinished = true;
        };

        core.OnAttackCompleted += currentAttackCompleteHandler;
        core.CmdPerformAttack?.Invoke(skill);

        yield return new WaitUntil(() => attackFinished || core.wasParried || core.currentState == EnemyState.Stun || core.IsDead || core.skillSelector.isInstantComboTriggered);

        if (currentAttackCompleteHandler != null)
        {
            core.OnAttackCompleted -= currentAttackCompleteHandler;
            currentAttackCompleteHandler = null;
        }

        core.StopEffects(skill.name);

        if (core.skillSelector.isInstantComboTriggered)
        {
            core.skillSelector.isInstantComboTriggered = false;
            core.CmdCancelAttack?.Invoke();
        }

        float chance = core.wasParried ? parriedBackWalkChance : normalBackWalkChance;
        core.wasParried = false;

        if (UnityEngine.Random.Range(0f, 100f) < chance && !core.skillSelector.HasPendingCombo)
        {
            yield return StartCoroutine(BackWalkRoutine(backWalkDist));
            lastActionWasBackWalk = true;
        }

        ChainNextAttackOrIdle();
    }

    private IEnumerator JumpAttackSequence(SkillBase skill)
    {
        isAttacking = true;
        core.CmdStopMovement?.Invoke();
        core.ChangeState(EnemyState.Attack);
        isRunningForward = false;

        bool attackFinished = false;

        currentAttackCompleteHandler = (nextTime) => {
            float delay = core.skillSelector.GetLastSkillDelay();
            nextAttackTime = Time.time + delay;
            attackFinished = true;
        };

        core.OnAttackCompleted += currentAttackCompleteHandler;
        core.CmdPerformAttack?.Invoke(skill);

        float prepTime = 0f;
        while (prepTime < 0.75f)
        {
            if (core.skillSelector.isInstantComboTriggered) break;
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
            if (core.skillSelector.isInstantComboTriggered || core.IsDead) break;

            float t = elapsed / moveDuration;
            Vector3 currentPos = Vector3.Lerp(startPos, targetPos, t);
            currentPos.y += jumpCurve.Evaluate(t) * jumpHeight;
            transform.position = currentPos;
            elapsed += Time.deltaTime;

            yield return null;
        }

        if (!core.skillSelector.isInstantComboTriggered && !core.IsDead)
        {
            core.CmdLookAt?.Invoke(Player.Instance.transform.position);
            transform.position = targetPos;
            core.CmdTrySnapToNavMesh?.Invoke();
        }

        yield return new WaitUntil(() => attackFinished || core.wasParried || core.currentState == EnemyState.Stun || core.IsDead || core.skillSelector.isInstantComboTriggered);

        if (currentAttackCompleteHandler != null)
        {
            core.OnAttackCompleted -= currentAttackCompleteHandler;
            currentAttackCompleteHandler = null;
        }

        core.StopEffects(skill.name);

        if (core.skillSelector.isInstantComboTriggered)
        {
            core.skillSelector.isInstantComboTriggered = false;
            core.CmdCancelAttack?.Invoke();
        }

        ChainNextAttackOrIdle();
    }

    private void ChainNextAttackOrIdle()
    {
        if (core.skillSelector.HasPendingCombo)
        {
            float currentDist = Vector3.Distance(transform.position, Player.Instance.transform.position);
            selectedSkill = core.skillSelector.GetNextSkill(currentDist);

            if (selectedSkill == jumpAttackSkill)
            {
                actionCoroutine = StartCoroutine(JumpAttackSequence(selectedSkill));
            }
            else
            {
                actionCoroutine = StartCoroutine(PerformAttackSequence(selectedSkill));
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

    protected void OnDisable()
    {
        if (currentAttackCompleteHandler != null && core != null)
        {
            core.OnAttackCompleted -= currentAttackCompleteHandler;
            currentAttackCompleteHandler = null;
        }
    }
}