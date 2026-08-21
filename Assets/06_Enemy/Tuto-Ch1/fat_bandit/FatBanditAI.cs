using UnityEngine;
using System;
using System.Collections;

[RequireComponent(typeof(SkillSelector))]
public class FatBanditAI : EnemyAIBase
{
    [Header("Behavior Tuning")]
    public float walkApproachFactor = 1.5f;
    [Range(0f, 1f)] public float walkApproachChance = 0.2f;
    private bool isChoosingWalkApproach = false;

    [Header("Strafe & Evasion Specific")]
    public float backWalkTriggerFactor = 0.7f;
    [Range(0f, 100f)] public float strafeOnCloseChance = 30f;
    private bool lastActionWasBackWalk = false;

    [Header("Run Attack Specific")]
    public SkillBase runAttackSkill;
    public Collider runAttackCollider;
    public float runAttackChancePerSec = 15f;
    public float runAttackCooldown = 8f;

    private SkillSelector skillSelector;
    private float lastRunAttackTime = -9999f;

    protected override void Awake()
    {
        base.Awake();
        skillSelector = GetComponent<SkillSelector>();

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
            float backWalkRange = executeRange * backWalkTriggerFactor;

            bool canAttackGlobal = isDetectedPlayer && (Time.time >= nextAttackTime || skillSelector.isNextSkillCombo);
            bool hasEnoughMP = core.QueryHasEnoughMP?.Invoke(selectedSkill.MP_cost) ?? false;

            // 💡 돌진 공격: 거리가 멀 때 확률적으로 발동하는 특수 패턴 (기존 로직 유지)
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

            // 💡 추가됨: 실제 스킬의 타격 박스(또는 범위) 내에 있는지 확인
            bool isWithinAttackBox = IsPlayerInSkillBox(selectedSkill, skillRangeMultiplier);

            // 타격 박스 안일 때
            if (isWithinAttackBox)
            {
                if (canAttackGlobal && hasEnoughMP)
                {
                    actionCoroutine = StartCoroutine(AttackSequence(selectedSkill));
                    lastActionWasBackWalk = false;
                }
                else
                {
                    // 쿨타임 대기 중
                    if (distToLastPos < backWalkRange && !lastActionWasBackWalk && !skillSelector.HasPendingCombo)
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
            // 💡 추가됨: 타격 박스 밖일 때의 유동적 위치 선정 로직
            else
            {
                if (distToLastPos < backWalkRange)
                {
                    // 거리는 너무 가까운데 각도가 빗나간 경우 -> 너무 가까우니 회피기동이나 뒤로 물러나면서 각도를 잼
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
                    // 거리가 멀거나 각도가 안 맞으므로 접근
                    if (canAttackGlobal && hasEnoughMP)
                    {
                        if (distToLastPos <= executeRange * walkApproachFactor)
                            UpdateWalkApproachTarget();
                        else
                            HandleDistantApproach(executeRange);
                    }
                    else
                    {
                        if (distToLastPos > executeRange * walkApproachFactor)
                        {
                            HandleDistantApproach(executeRange);
                        }
                        else
                        {
                            actionCoroutine = StartCoroutine(StrafeRoutine(UnityEngine.Random.Range(strafeMinDuration, strafeMaxDuration)));
                        }
                    }
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
}