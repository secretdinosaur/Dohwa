using System;
using System.Collections;
using UnityEngine;

public class MeleeEnemyAI : EnemyAIBase
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

            if (dist <= executeRange)
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
            else
            {
                UpdateChaseTarget();
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

    protected void OnDisable()
    {
        if (currentAttackCompleteHandler != null && core != null)
        {
            core.OnAttackCompleted -= currentAttackCompleteHandler;
            currentAttackCompleteHandler = null;
        }
    }
}