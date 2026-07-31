using UnityEngine;
using System;
using System.Collections;

public class RangeEnemyAI : EnemyAIBase
{
    [Header("Range Specific")]
    public float backWalkTriggerFactor = 0.7f;
    public float LookAtPlayerBeforeAttackTime = 1f;
    [Range(0f, 100f)] public float strafeOnCloseChance = 30f;

    private bool lastActionWasBackWalk = false;
    private Action<float> currentAttackCompleteHandler;

    protected override void HandleBehaviorLogic(float distToLastPos)
    {
        if (core.IsDead) return;
        if (isAttacking || core.currentState == EnemyState.BackWalk || core.currentState == EnemyState.CooldownWalk || core.currentState == EnemyState.Attack) return;

        if (isMemorizingPlayer)
        {
            StopActionRoutine();

            if (selectedSkill == null)
            {
                selectedSkill = core.skillSelector.GetNextSkill(distToLastPos);
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

            if (distToLastPos <= executeRange)
            {
                if (distToLastPos < backWalkRange && !lastActionWasBackWalk && !core.skillSelector.HasPendingCombo)
                {
                    if (UnityEngine.Random.Range(0f, 100f) < strafeOnCloseChance)
                    {
                        actionCoroutine = StartCoroutine(StrafeRoutine(UnityEngine.Random.Range(strafeMinDuration, strafeMaxDuration), 2));
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
                    float ran = UnityEngine.Random.value;
                    if (ran < 0.3f)
                        actionCoroutine = StartCoroutine(StrafeRoutine(UnityEngine.Random.Range(strafeMinDuration, strafeMaxDuration), 1));
                    else if (ran < 0.8f)
                        actionCoroutine = StartCoroutine(StrafeRoutine(UnityEngine.Random.Range(strafeMinDuration, strafeMaxDuration), 2));
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

        StartCoroutine(LookatPlayer(LookAtPlayerBeforeAttackTime));

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

        core.wasParried = false;

        ChainNextAttackOrIdle();
    }

    private IEnumerator LookatPlayer(float time)
    {
        float elapsed = 0f;
        while (elapsed < time)
        {
            if (isAttacking == false || core.IsDead || core.currentState == EnemyState.Stun) yield break;

            elapsed += Time.deltaTime;
            if (!CheckForPlayer())
            {
                core.CmdCancelAttack?.Invoke();
                CancelCurrentSkillAction();
                ChainNextAttackOrIdle();
                yield break;
            }
            core.CmdLookAt?.Invoke(Player.Instance.transform.position);
            yield return null;
        }
    }
}