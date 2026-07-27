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

    // 💡 추가된 설정값
    public float guardStartupDelay = 0.2f; // 가드 애니메이션 시작 후 판정이 생길 때까지의 여유 시간
    public float postStunGuardDelay = 2.0f; // 기절 직후 가드를 올리지 않는 쿨타임

    private BossSkillSelector skillSelector;
    private float nextWanderTime = 0f;
    private Vector3 cooldownWanderTarget;
    private float lastGuardTime = -9999f;
    private bool isStartingFight = false;

    // 💡 기절 직후 쿨타임 관리를 위한 변수
    private bool wasStunned = false;
    private float nextAllowedGuardTime = -9999f;

    private Action<float> currentAttackCompleteHandler;

    protected override void Awake()
    {
        base.Awake();
        skillSelector = GetComponent<BossSkillSelector>();

        core.OnPlayerDetected += HandlePlayerDetected;
        core.OnStunned += HandleInterruptSpecial;
        core.OnDeath += HandleInterruptSpecial;
        core.OnRevived += HandleRevived;

        // 범용 가드 성공 이벤트에 카운터 공격 연결
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

            core.OnGuardSuccess -= TriggerCounter;
        }
    }

    private void HandleInterruptSpecial()
    {
        isStartingFight = false;
        core.isGuarding = false;

        // 💡 스턴 상태 진입 기록
        wasStunned = true;
    }

    private void HandleRevived()
    {
        isStartingFight = false;
        core.isGuarding = false;
        wasStunned = false;
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

        // 💡 기절에서 회복되어 AI 로직이 다시 돌기 시작할 때 가드 쿨타임 세팅
        if (wasStunned)
        {
            wasStunned = false;
            nextAllowedGuardTime = Time.time + postStunGuardDelay;
        }

        if (isMemorizingPlayer)
        {
            StopActionRoutine();

            // 💡 기본 쿨타임 + 기절 직후 쿨타임 모두 충족해야 가드 가능
            bool canGuard = guardSkill != null
                            && Time.time >= lastGuardTime + guardCooldown
                            && Time.time >= nextAllowedGuardTime;

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

        core.isGuarding = false;

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
        // 💡 즉시 가드 판정을 켜지 않음

        core.CmdStopMovement?.Invoke();
        core.ChangeState(EnemyState.Attack);

        bool actionFinished = false;

        currentAttackCompleteHandler = (nextTime) => {
            nextAttackTime = Time.time + skillSelector.GetLastSkillDelay();
            actionFinished = true;
        };

        core.OnAttackCompleted += currentAttackCompleteHandler;
        core.CmdPerformAttack?.Invoke(skill);

        // 💡 설정한 시간(선딜레이)만큼 대기 (도중에 캔슬되면 루프 탈출)
        float startupTimer = 0f;
        while (startupTimer < guardStartupDelay && !actionFinished)
        {
            startupTimer += Time.deltaTime;
            yield return null;
        }

        // 💡 선딜레이를 무사히 넘겼다면 가드 판정 On
        if (!actionFinished)
        {
            core.isGuarding = true;
        }

        yield return new WaitUntil(() => actionFinished || !core.isGuarding);

        ClearAttackHandler();

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
            core.CmdCancelAttack?.Invoke();
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