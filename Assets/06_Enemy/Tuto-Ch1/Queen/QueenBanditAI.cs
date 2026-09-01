using UnityEngine;
using System;
using System.Collections;
using System.Linq;
using UnityEngine.UIElements;

[RequireComponent(typeof(SkillSelector))]
public class QueenBanditAI : EnemyAIBase
{
    [Header("Visuals & Audio (Queen Specific)")]
    public GameObject[] AfterImages;
    public Material[] AfterImageMats;
    public AudioClip PhaseTransitionSound;
    public AudioClip BackStepSound;
    public AudioClip StepSound;
    public AudioClip[] BossMusic;
    public TrailRenderer rightTrail;
    public TrailRenderer leftTrail;

    [Header("Start Fight")]
    public GameObject FightWall;
    public float startFightDelay = 2.5f;

    [Header("Behavior Tuning")]
    public float walkApproachFactor = 1.5f;
    public float chaseBuffer = 1.0f;
    public float closeWanderMin = 1.5f;
    public float closeWanderMax = 3.5f;
    public float stateChangeDelay = 2.0f;

    [Header("Strafe & Step Adjustments")]
    [Range(0f, 100f)] public float idleStrafeChance = 15f;

    [Header("Special Skill Logic (Step Before Attack)")]
    public SkillBase[] leftStepSkills;
    public SkillBase[] rightStepSkills;
    public SkillBase[] preBackStepSkills;

    [Header("Delayed Effect Skills")]
    public SkillBase[] earthquakeSkills;
    public float earthquakeEffectDelay = 10f;

    public SkillBase PushSkill;

    [Header("Step Tuning")]
    public float sideStepDuration = 0.5f;
    public float attackBackStepDuration = 0.3f;
    private UnityEngine.AI.NavMeshAgent agent;

    [Header("Evasive BackStep")]
    public float evasionTriggerFactor = 0.5f;
    public float evasionDuration = 0.3f;
    public float evasionCooldown = 5f;
    public float evasionProbability = 0.7f;

    [Header("Attack 9 Dash Settings")]
    public float attack9DashDuration = 0.3f;
    public float attack9DashDelay = 0.2f;
    public AnimationCurve attack9DashCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Skill Timeout")]
    public float skillHoldTimeout = 5f;
    private float skillHoldTimer = 0f;

    private SkillSelector skillSelector;
    private float nextWanderTime = 0f;
    private Vector3 cooldownWanderTarget;

    private float lastEvasionTime = -9999f;
    private bool isStepping = false;
    private bool isTransitioning = false;
    private bool isStartingFight = false;
    private float currentStrafeDir = 1f;

    private float nextStateChangeTime = 0f;

    private Coroutine activeDashCoroutine;
    private Coroutine fightSequenceRoutine;

    protected override void Awake()
    {
        base.Awake();
        skillSelector = GetComponent<SkillSelector>();
        agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        core.OnStunned += HandleInterruptSpecial;
        core.OnDeath += HandleInterruptSpecial;
        core.OnPhaseTransitionStart += HandlePhaseTransition;
        core.OnRevived += HandleRevived;
        core.OnPlayerDetected += HandlePlayerDetected;
        core.OnPlayerHit += TriggerAirborneHit;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (core != null)
        {
            core.OnStunned -= HandleInterruptSpecial;
            core.OnDeath -= HandleInterruptSpecial;
            core.OnPhaseTransitionStart -= HandlePhaseTransition;
            core.OnRevived -= HandleRevived;
            core.OnPlayerDetected -= HandlePlayerDetected;
            core.OnPlayerHit -= TriggerAirborneHit;
        }
    }

    private void HandleInterruptSpecial()
    {
        isStepping = false;
        isTransitioning = false;
        isStartingFight = false;
        isAttacking = false;

        ClearAttackHandler();

        if (activeDashCoroutine != null)
        {
            StopCoroutine(activeDashCoroutine);
            activeDashCoroutine = null;
        }

        if (actionCoroutine != null)
        {
            StopCoroutine(actionCoroutine);
            actionCoroutine = null;
        }

        StopActionRoutine();

        if (!core.IsDead)
        {
            core.CmdCancelAttack?.Invoke();
        }

        if (fightSequenceRoutine != null)
        {
            StopCoroutine(fightSequenceRoutine);
            fightSequenceRoutine = null;
        }

        if (agent != null && !agent.enabled && !core.IsDead)
        {
            SnapToGround();
            agent.enabled = true;
        }
    }

    private void HandleRevived()
    {
        if (agent != null && !agent.enabled)
        {
            agent.enabled = true;
            core.CmdTrySnapToNavMesh?.Invoke();
        }

        if (fightSequenceRoutine != null)
        {
            StopCoroutine(fightSequenceRoutine);
            fightSequenceRoutine = null;
        }

        skillSelector.SetPhase(1);
        isTransitioning = false;
        isStartingFight = false;
        isAttacking = false;
        isStepping = false;
        isMemorizingPlayer = false;
        lastActionWasEvasion = false;
        selectedSkill = null;
        StopActionRoutine();

        foreach (var img in AfterImages)
        {
            if (img != null) img.SetActive(false);
        }

        if (FightWall != null) FightWall.SetActive(false);

        animator.Play("BeforeFight", 0, 0f);
    }

    private void HandlePlayerDetected()
    {
        if (fightSequenceRoutine != null) StopCoroutine(fightSequenceRoutine);
        fightSequenceRoutine = StartCoroutine(StartFightSequence());
    }

    private IEnumerator StartFightSequence()
    {
        isStartingFight = true;

        core.CmdStopMovement?.Invoke();
        core.ChangeState(EnemyState.Idle);
        core.SetMoveAnimations(0f, 0f);
        core.CmdLookAt?.Invoke(Player.Instance.transform.position);

        if (FightWall != null) FightWall.SetActive(true);

        core.animator.SetTrigger("StartFight");

        yield return new WaitForSeconds(startFightDelay);

        isStartingFight = false;
        fightSequenceRoutine = null;
    }

    private void HandlePhaseTransition(int newPhase)
    {
        if (newPhase == 2)
        {
            StartCoroutine(PhaseTransitionSequence());
        }
    }

    private IEnumerator PhaseTransitionSequence()
    {
        isTransitioning = true;
        isStepping = false;
        isAttacking = false;

        ClearAttackHandler();
        if (activeDashCoroutine != null)
        {
            StopCoroutine(activeDashCoroutine);
            activeDashCoroutine = null;
        }

        if (agent != null) agent.enabled = false;

        core.CmdCancelAttack?.Invoke();
        StopActionRoutine();
        core.CmdStopMovement?.Invoke();
        core.SetMoveAnimations(0f, 0f);

        SoundManager.Instance.StopSpecialBGMWithFade();
        core.animator.Play("Phase1Dead");

        if (PhaseTransitionSound != null) SoundManager.Instance.PlaySound(PhaseTransitionSound);

        core.PlayEffects("PhaseTransition");

        yield return new WaitForSeconds(8f);

        foreach (var afterImage in AfterImages)
        {
            if (afterImage != null) afterImage.SetActive(true);
        }

        float elapsed = 0f;
        while (elapsed < 1f)
        {
            elapsed += Time.deltaTime;
            if (AfterImageMats.Length >= 3)
            {
                AfterImageMats[0].SetFloat("_Alpha", Mathf.Lerp(0, 5, elapsed));
                AfterImageMats[1].SetFloat("_Alpha", Mathf.Lerp(0, 0.666f, elapsed));
                AfterImageMats[2].SetFloat("_Alpha", Mathf.Lerp(0, 0.333f, elapsed));
            }
            yield return null;
        }

        yield return new WaitForSeconds(5f);

        if (BossMusic != null && BossMusic.Length > 1)
        {
            SoundManager.Instance.PlaySpecialBGM(BossMusic[1]);
        }

        yield return new WaitForSeconds(1f);
        elapsed = 0f;
        while (elapsed < 2f)
        {
            elapsed += Time.deltaTime;
            if (AfterImageMats.Length >= 3)
                core.CmdLookAt?.Invoke(Player.Instance.transform.position);

            yield return null;
        }

        if (agent != null)
        {
            SnapToGround();
            agent.enabled = true;
            core.CmdTrySnapToNavMesh?.Invoke();
        }

        isTransitioning = false;
        skillSelector.SetPhase(2);
        isAttacking = false;

        selectedSkill = null;
        skillSelector.isInstantComboTriggered = false;
    }

    public void PhaseTransitionKnockback()
    {
        if (Player.Instance == null || Player.Instance.PlayerMove == null) return;

        Vector3 myPos = transform.position;
        Vector3 playerPos = Player.Instance.transform.position;

        Vector3 dirToPlayer = playerPos - myPos;
        dirToPlayer.y = 0f;

        if (dirToPlayer.sqrMagnitude < 0.001f) return;
        dirToPlayer.Normalize();

        Vector3 myForward = transform.forward;
        myForward.y = 0f;
        myForward.Normalize();

        float angle = Vector3.Angle(myForward, dirToPlayer);

        if (angle < 30f)
        {
            float knockbackPower = 15f;
            float upwardPower = 2f;

            Vector3 knockbackForce = (dirToPlayer * knockbackPower) + (Vector3.up * upwardPower);
            Player.Instance.PlayerMove.KnockBack(knockbackForce, 1, 1.5f);
        }
    }

    private bool CheckPlayerInSkillBox(SkillBase skill)
    {
        if (Player.Instance == null) return false;

        if (skill is Skill_Melee m && m.hitDataList != null && m.hitDataList.Count > 0)
        {
            var h = m.hitDataList[0];
            Vector3 worldCenter = transform.position + (transform.rotation * h.centerOffset);
            Quaternion worldRotation = transform.rotation * Quaternion.Euler(h.boxRotation);

            Vector3 playerPos = Player.Instance.transform.position;
            worldCenter.y = 0;
            playerPos.y = 0;

            Vector3 localPos = Quaternion.Inverse(worldRotation) * (playerPos - worldCenter);
            Vector3 extents = (h.boxSize * 0.9f) / 2f;

            bool inBox = Mathf.Abs(localPos.x) <= (extents.x + 0.5f) &&
                         Mathf.Abs(localPos.z) <= (extents.z + 0.5f);

            return inBox;
        }
        else if (skill is Skill_Range r)
        {
            return Vector3.Distance(transform.position, Player.Instance.transform.position) <= r.range * 0.9f;
        }

        return Vector3.Distance(transform.position, Player.Instance.transform.position) <= 2.5f * 0.9f;
    }

    protected override void HandleBehaviorLogic(float distToLastPos)
    {
        if (isAttacking || isStepping || isTransitioning || isStartingFight || core.IsDead)
        {
            return;
        }

        if (isMemorizingPlayer)
        {
            if (selectedSkill == null)
            {
                selectedSkill = skillSelector.GetNextSkill(distToLastPos);
                skillHoldTimer = 0f;
            }
            else
            {
                skillHoldTimer += Time.deltaTime;
                if (skillHoldTimer >= skillHoldTimeout)
                {
                    selectedSkill = null;
                    return;
                }
            }

            if (selectedSkill == null)
            {
                if (distToLastPos > 5f) UpdateChaseTarget(1f);
                else HandleCooldownWalk(2.5f);
                return;
            }

            float executeRange = GetSkillRange(selectedSkill) * skillRangeMultiplier;
            float evadeRange = executeRange * evasionTriggerFactor;
            bool canAttackGlobal = isDetectedPlayer && (Time.time >= nextAttackTime || skillSelector.isNextSkillCombo);
            bool hasEnoughMP = core.QueryHasEnoughMP?.Invoke(selectedSkill.MP_cost) ?? false;

            bool isWithinAttackBox = CheckPlayerInSkillBox(selectedSkill);

            if (isWithinAttackBox)
            {
                if (canAttackGlobal && hasEnoughMP)
                {
                    actionCoroutine = StartCoroutine(ExecuteSkillAndChain(selectedSkill));
                    lastActionWasEvasion = false;
                }
                else
                {
                    if (distToLastPos < evadeRange && !lastActionWasEvasion && !skillSelector.HasPendingCombo && UnityEngine.Random.value <= evasionProbability)
                    {
                        if (UnityEngine.Random.Range(0f, 100f) < strafeOnCloseChance)
                            actionCoroutine = StartCoroutine(StrafeRoutine(UnityEngine.Random.Range(strafeMinDuration, strafeMaxDuration)));
                        else
                        {
                            actionCoroutine = StartCoroutine(ExecuteEvasionAfterAttack());
                        }
                    }
                    else
                    {
                        actionCoroutine = StartCoroutine(StrafeRoutine(UnityEngine.Random.Range(strafeMinDuration, strafeMaxDuration)));
                        lastActionWasEvasion = false;
                    }
                }
            }
            else
            {
                if (distToLastPos < evadeRange)
                {
                    if (UnityEngine.Random.Range(0f, 100f) < strafeOnCloseChance || lastActionWasEvasion)
                    {
                        actionCoroutine = StartCoroutine(StrafeRoutine(UnityEngine.Random.Range(strafeMinDuration, strafeMaxDuration)));
                        lastActionWasEvasion = false;
                    }
                    else
                    {
                        actionCoroutine = StartCoroutine(ExecuteEvasionAfterAttack());
                    }
                }
                else if (distToLastPos <= executeRange)
                {
                    if (canAttackGlobal && hasEnoughMP)
                    {
                        actionCoroutine = StartCoroutine(AdjustPositionRoutine(selectedSkill));
                        lastActionWasEvasion = false;
                    }
                    else
                    {
                        actionCoroutine = StartCoroutine(StrafeRoutine(UnityEngine.Random.Range(strafeMinDuration, strafeMaxDuration)));
                        lastActionWasEvasion = false;
                    }
                }
                else
                {
                    if (canAttackGlobal && hasEnoughMP)
                    {
                        if (distToLastPos <= executeRange * walkApproachFactor) UpdateWalkApproachTarget(executeRange * 0.8f);
                        else UpdateChaseTarget(executeRange * 0.8f);
                    }
                    else
                    {
                        if (UnityEngine.Random.Range(0f, 100f) < idleStrafeChance && Time.time > nextWanderTime)
                        {
                            actionCoroutine = StartCoroutine(StrafeRoutine(UnityEngine.Random.Range(strafeMinDuration, strafeMaxDuration)));
                            nextWanderTime = Time.time + UnityEngine.Random.Range(closeWanderMin, closeWanderMax);
                            lastActionWasEvasion = false;
                            return;
                        }

                        float baseThreshold = executeRange * walkApproachFactor;
                        float currentThreshold = isRunningForward ? (baseThreshold - chaseBuffer) : (baseThreshold + chaseBuffer);
                        bool shouldRun = distToLastPos > currentThreshold;

                        if (shouldRun != isRunningForward && Time.time >= nextStateChangeTime)
                            nextStateChangeTime = Time.time + stateChangeDelay;
                        else if (Time.time < nextStateChangeTime)
                            shouldRun = isRunningForward;

                        if (shouldRun) UpdateChaseTarget(executeRange * 0.8f);
                        else HandleCooldownWalk(skillSelector.GetOptimalDistance(selectedSkill));

                        lastActionWasEvasion = false;
                    }
                }
            }
        }
        else
        {
            core.ChangeState(EnemyState.Idle);
            core.CmdStopMovement?.Invoke();
            core.SetMoveAnimations(0f, 0f);

            if (BossMusic != null && BossMusic.Length > 0 && !isDetectedPlayer)
            {
                SoundManager.Instance.StopSpecialBGMWithFade();
            }
        }
    }

    private void SnapToGround()
    {
        if (UnityEngine.AI.NavMesh.SamplePosition(transform.position, out UnityEngine.AI.NavMeshHit hit, 2f, UnityEngine.AI.NavMesh.AllAreas))
        {
            Vector3 pos = transform.position;
            pos.y = hit.position.y;
            transform.position = pos;
            if (agent != null && agent.enabled) agent.nextPosition = pos;
        }
    }

    protected override IEnumerator RouteAndExecuteSkill(SkillBase skill)
    {
        if (leftStepSkills.Contains(skill)) yield return StartCoroutine(StepAttackSequence(skill, "Step_Left", sideStepDuration));
        else if (rightStepSkills.Contains(skill)) yield return StartCoroutine(StepAttackSequence(skill, "Step_Right", sideStepDuration));
        else if (preBackStepSkills.Contains(skill)) yield return StartCoroutine(StepAttackSequence(skill, "Step_Back", attackBackStepDuration));
        else yield return StartCoroutine(PerformAttackSequence(skill));
    }

    private IEnumerator StepAttackSequence(SkillBase skill, string stepAnimName, float stepDuration)
    {
        if (skill == null || core.IsDead) yield break;

        isAttacking = true;
        isStepping = true;
        yield return null;

        if (core.IsDead) yield break;

        skillSelector.isInstantComboTriggered = false;
        core.CmdStopMovement?.Invoke();

        if (stepAnimName == "Step_Back") SoundManager.Instance.PlaySound(BackStepSound);
        else SoundManager.Instance.PlaySound(StepSound);

        core.animator.CrossFade(stepAnimName, 0.1f);

        float elapsed = 0f;
        while (elapsed < stepDuration)
        {
            if (core.IsDead) yield break;
            elapsed += Time.deltaTime;
            if (Player.Instance != null)
            {
                core.CmdLookAt?.Invoke(Player.Instance.transform.position);
                SnapToGround();
            }
            yield return null;
        }

        if (core.IsDead) yield break;

        if (!skillSelector.isInstantComboTriggered) core.CmdTrySnapToNavMesh?.Invoke();
        isStepping = false;

        // 💡 라우팅 구조 덕분에 부모의 안전한 공격 시퀀스를 그대로 연결하여 실행
        yield return StartCoroutine(PerformAttackSequence(skill));
    }

    protected override IEnumerator PerformAttackSequence(SkillBase skill)
    {
        bool isPushAttack = PushSkill != null && PushSkill == skill;
        bool UseAnimRoot = skill.name.Contains("Attack7") || skill.name.Contains("Attack9") || skill.name.Contains("Attack10");

        if (isPushAttack) core.CmdSetIgnorePlayerCollision?.Invoke(false);

        if (agent != null)
        {
            if (UseAnimRoot) agent.enabled = false;
            else if (agent.enabled)
            {
                agent.updateRotation = false;
                agent.velocity = Vector3.zero;
            }
        }

        if (skill.name.Contains("Attack9") || skill.name.Contains("Attack10"))
        {
            activeDashCoroutine = StartCoroutine(Attack9DashRoutine());
        }

        // 부모의 공격 시퀀스 호출 (try-finally 보장)
        yield return StartCoroutine(base.PerformAttackSequence(skill));

        if (activeDashCoroutine != null)
        {
            StopCoroutine(activeDashCoroutine);
            activeDashCoroutine = null;
        }

        if (isPushAttack) core.CmdSetIgnorePlayerCollision?.Invoke(true);

        if (UseAnimRoot)
        {
            if (agent != null && !agent.enabled)
            {
                SnapToGround();
                agent.enabled = true;
            }
            if (!skillSelector.isInstantComboTriggered) core.CmdTrySnapToNavMesh?.Invoke();
        }
    }

    protected override void StopAttackEffects(SkillBase skill)
    {
        bool isEarthquake = earthquakeSkills != null && earthquakeSkills.Contains(skill);
        float stopDelay = isEarthquake ? earthquakeEffectDelay : 0f;
        core.StopEffects(skill.name, stopDelay);
        LeftTrail(0);
        RightTrail(0);
    }

    protected override IEnumerator ExecuteEvasionAfterAttack()
    {
        yield return StartCoroutine(EvasiveBackStepSequence());
        lastActionWasEvasion = true;
    }

    private IEnumerator EvasiveBackStepSequence()
    {
        isStepping = true;
        lastEvasionTime = Time.time;
        core.CmdStopMovement?.Invoke();
        core.ChangeState(EnemyState.BackWalk);
        core.SetMoveAnimations(0f, 0f, 0f);
        SoundManager.Instance.PlaySound(BackStepSound);

        core.animator.CrossFade("Step_Back", 0.1f);

        float elapsed = 0f;
        while (elapsed < evasionDuration)
        {
            if (core.IsDead) yield break;

            elapsed += Time.deltaTime;
            core.CmdLookAt?.Invoke(lastKnownPlayerPos);
            SnapToGround();
            yield return null;
        }

        if (core.IsDead) yield break;

        core.CmdTrySnapToNavMesh?.Invoke();
        core.SetMoveAnimations(0f, 0f);
        isStepping = false;

        if (!core.IsDead) core.ChangeState(EnemyState.Idle);
        actionCoroutine = null;
    }

    private IEnumerator Attack9DashRoutine()
    {
        float delayElapsed = 0f;
        while (delayElapsed < attack9DashDelay)
        {
            if (core.IsDead) yield break;
            delayElapsed += Time.deltaTime;

            if (Player.Instance != null)
            {
                Vector3 targetDir = Player.Instance.transform.position - transform.position;
                targetDir.y = 0;

                if (targetDir.sqrMagnitude > 0.001f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(targetDir.normalized);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 15f);
                }
            }
            yield return null;
        }

        if (core.IsDead) yield break;

        Vector3 startPos = transform.position;
        Vector3 playerPos = Player.Instance.transform.position;

        Vector3 forwardDir = transform.forward;
        forwardDir.y = 0f;
        if (forwardDir.sqrMagnitude > 0.001f) forwardDir.Normalize();
        else forwardDir = transform.forward;

        float distanceToPlayer = Vector3.Distance(new Vector3(startPos.x, 0, startPos.z), new Vector3(playerPos.x, 0, playerPos.z));
        float stopOffset = 1.5f;
        float targetDashDistance = Mathf.Max(0f, distanceToPlayer - stopOffset);

        float elapsed = 0f;
        while (elapsed < attack9DashDuration)
        {
            if (core.IsDead || !isAttacking) yield break;

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / attack9DashDuration);
            float curveValue = attack9DashCurve.Evaluate(t);

            Vector3 nextPos = startPos + (forwardDir * (targetDashDistance * curveValue));
            Vector3 moveDir = (nextPos - transform.position).normalized;
            float moveDist = Vector3.Distance(transform.position, nextPos);

            if (moveDist > 0.001f && Physics.SphereCast(transform.position + Vector3.up, 0.3f, moveDir, out RaycastHit hit, moveDist, wallLayer))
                break;

            transform.position = nextPos;
            yield return null;
        }
    }

    private void UpdateChaseTarget(float stopDist = 0f)
    {
        if (agent != null)
        {
            agent.updateRotation = false;
            if (agent.enabled) agent.isStopped = false;
        }

        core.ChangeState(EnemyState.Chase);
        core.SetMoveAnimations(0f, 1f, 2f);
        isRunningForward = true;
        core.CmdLookAt?.Invoke(lastKnownPlayerPos);

        if (Time.time >= nextTargetUpdateTime)
        {
            nextTargetUpdateTime = Time.time + targetUpdateInterval;

            Vector3 targetPos = lastKnownPlayerPos;
            if (playerVelocity.sqrMagnitude > 1f)
            {
                float dist = Vector3.Distance(transform.position, targetPos);
                float predictionTime = Mathf.Clamp(dist / (core.BaseMovementSpeed * chaseSpeedMultiplier), 0f, maxPredictionTime);
                targetPos += new Vector3(playerVelocity.x, 0f, playerVelocity.z) * predictionTime;
            }

            core.CmdMoveTo?.Invoke(targetPos, chaseSpeedMultiplier);
        }
    }

    private void UpdateWalkApproachTarget(float stopDist = 0f)
    {
        if (agent != null)
        {
            agent.updateRotation = false;
            if (agent.enabled) agent.isStopped = false;
        }

        core.ChangeState(EnemyState.Chase);
        core.SetMoveAnimations(0f, 1f, 1f);
        isRunningForward = false;
        core.CmdLookAt?.Invoke(lastKnownPlayerPos);

        if (Time.time >= nextTargetUpdateTime)
        {
            nextTargetUpdateTime = Time.time + targetUpdateInterval;
            core.CmdMoveTo?.Invoke(lastKnownPlayerPos, 1f);
        }
    }

    private void HandleCooldownWalk(float optimalDistance)
    {
        if (agent != null)
        {
            agent.updateRotation = false;
            if (agent.enabled) agent.isStopped = false;
        }

        core.ChangeState(EnemyState.CooldownWalk);
        core.SetMoveAnimations(0f, 1f, 1f);
        isRunningForward = false;

        bool isPathPending = core.QueryIsPathPending?.Invoke() ?? false;
        core.CmdLookAt?.Invoke(Player.Instance.transform.position);

        if (Time.time > nextWanderTime || !isPathPending)
        {
            Vector3 dirToMe = (transform.position - lastKnownPlayerPos).normalized;
            Vector3 randomOffset = UnityEngine.Random.insideUnitSphere * 2f;
            randomOffset.y = 0;

            cooldownWanderTarget = lastKnownPlayerPos + (dirToMe * optimalDistance) + randomOffset;
            nextWanderTime = Time.time + UnityEngine.Random.Range(closeWanderMin, closeWanderMax);
            core.CmdMoveTo?.Invoke(cooldownWanderTarget, 1f);
        }
    }

    private IEnumerator AdjustPositionRoutine(SkillBase skill)
    {
        isStepping = true;
        float elapsed = 0f;

        core.CmdStopMovement?.Invoke();

        float optimalDist = 2.5f;
        float boxDepth = 1.0f;

        if (skill is Skill_Melee m && m.hitDataList.Count > 0)
        {
            optimalDist = Mathf.Abs(m.hitDataList[0].centerOffset.z / 2f);
            boxDepth = m.hitDataList[0].boxSize.z / 2f;
        }
        else if (skill is Skill_Range r)
        {
            optimalDist = r.range * 0.8f;
            boxDepth = r.range * 0.2f;
        }

        float minDist = optimalDist - boxDepth;
        float maxDist = optimalDist + boxDepth;

        if (agent != null) agent.updateRotation = false;

        while (elapsed < 1.5f)
        {
            if (core.IsDead) yield break;
            if (CheckPlayerInSkillBox(skill)) break;

            Vector3 myPos = transform.position;
            Vector3 playerPosPlane = new Vector3(lastKnownPlayerPos.x, myPos.y, lastKnownPlayerPos.z);
            float currentDist = Vector3.Distance(myPos, playerPosPlane);

            elapsed += Time.deltaTime;
            core.CmdLookAt?.Invoke(lastKnownPlayerPos);

            if (currentDist < minDist)
            {
                core.ChangeState(EnemyState.BackWalk);
                core.SetMoveAnimations(0f, -1f, 1f);
                Vector3 dirFromPlayer = (myPos - playerPosPlane).normalized;
                core.CmdMoveDirection?.Invoke(dirFromPlayer, 1f);
            }
            else if (currentDist > maxDist)
            {
                core.ChangeState(EnemyState.Chase);
                core.SetMoveAnimations(0f, 1f, 1f);
                Vector3 dirToPlayer = (playerPosPlane - myPos).normalized;
                core.CmdMoveDirection?.Invoke(dirToPlayer, 1f);
            }
            else
            {
                core.ChangeState(EnemyState.Idle);
                core.CmdStopMovement?.Invoke();
                core.SetMoveAnimations(0f, 0f, 0f);
            }

            SnapToGround();
            yield return null;
        }

        if (core.IsDead) yield break;

        core.CmdStopMovement?.Invoke();
        isStepping = false;
        actionCoroutine = null;

        if (!CheckPlayerInSkillBox(skill))
        {
            selectedSkill = null;
        }
    }

    public void LeftTrail(int status)
    {
        if (leftTrail != null) leftTrail.emitting = status != 0;
    }

    public void RightTrail(int status)
    {
        if (rightTrail != null) rightTrail.emitting = status != 0;
    }

    public void TriggerAirborneHit(SkillBase skill, int value)
    {
        if (skill.name == "Attack7_1" && value == 0)
            Player.Instance.PlayerMove.TriggerAirborneHit();
    }
}