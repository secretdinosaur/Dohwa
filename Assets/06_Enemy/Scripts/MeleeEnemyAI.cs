using System.Linq;
using UnityEngine;
using System;
using System.Collections;

public class MeleeEnemyAI : EnemyAIBase
{
    [Header("Melee Specific")]
    public float parriedBackWalkChance = 60f;
    public float normalBackWalkChance = 20f;
    protected override void HandleBehaviorLogic(float dist)
    {
        if (core.IsDead) return;
        if (isAttacking || core.currentState == EnemyState.BackWalk || core.currentState == EnemyState.Attack) return;

        if (isDetectedPlayer || (core.currentState == EnemyState.Chase && dist <= chaseRange))
        {
            StopActionRoutine();
            if (selectedSkill == null) selectedSkill = core.QueryGetSkill?.Invoke();

            float executeRange = GetSkillRange(selectedSkill) * skillRangeMultiplier;
            bool canAttack = Time.time >= nextAttackTime && !isAttacking;

            if (dist <= executeRange)
            {
                if (canAttack && (core.QueryHasEnoughMP?.Invoke(selectedSkill?.MP_cost ?? 0) ?? false))
                {
                    actionCoroutine = StartCoroutine(AttackSequence(selectedSkill));
                }
                else
                {
                    core.CmdStopMovement?.Invoke();
                    core.ChangeState(EnemyState.Idle);
                    core.SetMoveAnimations(false, false, false);
                }
            }
            else UpdateChaseTarget();
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
        core.CmdLookAt?.Invoke(Player.Instance.transform.position);

        if (Time.time >= nextTargetUpdateTime)
        {
            nextTargetUpdateTime = Time.time + targetUpdateInterval;
            core.CmdMoveTo?.Invoke(Player.Instance.transform.position, chaseSpeedMultiplier);
        }
    }

    private IEnumerator AttackSequence(SkillBase skill)
    {
        isAttacking = true;
        bool attackFinished = false;

        Action<float> onComplete = (nextTime) => {
            nextAttackTime = nextTime;
            attackFinished = true;
        };

        core.OnAttackCompleted += onComplete;
        core.CmdPerformAttack?.Invoke(skill);

        yield return new WaitUntil(() => attackFinished || core.wasParried || core.currentState == EnemyState.Stun || core.IsDead);

        core.OnAttackCompleted -= onComplete;
        core.StopEffects(skill.name);
        selectedSkill = null;
        isAttacking = false;

        float chance = core.wasParried ? parriedBackWalkChance : normalBackWalkChance;
        core.wasParried = false;

        SkillBase nextSkill = core.QueryGetSkill?.Invoke();
        bool hasEnoughMP = nextSkill != null && (core.QueryHasEnoughMP?.Invoke(nextSkill.MP_cost) ?? false);

        if (!hasEnoughMP || UnityEngine.Random.Range(0f, 100f) < chance)
        {
            yield return StartCoroutine(BackWalkRoutine(backWalkDist));
        }

        core.ChangeState(EnemyState.Idle);
        actionCoroutine = null;
    }

    protected float GetSkillRange(SkillBase skill)
    {
        if (skill is Skill_Melee m && m.hitDataList != null && m.hitDataList.Count > 0)
            return m.hitDataList.Max(h => Mathf.Abs(h.centerOffset.z) + (h.boxSize.z / 2f));
        return 2.5f;
    }
}