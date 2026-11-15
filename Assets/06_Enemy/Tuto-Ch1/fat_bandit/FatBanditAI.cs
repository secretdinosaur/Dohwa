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

    [Header("Run Attack Specific")]
    public SkillBase runAttackSkill;
    public Collider runAttackCollider;
    public float runAttackChancePerSec = 15f;
    public float runAttackCooldown = 8f;

    private float lastRunAttackTime = -9999f;

    protected override void Awake()
    {
        base.Awake();

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
        if (core.IsDead) return;
        if (isAttacking || actionCoroutine != null) return;

        if (isMemorizingPlayer)
        {
            StopActionRoutine();

            if (selectedSkill == null)
            {
                selectedSkill = core.skillSelector.GetNextSkill(distToLastPos);
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

            bool canAttackGlobal = isDetectedPlayer && (Time.time >= nextAttackTime || core.skillSelector.isNextSkillCombo);
            bool hasEnoughMP = core.QueryHasEnoughMP?.Invoke(selectedSkill.MP_cost) ?? false;

            // 💡 돌진 공격: 거리가 멀 때 확률적으로 발동하는 특수 패턴
            if (canAttackGlobal && distToLastPos > executeRange)
            {
                bool canRunAttack = runAttackSkill != null && Time.time >= lastRunAttackTime + runAttackCooldown;
                bool hasMpForRunAttack = core.QueryHasEnoughMP?.Invoke(runAttackSkill.MP_cost) ?? false;

                if (canRunAttack && hasMpForRunAttack)
                {
                    if (UnityEngine.Random.Range(0f, 100f) < runAttackChancePerSec * Time.deltaTime)
                    {
                        lastRunAttackTime = Time.time;
                        // 💡 라우팅 통합 메서드 사용
                        actionCoroutine = StartCoroutine(ExecuteSkillAndChain(runAttackSkill));
                        return;
                    }
                }
            }

            bool isWithinAttackBox = IsPlayerInSkillBox(selectedSkill, skillRangeMultiplier);

            // 타격 박스 안일 때
            if (isWithinAttackBox)
            {
                if (canAttackGlobal && hasEnoughMP)
                {
                    // 💡 통합 공격/체인 메서드 호출
                    actionCoroutine = StartCoroutine(ExecuteSkillAndChain(selectedSkill));
                    lastActionWasEvasion = false;
                }
                else
                {
                    // 쿨타임 대기 중
                    if (distToLastPos < backWalkRange && !lastActionWasEvasion && !core.skillSelector.HasPendingCombo)
                    {
                        if (UnityEngine.Random.Range(0f, 100f) < strafeOnCloseChance)
                            actionCoroutine = StartCoroutine(StrafeRoutine(UnityEngine.Random.Range(strafeMinDuration, strafeMaxDuration)));
                        else
                            actionCoroutine = StartCoroutine(BackWalkRoutine(backWalkDist));

                        lastActionWasEvasion = true;
                    }
                    else
                    {
                        actionCoroutine = StartCoroutine(StrafeRoutine(UnityEngine.Random.Range(strafeMinDuration, strafeMaxDuration)));
                        lastActionWasEvasion = false;
                    }
                }
            }
            // 타격 박스 밖일 때
            else
            {
                if (distToLastPos < backWalkRange)
                {
                    // 거리는 너무 가까운데 각도가 빗나간 경우 -> 너무 가까우니 회피기동이나 뒤로 물러나면서 각도를 잼
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
                            HandleDistantApproach(executeRange);
                        else
                            actionCoroutine = StartCoroutine(StrafeRoutine(UnityEngine.Random.Range(strafeMinDuration, strafeMaxDuration)));
                    }
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

    // =========================================================================
    // 💡 2번, 3번 개선점: 스킬 라우팅 및 RunAttack 로직 래핑
    // =========================================================================

    protected override IEnumerator RouteAndExecuteSkill(SkillBase skill)
    {
        if (skill == runAttackSkill) yield return RunAttackSequence(skill);
        else yield return base.RouteAndExecuteSkill(skill);
    }

    private IEnumerator RunAttackSequence(SkillBase skill)
    {
        try
        {
            if (runAttackCollider != null) runAttackCollider.enabled = true;
            yield return base.PerformAttackSequence(skill);
        }
        finally
        {
            if (runAttackCollider != null) runAttackCollider.enabled = false;
        }
    }
}