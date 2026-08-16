using UnityEngine;
using System;
using System.Collections;

[RequireComponent(typeof(SkillSelector))]
public class StrongBanditAI : EnemyAIBase
{
    [Header("Start Fight")]
    public GameObject FightWall;
    public float startFightPreDelay = 1.0f;
    public float startFightAnimDuration = 2.0f;

    [Header("Behavior Tuning")]
    public float walkApproachFactor = 1.5f;

    [Header("Evasion Specific")]
    public float backWalkTriggerFactor = 0.7f;
    [Range(0f, 100f)] public float strafeOnCloseChance = 30f;
    [Range(0f, 100f)] public float parriedBackWalkChance = 60f;
    [Range(0f, 100f)] public float normalBackWalkChance = 20f;
    private bool lastActionWasBackWalk = false;

    [Header("Guard & Counter Specific")]
    public SkillBase guardSkill;
    public SkillBase counterSkill;
    public float guardChancePerSec = 15f;
    public float guardCooldown = 8f;
    public float guardTriggerFactor = 0.5f;
    public float guardProbability = 0.7f;
    public float maxGuardDistance = 5.0f;
    public float guardStartupDelay = 0.5f;
    public float postStunGuardDelay = 2.0f;

    private SkillSelector skillSelector;
    private float lastGuardTime = -9999f;
    private bool isStartingFight = false;
    private bool wasStunned = false;
    private float nextAllowedGuardTime = -9999f;

    private Action<float> currentAttackCompleteHandler;

    protected override void Awake()
    {
        base.Awake();
        skillSelector = GetComponent<SkillSelector>();

        core.OnPlayerDetected += HandlePlayerDetected;
        core.OnStunned += HandleInterruptSpecial;
        core.OnDeath += HandleInterruptSpecial;
        core.OnRevived += HandleRevived;
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
        StopActionRoutine();
        actionCoroutine = StartCoroutine(StartFightSequence());
    }

    private IEnumerator StartFightSequence()
    {
        isStartingFight = true;

        core.CmdStopMovement?.Invoke();
        core.ChangeState(EnemyState.Idle);
        core.SetMoveAnimations(0f, 0f);
        core.CmdLookAt?.Invoke(Player.Instance.transform.position);

        if (FightWall != null) FightWall.SetActive(true);

        yield return new WaitForSeconds(startFightPreDelay);

        core.animator.SetTrigger("StartFight");

        yield return new WaitForSeconds(startFightAnimDuration);

        isStartingFight = false;
        actionCoroutine = null;
    }

    protected override void HandleBehaviorLogic(float distToLastPos)
    {
        if (isAttacking || isStartingFight || core.currentState == EnemyState.BackWalk || core.currentState == EnemyState.CooldownWalk) return;
        if (core.IsDead) return;

        if (wasStunned)
        {
            wasStunned = false;
            nextAllowedGuardTime = Time.time + postStunGuardDelay;
        }

        if (isMemorizingPlayer)
        {
            StopActionRoutine();

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
                    if (UnityEngine.Random.Range(0f, 100f) < 60f)
                    {
                        float runSpeed = UnityEngine.Random.value > 0.5f ? 2f : 1f;
                        actionCoroutine = StartCoroutine(StrafeRoutine(UnityEngine.Random.Range(strafeMinDuration, strafeMaxDuration), runSpeed));
                    }
                    lastActionWasBackWalk = false;
                }
                return;
            }

            float executeRange = GetSkillRange(selectedSkill) * skillRangeMultiplier;
            float backWalkRange = executeRange * backWalkTriggerFactor;

            bool canAttackGlobal = isDetectedPlayer && (Time.time >= nextAttackTime || skillSelector.isNextSkillCombo);
            bool hasEnoughMP = core.QueryHasEnoughMP?.Invoke(selectedSkill.MP_cost) ?? false;

            if (distToLastPos <= executeRange)
            {
                float guardTriggerRange = executeRange * guardTriggerFactor;

                if (distToLastPos < guardTriggerRange && canGuard && hasMpForGuard && UnityEngine.Random.value <= guardProbability && !skillSelector.HasPendingCombo)
                {
                    lastGuardTime = Time.time;
                    actionCoroutine = StartCoroutine(GuardSequence(guardSkill));
                    return;
                }

                if (distToLastPos < backWalkRange && !skillSelector.HasPendingCombo)
                {
                    if (UnityEngine.Random.Range(0f, 100f) < strafeOnCloseChance || lastActionWasBackWalk)
                    {
                        actionCoroutine = StartCoroutine(StrafeRoutine(UnityEngine.Random.Range(strafeMinDuration, strafeMaxDuration), 1));
                        lastActionWasBackWalk = false;
                        return;
                    }
                    else
                    {
                        actionCoroutine = StartCoroutine(BackWalkRoutine(backWalkDist));
                        lastActionWasBackWalk = true;
                    }
                }
                else if (canAttackGlobal && hasEnoughMP)
                {
                    if (UnityEngine.Random.Range(0f, 100f) < 20f && !skillSelector.HasPendingCombo)
                    {
                        if (UnityEngine.Random.value < 0.5f)
                        {
                            actionCoroutine = StartCoroutine(BackWalkRoutine(backWalkDist));
                            lastActionWasBackWalk = true;
                        }
                        else
                        {
                            float runSpeed = UnityEngine.Random.value > 0.5f ? 2f : 1f;
                            actionCoroutine = StartCoroutine(StrafeRoutine(UnityEngine.Random.Range(strafeMinDuration, strafeMaxDuration), runSpeed));
                            lastActionWasBackWalk = false;
                        }
                    }
                    else
                    {
                        actionCoroutine = StartCoroutine(PerformAttackSequence(selectedSkill));
                        lastActionWasBackWalk = false;
                    }
                }
                else
                {
                    if (UnityEngine.Random.Range(0f, 100f) < 60f)
                    {
                        float runSpeed = UnityEngine.Random.value > 0.5f ? 2f : 1f;
                        actionCoroutine = StartCoroutine(StrafeRoutine(UnityEngine.Random.Range(strafeMinDuration, strafeMaxDuration), runSpeed));
                    }
                    lastActionWasBackWalk = false;
                }
            }
            else
            {
                if (canAttackGlobal && hasEnoughMP)
                {
                    if (distToLastPos <= executeRange * walkApproachFactor)
                        UpdateWalkApproachTarget();
                    else
                        UpdateChaseTarget();
                }
                else
                {
                    if (distToLastPos > executeRange * walkApproachFactor)
                    {
                        UpdateChaseTarget();
                    }
                    else
                    {
                        if (UnityEngine.Random.Range(0f, 100f) < 60f)
                        {
                            float runSpeed = UnityEngine.Random.value > 0.5f ? 2f : 1f;
                            actionCoroutine = StartCoroutine(StrafeRoutine(UnityEngine.Random.Range(strafeMinDuration, strafeMaxDuration), runSpeed));
                        }
                        lastActionWasBackWalk = false;
                    }
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

        core.CmdStopMovement?.Invoke();
        core.ChangeState(EnemyState.Attack);

        bool actionFinished = false;

        currentAttackCompleteHandler = (nextTime) => {
            nextAttackTime = Time.time + skillSelector.GetLastSkillDelay();
            actionFinished = true;
        };

        core.OnAttackCompleted += currentAttackCompleteHandler;
        core.CmdPerformAttack?.Invoke(skill);

        float startupTimer = 0f;
        while (startupTimer < guardStartupDelay && !actionFinished)
        {
            startupTimer += Time.deltaTime;
            yield return null;
        }

        if (!actionFinished)
        {
            core.isGuarding = true;
        }

        yield return new WaitUntil(() => actionFinished || !core.isGuarding || core.wasParried || core.currentState == EnemyState.Stun || core.IsDead);

        ClearAttackHandler();

        core.isGuarding = false;
        ChainNextAttackOrIdle();
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

        yield return new WaitUntil(() => attackFinished || skillSelector.isInstantComboTriggered || core.wasParried || core.currentState == EnemyState.Stun || core.IsDead);

        ClearAttackHandler();

        if (skillSelector.isInstantComboTriggered)
        {
            skillSelector.isInstantComboTriggered = false;
        }

        float chance = core.wasParried ? parriedBackWalkChance : normalBackWalkChance;
        core.wasParried = false;

        if (UnityEngine.Random.Range(0f, 100f) < chance && !skillSelector.HasPendingCombo)
        {
            yield return StartCoroutine(BackWalkRoutine(backWalkDist));
            lastActionWasBackWalk = true;
        }

        ChainNextAttackOrIdle();
    }

    protected override IEnumerator PerformAttackSequence(SkillBase skill)
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

        yield return new WaitUntil(() => attackFinished || skillSelector.isInstantComboTriggered || core.wasParried || core.currentState == EnemyState.Stun || core.IsDead);

        ClearAttackHandler();

        if (skillSelector.isInstantComboTriggered)
        {
            skillSelector.isInstantComboTriggered = false;
            core.CmdCancelAttack?.Invoke();
        }

        float chance = core.wasParried ? parriedBackWalkChance : normalBackWalkChance;
        core.wasParried = false;

        if (UnityEngine.Random.Range(0f, 100f) < chance && !skillSelector.HasPendingCombo)
        {
            yield return StartCoroutine(BackWalkRoutine(backWalkDist));
            lastActionWasBackWalk = true;
        }

        ChainNextAttackOrIdle();
    }
}