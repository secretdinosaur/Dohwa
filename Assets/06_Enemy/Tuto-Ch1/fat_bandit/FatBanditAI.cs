using UnityEngine;
using System;
using System.Collections;

[RequireComponent(typeof(BossSkillSelector))]
public class FatBanditAI : EnemyAIBase
{
    [Header("Behavior Tuning")]
    public float walkApproachFactor = 1.5f;
    public float closeWanderMin = 1.5f;
    public float closeWanderMax = 3.5f;

    [Header("Run Attack Specific")]
    public SkillBase runAttackSkill;
    public Collider runAttackCollider;
    public float runAttackChancePerSec = 15f;
    public float runAttackCooldown = 8f;

    private BossSkillSelector skillSelector;
    private float nextWanderTime = 0f;
    private Vector3 cooldownWanderTarget;
    private float lastRunAttackTime = -9999f;

    protected override void Awake()
    {
        base.Awake();
        skillSelector = GetComponent<BossSkillSelector>();

        if (runAttackCollider != null) runAttackCollider.enabled = false;

        core.OnStunned += DisableRunCollider;
        core.OnParried += DisableRunCollider;
        core.OnDeath += DisableRunCollider;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (core != null)
        {
            core.OnStunned -= DisableRunCollider;
            core.OnParried -= DisableRunCollider;
            core.OnDeath -= DisableRunCollider;
        }
    }

    private void DisableRunCollider()
    {
        if (runAttackCollider != null) runAttackCollider.enabled = false;
    }
    protected override void HandleBehaviorLogic(float distToLastPos)
    {
        if (isAttacking || core.currentState == EnemyState.BackWalk) return;
        if (core.IsDead) return;
        if (isMemorizingPlayer)
        {
            StopActionRoutine();

            if (selectedSkill == null)
            {
                selectedSkill = skillSelector.GetNextSkill(distToLastPos);
            }

            // 1. 스킬을 찾지 못했을 때(쿨타임이거나 거리가 멂)
            if (selectedSkill == null)
            {
                // 플레이어가 멀면 뛰어서 쫓아가고, 가까우면 배회(걷기)
                if (distToLastPos > 5f)
                {
                    UpdateChaseTarget();
                }
                else
                {
                    float optimalDist = skillSelector.GetOptimalDistance(null);
                    HandleCooldownWalk(optimalDist);
                }
                return;
            }

            float executeRange = GetSkillRange(selectedSkill) * skillRangeMultiplier;
            bool canAttackGlobal = isDetectedPlayer && (Time.time >= nextAttackTime || skillSelector.isNextSkillCombo);

            if (canAttackGlobal && distToLastPos > executeRange)
            {
                bool canRunAttack = runAttackSkill != null && Time.time >= lastRunAttackTime + runAttackCooldown;
                bool hasMpForRunAttack = core.QueryHasEnoughMP?.Invoke(runAttackSkill.MP_cost) ?? false;

                if (canRunAttack && hasMpForRunAttack)
                {
                    if (UnityEngine.Random.Range(0f, 100f) < runAttackChancePerSec * Time.deltaTime)
                    {
                        lastRunAttackTime = Time.time;
                        actionCoroutine = StartCoroutine(RunAttackSequence(runAttackSkill));
                        return;
                    }
                }
            }

            bool hasEnoughMP = core.QueryHasEnoughMP?.Invoke(selectedSkill.MP_cost) ?? false;

            if (canAttackGlobal && hasEnoughMP)
            {
                if (distToLastPos <= executeRange)
                {
                    actionCoroutine = StartCoroutine(AttackSequence(selectedSkill));
                }
                else if (distToLastPos <= executeRange * walkApproachFactor) // 걷기 접근 거리
                {
                    UpdateWalkApproachTarget();
                }
                else
                {
                    UpdateChaseTarget();
                }
            }
            else
            {
                if (distToLastPos > executeRange * walkApproachFactor)
                {
                    UpdateChaseTarget();
                }
                else
                {
                    float optimalDist = skillSelector.GetOptimalDistance(selectedSkill);
                    HandleCooldownWalk(optimalDist);
                }
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

    private void UpdateWalkApproachTarget()
    {
        core.ChangeState(EnemyState.Chase);
        core.SetMoveAnimations(false, true, false);

        if (Time.time >= nextTargetUpdateTime)
        {
            nextTargetUpdateTime = Time.time + targetUpdateInterval;
            core.CmdMoveTo?.Invoke(lastKnownPlayerPos, 1f);
        }
    }

    private void HandleCooldownWalk(float optimalDistance)
    {
        core.ChangeState(EnemyState.CooldownWalk);
        core.SetMoveAnimations(false, true, false);

        bool isPathPending = core.QueryIsPathPending?.Invoke() ?? false;

        if (Time.time > nextWanderTime || !isPathPending)
        {
            Vector3 dirToMe = (transform.position - lastKnownPlayerPos).normalized;
            cooldownWanderTarget = lastKnownPlayerPos + (dirToMe * optimalDistance);
            nextWanderTime = Time.time + UnityEngine.Random.Range(closeWanderMin, closeWanderMax);

            core.CmdMoveTo?.Invoke(cooldownWanderTarget, 1f);
        }
    }

    private void ChainNextAttackOrIdle()
    {
        if (skillSelector.HasPendingCombo)
        {
            float currentDist = Vector3.Distance(transform.position, Player.Instance.transform.position);
            selectedSkill = skillSelector.GetNextSkill(currentDist);

            if (selectedSkill == runAttackSkill)
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

    private IEnumerator RunAttackSequence(SkillBase skill)
    {
        isAttacking = true;
        core.CmdStopMovement?.Invoke();
        core.ChangeState(EnemyState.Attack);

        if (runAttackCollider != null) runAttackCollider.enabled = true;

        bool attackFinished = false;

        Action<float> onComplete = (nextTime) => {
            nextAttackTime = Time.time + 1f;
            attackFinished = true;
        };

        core.OnAttackCompleted += onComplete;
        core.CmdPerformAttack?.Invoke(skill);

        yield return new WaitUntil(() => attackFinished || skillSelector.isInstantComboTriggered);

        core.OnAttackCompleted -= onComplete;

        if (runAttackCollider != null) runAttackCollider.enabled = false;

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
}