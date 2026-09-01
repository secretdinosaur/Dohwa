using System;
using System.Collections;
using UnityEngine;

public class MeleeEnemyAI : EnemyAIBase
{
    protected override void HandleBehaviorLogic(float dist)
    {
        if (core.IsDead) return;
        if (isAttacking || core.currentState == EnemyState.BackWalk || core.currentState == EnemyState.CooldownWalk || core.currentState == EnemyState.Attack) return;

        if (isDetectedPlayer || (core.currentState == EnemyState.Chase && dist <= chaseRange))
        {
            StopActionRoutine();

            if (selectedSkill == null)
                selectedSkill = core.skillSelector.GetNextSkill(dist);

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
                if (dist < backWalkRange && !lastActionWasEvasion && !core.skillSelector.HasPendingCombo && UnityEngine.Random.Range(0f, 100f) < moveBeforeAttackChance)
                {
                    if (UnityEngine.Random.Range(0f, 100f) < strafeOnCloseChance)
                        actionCoroutine = StartCoroutine(StrafeRoutine(UnityEngine.Random.Range(strafeMinDuration, strafeMaxDuration)));
                    else
                        actionCoroutine = StartCoroutine(BackWalkRoutine(backWalkDist));
                    lastActionWasEvasion = true;
                }
                else if (canAttackGlobal && hasEnoughMP)
                {
                    actionCoroutine = StartCoroutine(ExecuteSkillAndChain(selectedSkill));
                    lastActionWasEvasion = false;
                }
                else
                {
                    if (UnityEngine.Random.Range(0f, 100f) < 60f)
                        actionCoroutine = StartCoroutine(StrafeRoutine(UnityEngine.Random.Range(strafeMinDuration, strafeMaxDuration)));
                    lastActionWasEvasion = false;
                }
            }
            else
            {
                if (dist < backWalkRange)
                {
                    if (UnityEngine.Random.Range(0f, 100f) < strafeOnCloseChance || lastActionWasEvasion)
                    {
                        actionCoroutine = StartCoroutine(StrafeRoutine(UnityEngine.Random.Range(strafeMinDuration, strafeMaxDuration)));
                        lastActionWasEvasion = false;
                    }
                    else
                    {
                        actionCoroutine = StartCoroutine(BackWalkRoutine(backWalkDist));
                        lastActionWasEvasion = true;
                    }
                }
                else
                {
                    UpdateChaseTarget();
                    lastActionWasEvasion = false;
                }
            }
        }
        else
        {
            core.ChangeState(EnemyState.Idle);
            StartWander();
        }
    }
}