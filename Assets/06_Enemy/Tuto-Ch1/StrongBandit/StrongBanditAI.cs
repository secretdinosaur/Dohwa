using UnityEngine;
using System;
using System.Collections;

[RequireComponent(typeof(BossSkillSelector))]
public class StrongBanditAI : EnemyAIBase
{
    [Header("Start Fight")]
    public GameObject FightWall;
    public float startFightPreDelay = 1.0f;
    public float startFightAnimDuration = 2.0f;

    [Header("Behavior Tuning")]
    public float walkApproachFactor = 1.5f;
    public float closeWanderMin = 1.5f;
    public float closeWanderMax = 3.5f;

    [Header("Guard & Counter Specific")]
    public SkillBase guardSkill;
    public SkillBase counterSkill;
    public float guardChancePerSec = 15f;
    public float guardCooldown = 8f;
    public float guardTriggerFactor = 0.5f;
    public float guardProbability = 0.7f;
    public float maxGuardDistance = 5.0f;

    private BossSkillSelector skillSelector;
    private float nextWanderTime = 0f;
    private Vector3 cooldownWanderTarget;
    private float lastGuardTime = -9999f;
    private bool isStartingFight = false;

    private Action<float> currentAttackCompleteHandler;

    protected override void Awake()
    {
        base.Awake();
        skillSelector = GetComponent<BossSkillSelector>();

        core.OnPlayerDetected += HandlePlayerDetected;
        core.OnStunned += HandleInterruptSpecial;
        core.OnDeath += HandleInterruptSpecial;
        core.OnRevived += HandleRevived;

        // 💡 범용 가드 성공 이벤트에 카운터 공격 연결
        core.OnGuardSuccess += TriggerCounter;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (core != null)
        {
            core.OnPlayerDetected -= HandlePlayerDetected;
            core.OnStunned -= HandleInterruptSpecial;
            core.OnDeath -= HandleInterruptSpecial;
            core.OnRevived -= HandleRevived;

            // 💡 이벤트 해제
            core.OnGuardSuccess -= TriggerCounter;
        }
    }

    private void HandleInterruptSpecial()
    {
        isStartingFight = false;
        core.isGuarding = false; // 💡 core의 상태 변수 사용
    }

    private void HandleRevived()
    {
        isStartingFight = false;
        core.isGuarding = false; // 💡 core의 상태 변수 사용
        if (FightWall != null) FightWall.SetActive(false);
        animator.Play("BeforeFight", 0, 0f);

    }

    private void HandlePlayerDetected()
    {
        StartCoroutine(StartFightSequence());
    }

    private IEnumerator StartFightSequence()
    {
        isStartingFight = true;

        core.CmdStopMovement?.Invoke();
        core.ChangeState(EnemyState.Idle);
        core.SetMoveAnimations(false, false, false);
        core.CmdLookAt?.Invoke(Player.Instance.transform.position);

        if (FightWall != null) FightWall.SetActive(true);

        yield return new WaitForSeconds(startFightPreDelay);

        core.animator.SetTrigger("StartFight");

        yield return new WaitForSeconds(startFightAnimDuration);

        isStartingFight = false;
    }

    protected override void HandleBehaviorLogic(float distToLastPos)
    {
        if (isAttacking || isStartingFight || core.currentState == EnemyState.BackWalk) return;
        if (core.IsDead) return;
        if (isMemorizingPlayer)
        {
            StopActionRoutine();

            bool canGuard = guardSkill != null && Time.time >= lastGuardTime + guardCooldown;
            bool hasMpForGuard = canGuard && (core.QueryHasEnoughMP?.Invoke(guardSkill.MP_cost) ?? false);

            if (canGuard && hasMpForGuard && distToLastPos <= maxGuardDistance)
            {
                if (UnityEngine.Random.Range(0f, 100f) < guardChancePerSec * Time.deltaTime)
                {
                    lastGuardTime = Time.time;
                    actionCoroutine = StartCoroutine(GuardSequence(guardSkill));
                    return;
                }
            }

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
                    float optimalDist = skillSelector.GetOptimalDistance(null);
                    HandleCooldownWalk(optimalDist);
                }
                return;
            }

            float executeRange = GetSkillRange(selectedSkill) * skillRangeMultiplier;
            bool canAttackGlobal = isDetectedPlayer && (Time.time >= nextAttackTime || skillSelector.isNextSkillCombo);
            bool hasEnoughMP = core.QueryHasEnoughMP?.Invoke(selectedSkill.MP_cost) ?? false;

            if (canAttackGlobal && hasEnoughMP)
            {
                if (distToLastPos <= executeRange)
                {
                    float guardTriggerRange = executeRange * guardTriggerFactor;

                    if (distToLastPos < guardTriggerRange && canGuard && hasMpForGuard && UnityEngine.Random.value <= guardProbability)
                    {
                        lastGuardTime = Time.time;
                        actionCoroutine = StartCoroutine(GuardSequence(guardSkill));
                    }
                    else
                    {
                        actionCoroutine = StartCoroutine(AttackSequence(selectedSkill));
                    }
                }
                else if (distToLastPos <= executeRange * walkApproachFactor)
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

    public void TriggerCounter()
    {
        if (counterSkill == null) return;

        core.isGuarding = false; // 💡 카운터 발동 시 가드 판정 해제

        if (actionCoroutine != null) StopCoroutine(actionCoroutine);
        Player.Instance.PlayerAttack.CancelCurrentSkill();
        ClearAttackHandler();
        core.CmdCancelAttack?.Invoke();
        skillSelector.isInstantComboTriggered = false;

        actionCoroutine = StartCoroutine(CounterSequence(counterSkill));
    }

    private void ClearAttackHandler()
    {
        if (currentAttackCompleteHandler != null)
        {
            core.OnAttackCompleted -= currentAttackCompleteHandler;
            currentAttackCompleteHandler = null;
        }
    }

    private IEnumerator GuardSequence(SkillBase skill)
    {
        isAttacking = true;
        core.isGuarding = true; // 💡 가드 판정 시작

        core.CmdStopMovement?.Invoke();
        core.ChangeState(EnemyState.Attack);

        bool actionFinished = false;

        currentAttackCompleteHandler = (nextTime) => {
            nextAttackTime = Time.time + skillSelector.GetLastSkillDelay();
            actionFinished = true;
        };

        core.OnAttackCompleted += currentAttackCompleteHandler;
        core.CmdPerformAttack?.Invoke(skill);

        // 💡 가드 모션이 끝나거나, 누군가에 의해 가드가 깨졌을 때(카운터 발동 등) 루프 탈출
        yield return new WaitUntil(() => actionFinished || !core.isGuarding);

        ClearAttackHandler();

        // 자연스럽게 가드가 끝난 경우
        if (core.isGuarding)
        {
            core.isGuarding = false;
            ChainNextAttackOrIdle();
        }
    }

    private IEnumerator CounterSequence(SkillBase skill)
    {
        isAttacking = true;
        core.CmdStopMovement?.Invoke();
        core.ChangeState(EnemyState.Attack);

        bool attackFinished = false;

        currentAttackCompleteHandler = (nextTime) => {
            nextAttackTime = Time.time + skillSelector.GetLastSkillDelay();
            attackFinished = true;
        };

        core.OnAttackCompleted += currentAttackCompleteHandler;
        core.CmdPerformAttack?.Invoke(skill);

        yield return new WaitUntil(() => attackFinished || skillSelector.isInstantComboTriggered);

        ClearAttackHandler();

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

        currentAttackCompleteHandler = (nextTime) => {
            float delay = skillSelector.GetLastSkillDelay();
            nextAttackTime = Time.time + delay;
            attackFinished = true;
        };

        core.OnAttackCompleted += currentAttackCompleteHandler;
        core.CmdPerformAttack?.Invoke(skill);

        yield return new WaitUntil(() => attackFinished || skillSelector.isInstantComboTriggered);

        ClearAttackHandler();

        if (skillSelector.isInstantComboTriggered)
        {
            skillSelector.isInstantComboTriggered = false;
        }

        ChainNextAttackOrIdle();
    }

    private void ChainNextAttackOrIdle()
    {
        if (skillSelector.HasPendingCombo)
        {
            float currentDist = Vector3.Distance(transform.position, Player.Instance.transform.position);
            selectedSkill = skillSelector.GetNextSkill(currentDist);

            actionCoroutine = StartCoroutine(AttackSequence(selectedSkill));
        }
        else
        {
            isAttacking = false;
            selectedSkill = null;
            core.ChangeState(EnemyState.Idle);
            actionCoroutine = null;
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