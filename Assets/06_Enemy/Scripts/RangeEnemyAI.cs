using UnityEngine;
using System;
using System.Collections;

public class RangeEnemyAI : EnemyAIBase
{
    [Header("Range Specific")]
    public float LookAtPlayerBeforeAttackTime = 1f;

    protected override void HandleBehaviorLogic(float distToLastPos)
    {
        if (core.IsDead) return;
        if (isAttacking || core.currentState == EnemyState.BackWalk || core.currentState == EnemyState.CooldownWalk || core.currentState == EnemyState.Attack) return;

        if (isMemorizingPlayer)
        {
            StopActionRoutine();

            if (selectedSkill == null)
                selectedSkill = core.skillSelector.GetNextSkill(distToLastPos);

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
                if (distToLastPos < backWalkRange && !lastActionWasEvasion && !core.skillSelector.HasPendingCombo)
                {
                    if (UnityEngine.Random.Range(0f, 100f) < strafeOnCloseChance)
                        actionCoroutine = StartCoroutine(StrafeRoutine(UnityEngine.Random.Range(strafeMinDuration, strafeMaxDuration), 2));
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
                    float ran = UnityEngine.Random.value;
                    if (ran < 0.3f)
                        actionCoroutine = StartCoroutine(StrafeRoutine(UnityEngine.Random.Range(strafeMinDuration, strafeMaxDuration), 1));
                    else if (ran < 0.8f)
                        actionCoroutine = StartCoroutine(StrafeRoutine(UnityEngine.Random.Range(strafeMinDuration, strafeMaxDuration), 2));

                    lastActionWasEvasion = false;
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

    // 💡 2번, 3번 개선점: 원거리 특유의 타겟 록온(조준) 로직만 오버라이드 (코루틴 캔슬 누수 완벽 차단)
    protected override IEnumerator PerformAttackSequence(SkillBase skill)
    {
        Coroutine aimCoroutine = StartCoroutine(AimAtPlayerRoutine(LookAtPlayerBeforeAttackTime));

        yield return StartCoroutine(base.PerformAttackSequence(skill));

        if (aimCoroutine != null) StopCoroutine(aimCoroutine);
    }

    private IEnumerator AimAtPlayerRoutine(float time)
    {
        float elapsed = 0f;
        while (elapsed < time)
        {
            // 공격 상태가 취소(Stun 등)되었거나 죽었다면 조준 즉시 정지
            if (!isAttacking || core.IsDead || core.currentState != EnemyState.Attack) yield break;

            elapsed += Time.deltaTime;

            if (!CheckForPlayer())
            {
                // 플레이어가 벽 뒤로 숨으면 공격 즉시 캔슬!
                core.CmdCancelAttack?.Invoke();
                CancelCurrentSkillAction();
                // 💡 여기서 base.PerformAttackSequence 쪽의 WaitUntil이 캔슬을 자동으로 감지하고 안전하게 정리함.
                yield break;
            }

            core.CmdLookAt?.Invoke(Player.Instance.transform.position);
            yield return null;
        }
    }
}