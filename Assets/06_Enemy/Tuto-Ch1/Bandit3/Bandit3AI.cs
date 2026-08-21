using System;
using System.Collections;
using UnityEngine;

public class Bandit3AI : EnemyAIBase
{
    [Header("Bandit3 Specific")]
    public float parriedBackWalkChance = 60f;
    public float normalBackWalkChance = 20f;
    public float backWalkTriggerFactor = 0.7f;
    [Range(0f, 100f)] public float strafeOnCloseChance = 30f;

    private bool lastActionWasBackWalk = false;
    private bool lastActionWasStep = false; // 추가: 연속 스텝 방지 플래그
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

            // 💡 추가됨: 단순 거리 비교 대신 실제 스킬의 타격 박스(또는 범위) 내에 있는지 확인
            bool isWithinAttackBox = IsPlayerInSkillBox(selectedSkill, skillRangeMultiplier);

            if (isWithinAttackBox)
            {
                // 타격 박스 안이지만, 플레이어가 너무 바짝 붙었을 경우 뒤로 물러날 확률 연산
                if (dist < backWalkRange && !lastActionWasBackWalk && !core.skillSelector.HasPendingCombo)
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
                    lastActionWasStep = false; // 스텝이 아니므로 초기화
                }
                else if (canAttackGlobal && hasEnoughMP)
                {
                    actionCoroutine = StartCoroutine(PerformAttackSequence(selectedSkill));
                    lastActionWasBackWalk = false;
                }
                else
                {
                    // 스킬 쿨타임 대기 중 특유의 게걸음/후퇴 무빙
                    float ran = UnityEngine.Random.value;
                    if (ran < 0.4f)
                        actionCoroutine = StartCoroutine(StrafeRoutine(UnityEngine.Random.Range(strafeMinDuration, strafeMaxDuration), 1));
                    else if (ran < 0.8f)
                        actionCoroutine = StartCoroutine(StrafeRoutine(UnityEngine.Random.Range(strafeMinDuration, strafeMaxDuration), 2));
                    else
                    {
                        actionCoroutine = StartCoroutine(BackWalkRoutine(backWalkDist));
                        lastActionWasBackWalk = true;
                        lastActionWasStep = false; // 스텝이 아니므로 초기화
                        return;
                    }

                    lastActionWasBackWalk = false;
                    lastActionWasStep = false; // 스텝이 아니므로 초기화
                }
            }
            else
            {
                // 💡 추가됨: 타격 박스 밖에 있을 때의 유동적 위치 선정 로직
                if (dist < backWalkRange)
                {
                    // 거리는 가까운데 각도가 빗나간 경우 -> 너무 가까우니 회피기동이나 뒤로 물러나면서 각도를 잼
                    if (UnityEngine.Random.Range(0f, 100f) < strafeOnCloseChance || lastActionWasBackWalk)
                    {
                        actionCoroutine = StartCoroutine(StrafeRoutine(UnityEngine.Random.Range(strafeMinDuration, strafeMaxDuration), 2));
                        lastActionWasBackWalk = false;
                    }
                    else
                    {
                        actionCoroutine = StartCoroutine(BackWalkRoutine(backWalkDist));
                        lastActionWasBackWalk = true;
                    }
                    lastActionWasStep = false;
                }
                else
                {
                    // 멀리 있는 경우 추적하여 간격 좁히기
                    UpdateChaseTarget();
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

    protected override IEnumerator PerformAttackSequence(SkillBase skill)
    {
        isAttacking = true;

        // 추가: lastActionWasStep이 false일 때만 스텝 실행 가능
        if (!core.skillSelector.isNextSkillCombo && !lastActionWasStep && UnityEngine.Random.value < 0.4f)
        {
            yield return StartCoroutine(ExecuteStepRoutine(2.5f, 0.4f, 0.25f));

            lastActionWasStep = true; // 스텝을 실행했으므로 플래그 켜기

            if (Player.Instance == null || core.IsDead) yield break;

            // 스텝 이후 타격 박스 안에 여전히 있는지 체크하여 공격 진행 여부 결정
            bool isWithinAttackBoxAfterStep = IsPlayerInSkillBox(skill, skillRangeMultiplier);
            float distToPlayer = Vector3.Distance(transform.position, Player.Instance.transform.position);
            float executeRange = GetSkillRange(skill) * skillRangeMultiplier;
            float backWalkRange = executeRange * backWalkTriggerFactor;

            // 💡 스텝 이후 거리가 틀어지거나 박스를 벗어났다면 공격 취소
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
            lastActionWasStep = false; // 스텝을 뛰지 않고 공격했으므로 플래그 끄기
        }

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
            lastActionWasStep = false; // 백워크를 했으므로 플래그 끄기
        }

        ChainNextAttackOrIdle();
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