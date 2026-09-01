using System;
using System.Collections;
using UnityEngine;

public class Bandit3AI : EnemyAIBase
{
    private bool lastActionWasStep = false;

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
                if (dist < backWalkRange && !lastActionWasEvasion && !core.skillSelector.HasPendingCombo)
                {
                    if (UnityEngine.Random.Range(0f, 100f) < strafeOnCloseChance)
                        actionCoroutine = StartCoroutine(StrafeRoutine(UnityEngine.Random.Range(strafeMinDuration, strafeMaxDuration), 1));
                    else
                        actionCoroutine = StartCoroutine(BackWalkRoutine(backWalkDist));

                    lastActionWasEvasion = true;
                    lastActionWasStep = false;
                }
                else if (canAttackGlobal && hasEnoughMP)
                {
                    actionCoroutine = StartCoroutine(ExecuteSkillAndChain(selectedSkill));
                    lastActionWasEvasion = false;
                }
                else
                {
                    float ran = UnityEngine.Random.value;
                    if (ran < 0.8f)
                        actionCoroutine = StartCoroutine(StrafeRoutine(UnityEngine.Random.Range(strafeMinDuration, strafeMaxDuration), 1));
                    else
                    {
                        actionCoroutine = StartCoroutine(BackWalkRoutine(backWalkDist));
                        lastActionWasEvasion = true;
                        lastActionWasStep = false;
                        return;
                    }

                    lastActionWasEvasion = false;
                    lastActionWasStep = false;
                }
            }
            else
            {
                if (dist < backWalkRange)
                {
                    if (UnityEngine.Random.Range(0f, 100f) < strafeOnCloseChance || lastActionWasEvasion)
                    {
                        actionCoroutine = StartCoroutine(StrafeRoutine(UnityEngine.Random.Range(strafeMinDuration, strafeMaxDuration), 1));
                        lastActionWasEvasion = false;
                    }
                    else
                    {
                        actionCoroutine = StartCoroutine(BackWalkRoutine(backWalkDist));
                        lastActionWasEvasion = true;
                    }
                    lastActionWasStep = false;
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

    protected override IEnumerator PerformAttackSequence(SkillBase skill)
    {
        if (!core.skillSelector.isNextSkillCombo && !lastActionWasStep && UnityEngine.Random.value < 0.4f)
        {
            yield return ExecuteStepRoutine(2.5f, 0.4f, 0.25f);
            lastActionWasStep = true;

            if (Player.Instance == null || core.IsDead) yield break;

            bool isWithinAttackBoxAfterStep = IsPlayerInSkillBox(skill, skillRangeMultiplier);
            float distToPlayer = Vector3.Distance(transform.position, Player.Instance.transform.position);
            float executeRange = GetSkillRange(skill) * skillRangeMultiplier;
            float backWalkRange = executeRange * backWalkTriggerFactor;

            if (!isWithinAttackBoxAfterStep || distToPlayer > executeRange || distToPlayer < backWalkRange)
            {
                isAttacking = false;
                core.ChangeState(EnemyState.Idle);
                actionCoroutine = null;
                yield break;
            }
        }
        else
        {
            lastActionWasStep = false;
        }

        yield return base.PerformAttackSequence(skill);
    }

    private IEnumerator ExecuteStepRoutine(float distance = 2f, float duration = 0.3f, float hopHeight = 0.2f)
    {
        core.CmdStopMovement?.Invoke();
        core.SetMoveAnimations(0f, 0f, 0f);

        int dirIndex = UnityEngine.Random.Range(0, 3);
        string animName = dirIndex == 0 ? "Step_Back" : (dirIndex == 1 ? "Step_Left" : "Step_Right");

        animator.CrossFade(animName, 0.1f);

        Vector3 moveDir = -transform.forward;
        if (dirIndex == 1) moveDir = -transform.right;
        else if (dirIndex == 2) moveDir = transform.right;

        Vector3 startPos = transform.position;
        Vector3 targetPos = startPos + moveDir * distance;

        if (Physics.SphereCast(startPos + Vector3.up, 0.3f, moveDir, out RaycastHit hit, distance, wallLayer))
        {
            targetPos = startPos + moveDir * Mathf.Max(0, hit.distance - 0.3f);
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float easeT = t * t * (3f - 2f * t);

            Vector3 currentPos = Vector3.Lerp(startPos, targetPos, easeT);
            currentPos.y += Mathf.Sin(t * Mathf.PI) * hopHeight;

            transform.position = currentPos;

            if (Player.Instance != null)
            {
                core.CmdLookAt?.Invoke(Player.Instance.transform.position);
            }

            yield return null;
        }

        core.CmdTrySnapToNavMesh?.Invoke();
    }
}