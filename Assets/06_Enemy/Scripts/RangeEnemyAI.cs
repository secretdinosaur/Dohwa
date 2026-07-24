using UnityEngine;
using System;
using System.Collections;

public class RangeEnemyAI : EnemyAIBase
{
    [Header("Range Specific")]
    public float backWalkTriggerFactor = 0.7f;
    public float LookAtPlayerBeforeAttackTime = 1f;

    [Header("Run Attack Specific")]
    public SkillBase runAttackSkill;
    public float runAttackChancePerSec = 15f;
    public float runAttackCooldown = 8f;

    private float lastRunAttackTime = -9999f;
    private bool lastActionWasBackWalk = false;

    protected override void HandleBehaviorLogic(float distToLastPos)
    {
        if (isAttacking || core.currentState == EnemyState.BackWalk || core.currentState == EnemyState.Dead) return;
        if (core.IsDead) return;
        if (isMemorizingPlayer)
        {
            StopActionRoutine();
            SkillBase selectedSkill = core.QueryGetSkill?.Invoke();
            float executeRange = GetSkillRange(selectedSkill) * skillRangeMultiplier;
            float backWalkRange = executeRange * backWalkTriggerFactor;

            bool canAttack = isDetectedPlayer && Time.time >= nextAttackTime && !isAttacking;

            if (distToLastPos <= executeRange)
            {
                if (distToLastPos < backWalkRange && !lastActionWasBackWalk)
                {
                    actionCoroutine = StartCoroutine(BackWalkRoutine(backWalkDist));
                    lastActionWasBackWalk = true;
                }
                else if (canAttack && (core.QueryHasEnoughMP?.Invoke(selectedSkill?.MP_cost ?? 0) ?? false))
                {
                    actionCoroutine = StartCoroutine(AttackSequence(selectedSkill));
                    lastActionWasBackWalk = false;
                }
                else
                {
                    core.ChangeState(EnemyState.Idle);
                    core.SetMoveAnimations(false, false, false);
                    core.CmdStopMovement?.Invoke();
                }
            }
            else
            {
                // 일반 사거리 밖일 때 발동 조건 체크
                bool canRunAttack = runAttackSkill != null && Time.time >= lastRunAttackTime + runAttackCooldown;
                bool hasMpForRunAttack = core.QueryHasEnoughMP?.Invoke(runAttackSkill?.MP_cost ?? 0) ?? false;

                if (canRunAttack && hasMpForRunAttack && isDetectedPlayer)
                {
                    if (UnityEngine.Random.Range(0f, 100f) < runAttackChancePerSec * Time.deltaTime)
                    {
                        lastRunAttackTime = Time.time;
                        lastActionWasBackWalk = false;
                        actionCoroutine = StartCoroutine(AttackSequence(runAttackSkill));
                        return;
                    }
                }

                UpdateChaseTarget();
            }
        }
        else
        {
            core.ChangeState(EnemyState.Idle);
            StartWander();
        }
    }

    private void UpdateChaseTarget()
    {
        core.ChangeState(EnemyState.Chase);
        core.SetMoveAnimations(true, false, false);

        if (Time.time >= nextTargetUpdateTime)
        {
            nextTargetUpdateTime = Time.time + targetUpdateInterval;
            core.CmdMoveTo?.Invoke(lastKnownPlayerPos, chaseSpeedMultiplier);
        }
    }

    private IEnumerator AttackSequence(SkillBase skill)
    {
        isAttacking = true;
        bool attackFinished = false;

        Action<float> onComplete = (nextTime) => {
            nextAttackTime = nextTime;
            attackFinished = true;
            isAttacking = false;
            core.ChangeState(EnemyState.Idle);
        };

        core.OnAttackCompleted += onComplete;
        core.CmdPerformAttack?.Invoke(skill);

        StartCoroutine(LookatPlayer(LookAtPlayerBeforeAttackTime));
        yield return new WaitUntil(() => attackFinished);

        core.OnAttackCompleted -= onComplete;
        core.StopEffects(skill.name);
        isAttacking = false;
        core.ChangeState(EnemyState.Idle);
        actionCoroutine = null;
    }

    private IEnumerator LookatPlayer(float time)
    {
        float elapsed = 0f;
        while (elapsed < time)
        {
            elapsed += Time.deltaTime;
            if (!CheckForPlayer())
            {
                core.CmdCancelAttack?.Invoke();
                CancelCurrentSkillAction();
                yield break;
            }
            core.CmdLookAt?.Invoke(Player.Instance.transform.position);
            yield return null;
        }
    }

    private void CancelCurrentSkillAction()
    {
        core.animator.CrossFade("Idle", 0.25f);
        core.ChangeState(EnemyState.Idle);
    }

    protected float GetSkillRange(SkillBase skill) => skill is Skill_Range r ? r.range : 10f;
}