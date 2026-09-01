using System;
using System.Collections;
using UnityEngine;

public class Bandit4AI : EnemyAIBase
{
    [Header("Jump Attack Specific")]
    public SkillBase jumpAttackSkill;
    public AnimationCurve jumpCurve = new AnimationCurve(new Keyframe(0, 0), new Keyframe(0.5f, 1), new Keyframe(1, 0));
    public float jumpHeight = 3f;
    public float jumpDuration = 0.8f;
    public float jumpLandingOffset = 1.5f;

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
                if (selectedSkill == jumpAttackSkill && canAttackGlobal && hasEnoughMP)
                {
                    actionCoroutine = StartCoroutine(ExecuteSkillAndChain(selectedSkill));
                    lastActionWasEvasion = false;
                }
                else
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

    protected override IEnumerator RouteAndExecuteSkill(SkillBase skill)
    {
        if (skill == jumpAttackSkill) yield return JumpAttackSequence(skill);
        else yield return base.RouteAndExecuteSkill(skill);
    }

    private IEnumerator JumpAttackSequence(SkillBase skill)
    {
        isAttacking = true;
        core.CmdStopMovement?.Invoke();
        core.ChangeState(EnemyState.Attack);
        isRunningForward = false;
        bool attackFinished = false;

        currentAttackCompleteHandler = (nextTime) => {
            nextAttackTime = Time.time + core.skillSelector.GetLastSkillDelay();
            attackFinished = true;
        };

        core.OnAttackCompleted += currentAttackCompleteHandler;

        try
        {
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

            yield return new WaitUntil(() => attackFinished || core.wasParried || core.currentState != EnemyState.Attack || core.IsDead || core.skillSelector.isInstantComboTriggered);
        }
        finally
        {
            ClearAttackHandler();
        }

        StopAttackEffects(skill);

        if (core.skillSelector.isInstantComboTriggered)
        {
            core.skillSelector.isInstantComboTriggered = false;
            core.CmdCancelAttack?.Invoke();
        }
    }
}