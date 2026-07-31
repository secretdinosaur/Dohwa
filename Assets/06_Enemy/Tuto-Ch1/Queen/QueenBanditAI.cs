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

    [Header("Start Fight 연출")]
    public GameObject FightWall;
    public float startFightDelay = 2.0f;

    [Header("Behavior Tuning")]
    public float walkApproachFactor = 1.5f;
    public float chaseBuffer = 1.0f;
    public float closeWanderMin = 1.5f;
    public float closeWanderMax = 3.5f;

    [Header("Melee Behavior Adjustments")]
    public float parriedBackStepChance = 60f;
    public float normalBackStepChance = 20f;
    [Range(0f, 100f)] public float strafeOnCloseChance = 30f;
    private bool lastActionWasBackStep = false;

    [Header("Special Skill Logic (Step Before Attack)")]
    public SkillBase[] leftStepSkills;
    public SkillBase[] rightStepSkills;
    public SkillBase[] preBackStepSkills;

    [Header("Delayed Effect Skills")]
    public SkillBase[] earthquakeSkills;
    public float earthquakeEffectDelay = 10f;

    public SkillBase PushSkill;

    [Header("Step Tuning")]
    public float sideStepDistance = 4.5f;
    public float sideStepDuration = 0.5f;
    public float attackBackStepDistance = 3f;
    public float attackBackStepDuration = 0.3f;
    private UnityEngine.AI.NavMeshAgent agent;

    [Header("Evasive BackStep")]
    public float evasionTriggerFactor = 0.5f;
    public float evasionDistance = 4.5f;
    public float evasionDuration = 0.3f;
    public float evasionCooldown = 5f;
    public float evasionProbability = 0.7f;

    [Header("Attack 9 Dash Settings")]
    public float attack9DashDuration = 0.3f;
    public float attack9DashDelay = 0.2f;
    public AnimationCurve attack9DashCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    private SkillSelector skillSelector;
    private float nextWanderTime = 0f;
    private Vector3 cooldownWanderTarget;

    private float lastEvasionTime = -9999f;
    private bool isStepping = false;
    private bool isTransitioning = false;
    private bool isStartingFight = false;

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
    }

    private void HandleRevived()
    {
        skillSelector.SetPhase(1);
        isTransitioning = false;
        isStartingFight = false;

        isAttacking = false;
        isStepping = false;
        isMemorizingPlayer = false;
        lastActionWasBackStep = false;
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
        StartCoroutine(StartFightSequence());
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
        core.CmdCancelAttack?.Invoke();
        StopActionRoutine();
        core.CmdStopMovement?.Invoke();
        core.SetMoveAnimations(0f, 0f);
        UIManager.Instance.BossStatus.SetActive(false);
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

        UIManager.Instance.BossStatus.SetActive(true);

        yield return new WaitForSeconds(5f);

        if (BossMusic != null && BossMusic.Length > 1)
        {
            SoundManager.Instance.PlaySpecialBGM(BossMusic[1]);
        }
        yield return new WaitForSeconds(3f);

        isTransitioning = false;
        skillSelector.SetPhase(2);
        isAttacking = false;
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
        if (isAttacking || isStepping || isTransitioning || isStartingFight || core.IsDead) return;
        if (core.currentState == EnemyState.BackWalk || core.currentState == EnemyState.CooldownWalk || core.currentState == EnemyState.Attack) return;

        if (isMemorizingPlayer)
        {
            StopActionRoutine();

            if (selectedSkill == null)
            {
                selectedSkill = skillSelector.GetNextSkill(distToLastPos);
            }

            if (selectedSkill == null)
            {
                if (distToLastPos > 5f) UpdateChaseTarget(1f);
                else HandleCooldownWalk(skillSelector.GetOptimalDistance(null));
                return;
            }

            float executeRange = GetSkillRange(selectedSkill) * skillRangeMultiplier;
            float evadeRange = executeRange * evasionTriggerFactor;
            bool canAttackGlobal = isDetectedPlayer && (Time.time >= nextAttackTime || skillSelector.isNextSkillCombo);
            bool hasEnoughMP = core.QueryHasEnoughMP?.Invoke(selectedSkill.MP_cost) ?? false;
            bool isWithinAttackBox = CheckPlayerInSkillBox(selectedSkill);

            if (distToLastPos <= executeRange)
            {
                if (distToLastPos < evadeRange && !lastActionWasBackStep && !skillSelector.HasPendingCombo && UnityEngine.Random.value <= evasionProbability)
                {
                    if (UnityEngine.Random.Range(0f, 100f) < strafeOnCloseChance)
                    {
                        actionCoroutine = StartCoroutine(StrafeRoutine(UnityEngine.Random.Range(0.5f, 1.0f), 1f));
                    }
                    else
                    {
                        actionCoroutine = StartCoroutine(EvasiveBackStepSequence());
                    }
                    lastActionWasBackStep = true;
                }
                else if (canAttackGlobal && hasEnoughMP)
                {
                    if (isWithinAttackBox)
                    {
                        if (leftStepSkills.Contains(selectedSkill))
                            actionCoroutine = StartCoroutine(StepAttackSequence(selectedSkill, sideStepDistance, -35f, "leftStep", sideStepDuration));
                        else if (rightStepSkills.Contains(selectedSkill))
                            actionCoroutine = StartCoroutine(StepAttackSequence(selectedSkill, sideStepDistance, 35f, "rightStep", sideStepDuration));
                        else if (preBackStepSkills.Contains(selectedSkill))
                            actionCoroutine = StartCoroutine(StepAttackSequence(selectedSkill, attackBackStepDistance, 180f, "backStep", attackBackStepDuration));
                        else
                            actionCoroutine = StartCoroutine(AttackSequence(selectedSkill));

                        lastActionWasBackStep = false;
                    }
                    else
                    {
                        float optimalDist = 2.5f;
                        float boxDepth = 1.0f;

                        if (selectedSkill is Skill_Melee m && m.hitDataList.Count > 0)
                        {
                            optimalDist = Mathf.Abs(m.hitDataList[0].centerOffset.z);
                            boxDepth = m.hitDataList[0].boxSize.z / 2f;
                        }
                        else if (selectedSkill is Skill_Range r)
                        {
                            optimalDist = r.range * 0.8f;
                            boxDepth = r.range * 0.2f;
                        }

                        float minDist = optimalDist - boxDepth;
                        float maxDist = optimalDist + boxDepth;

                        if (distToLastPos < minDist)
                        {
                            // 💡 뒷걸음(BackWalk) 대신 뒷스텝(EvasiveBackStepSequence) 사용
                            actionCoroutine = StartCoroutine(EvasiveBackStepSequence());
                            lastActionWasBackStep = true;
                        }
                        else if (distToLastPos > maxDist)
                        {
                            if (distToLastPos <= executeRange * walkApproachFactor)
                                UpdateWalkApproachTarget(optimalDist);
                            else
                                UpdateChaseTarget(optimalDist);

                            lastActionWasBackStep = false;
                        }
                        else
                        {
                            actionCoroutine = StartCoroutine(StrafeRoutine(UnityEngine.Random.Range(0.5f, 1.0f), 1f));
                            lastActionWasBackStep = false;
                        }
                    }
                }
                else
                {
                    if (distToLastPos < evadeRange)
                    {
                        if (UnityEngine.Random.Range(0f, 100f) < strafeOnCloseChance)
                            actionCoroutine = StartCoroutine(StrafeRoutine(UnityEngine.Random.Range(0.5f, 1.0f), 1f));
                        else
                            actionCoroutine = StartCoroutine(EvasiveBackStepSequence());

                        lastActionWasBackStep = true;
                    }
                    else
                    {
                        actionCoroutine = StartCoroutine(StrafeRoutine(UnityEngine.Random.Range(0.5f, 1.0f), 1f));
                        lastActionWasBackStep = false;
                    }
                }
            }
            else
            {
                float threshold = (core.currentState == EnemyState.Chase)
                    ? (executeRange * walkApproachFactor) + chaseBuffer
                    : (executeRange * walkApproachFactor);

                if (distToLastPos > threshold) UpdateChaseTarget(executeRange * 0.8f);
                else HandleCooldownWalk(skillSelector.GetOptimalDistance(selectedSkill));

                lastActionWasBackStep = false;
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

    private void UpdateChaseTarget(float stopDist)
    {
        if (agent != null) agent.updateRotation = false;
        core.ChangeState(EnemyState.Chase);
        core.SetMoveAnimations(0f, 1f, 1f);
        core.CmdLookAt?.Invoke(lastKnownPlayerPos);

        if (Time.time >= nextTargetUpdateTime)
        {
            nextTargetUpdateTime = Time.time + targetUpdateInterval;
            Vector3 dirFromPlayer = (transform.position - lastKnownPlayerPos).normalized;
            Vector3 targetPos = lastKnownPlayerPos + (dirFromPlayer * stopDist);
            core.CmdMoveTo?.Invoke(targetPos, chaseSpeedMultiplier);
        }
    }

    private void UpdateWalkApproachTarget(float stopDist)
    {
        if (agent != null) agent.updateRotation = false;
        core.ChangeState(EnemyState.Chase);
        core.SetMoveAnimations(0f, 1f);
        core.CmdLookAt?.Invoke(lastKnownPlayerPos);

        if (Time.time >= nextTargetUpdateTime)
        {
            nextTargetUpdateTime = Time.time + targetUpdateInterval;
            Vector3 dirFromPlayer = (transform.position - lastKnownPlayerPos).normalized;
            Vector3 targetPos = lastKnownPlayerPos + (dirFromPlayer * stopDist);
            core.CmdMoveTo?.Invoke(targetPos, 1f);
        }
    }

    private void HandleCooldownWalk(float optimalDistance)
    {
        if (agent != null) agent.updateRotation = false;

        core.ChangeState(EnemyState.CooldownWalk);
        core.SetMoveAnimations(0f, 1f);

        bool isPathPending = core.QueryIsPathPending?.Invoke() ?? false;
        core.CmdLookAt?.Invoke(Player.Instance.transform.position);

        if (Time.time > nextWanderTime || !isPathPending)
        {
            Vector3 dirToMe = (transform.position - lastKnownPlayerPos).normalized;
            cooldownWanderTarget = lastKnownPlayerPos + (dirToMe * optimalDistance);
            nextWanderTime = Time.time + UnityEngine.Random.Range(closeWanderMin, closeWanderMax);

            core.CmdMoveTo?.Invoke(cooldownWanderTarget, 1f);
        }
    }
    private IEnumerator StepAttackSequence(SkillBase skill, float distance, float angle, string stepAnimName, float stepDuration)
    {
        isAttacking = true;
        isStepping = true;

        yield return null;

        skillSelector.isInstantComboTriggered = false;
        core.CmdStopMovement?.Invoke();

        if (stepAnimName == "backStep") SoundManager.Instance.PlaySound(BackStepSound);
        else SoundManager.Instance.PlaySound(StepSound);

        core.animator.CrossFade(stepAnimName, 0.1f);

        Vector3 startPos = transform.position;
        Vector3 playerPos = Player.Instance.transform.position;

        Vector3 dirToMe = (startPos - playerPos).normalized;
        dirToMe.y = 0f;

        Quaternion rotation = Quaternion.Euler(0, angle, 0);
        Vector3 targetDir = rotation * dirToMe;

        Vector3 targetPos = playerPos + (targetDir * distance);
        targetPos.y = startPos.y;

        float elapsed = 0f;
        while (elapsed < stepDuration)
        {
            elapsed += Time.deltaTime;
            float smoothT = Mathf.SmoothStep(0f, 1f, elapsed / stepDuration);

            core.CmdLookAt?.Invoke(Player.Instance.transform.position);

            Vector3 nextPos = Vector3.Lerp(startPos, targetPos, smoothT);
            Vector3 moveDir = (nextPos - transform.position).normalized;
            float moveDist = Vector3.Distance(transform.position, nextPos);

            if (moveDist > 0.001f && Physics.SphereCast(transform.position + Vector3.up, 0.3f, moveDir, out RaycastHit hit, moveDist, wallLayer))
            {
                break;
            }

            transform.position = nextPos;
            yield return null;
        }

        if (!skillSelector.isInstantComboTriggered) core.CmdTrySnapToNavMesh?.Invoke();
        isStepping = false;

        core.ChangeState(EnemyState.Attack);
        skillSelector.isInstantComboTriggered = false;

        bool attackFinished = false;
        Action<float> onComplete = (nextTime) => {
            float delay = skillSelector.GetLastSkillDelay();
            nextAttackTime = Time.time + delay;
            attackFinished = true;
        };

        core.OnAttackCompleted += onComplete;
        core.CmdPerformAttack?.Invoke(skill);

        float clipLength = (skill.clip != null) ? skill.clip.length : 2f;
        float timeout = Time.time + clipLength + 0.2f;

        yield return new WaitUntil(() => attackFinished || skillSelector.isInstantComboTriggered || Time.time > timeout);

        core.OnAttackCompleted -= onComplete;

        if (skillSelector.isInstantComboTriggered) skillSelector.isInstantComboTriggered = false;

        // 💡 공격 종료 후 MeleeEnemyAI 와 동일하게 확률적 뒷스텝
        float chance = core.wasParried ? parriedBackStepChance : normalBackStepChance;
        core.wasParried = false;

        if (UnityEngine.Random.Range(0f, 100f) < chance && !skillSelector.HasPendingCombo)
        {
            yield return StartCoroutine(EvasiveBackStepSequence());
            lastActionWasBackStep = true;
        }

        ChainNextAttackOrIdle();
    }

    private IEnumerator EvasiveBackStepSequence()
    {
        isStepping = true;
        lastEvasionTime = Time.time;

        core.CmdStopMovement?.Invoke();
        core.ChangeState(EnemyState.BackWalk);
        core.SetMoveAnimations(0f, -1f);
        SoundManager.Instance.PlaySound(BackStepSound);
        Vector3 startPos = transform.position;
        Vector3 backDir = (-transform.forward).normalized;
        backDir.y = 0f;

        Vector3 targetPos = startPos + (backDir * evasionDistance);

        float elapsed = 0f;
        while (elapsed < evasionDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / evasionDuration;
            float smoothT = 1f - Mathf.Pow(1f - t, 3f);

            Vector3 nextPos = Vector3.Lerp(startPos, targetPos, smoothT);

            Vector3 moveDir = (nextPos - transform.position).normalized;
            float moveDist = Vector3.Distance(transform.position, nextPos);

            if (moveDist > 0.001f && Physics.SphereCast(transform.position + Vector3.up, 0.3f, moveDir, out _, moveDist, wallLayer))
            {
                break;
            }

            transform.position = nextPos;
            yield return null;
        }

        core.CmdTrySnapToNavMesh?.Invoke();
        core.SetMoveAnimations(0f, 0f);

        isStepping = false;
        core.ChangeState(EnemyState.Idle);
        actionCoroutine = null;
    }

    private IEnumerator AttackSequence(SkillBase skill)
    {
        isAttacking = true;

        yield return null;

        skillSelector.isInstantComboTriggered = false;

        core.CmdStopMovement?.Invoke();
        core.ChangeState(EnemyState.Attack);

        bool isPushAttack = PushSkill != null && PushSkill == skill;
        if (isPushAttack)
        {
            core.CmdSetIgnorePlayerCollision?.Invoke(false);
        }

        bool UseAnimRoot = skill.name.Contains("Attack7") || skill.name.Contains("Attack9");
        if (UseAnimRoot && agent != null)
        {
            agent.enabled = false;
        }

        Coroutine dashCoroutine = null;
        if (skill.name.Contains("Attack9"))
        {
            dashCoroutine = StartCoroutine(Attack9DashRoutine());
        }

        bool attackFinished = false;

        Action<float> onComplete = (nextTime) => {
            float delay = skillSelector.GetLastSkillDelay();
            nextAttackTime = Time.time + delay;
            attackFinished = true;
        };

        core.OnAttackCompleted += onComplete;

        core.CmdPerformAttack?.Invoke(skill);

        float clipLength = (skill.clip != null) ? skill.clip.length : 2f;
        float timeout = Time.time + clipLength + 0.1f;

        yield return new WaitUntil(() => attackFinished || skillSelector.isInstantComboTriggered || Time.time > timeout);

        core.OnAttackCompleted -= onComplete;

        if (dashCoroutine != null)
        {
            StopCoroutine(dashCoroutine);
        }

        if (isPushAttack)
        {
            core.CmdSetIgnorePlayerCollision?.Invoke(true);
        }

        if (UseAnimRoot)
        {
            if (agent != null) agent.enabled = true;
            if (!skillSelector.isInstantComboTriggered)
            {
                core.CmdTrySnapToNavMesh?.Invoke();
            }
        }

        bool isEarthquake = earthquakeSkills != null && earthquakeSkills.Contains(skill);
        float stopDelay = isEarthquake ? earthquakeEffectDelay : 0f;
        core.StopEffects(skill.name, stopDelay);

        if (skillSelector.isInstantComboTriggered) skillSelector.isInstantComboTriggered = false;

        // 💡 공격 종료 후 MeleeEnemyAI 와 동일하게 확률적 뒷스텝
        float chance = core.wasParried ? parriedBackStepChance : normalBackStepChance;
        core.wasParried = false;

        if (UnityEngine.Random.Range(0f, 100f) < chance && !skillSelector.HasPendingCombo)
        {
            yield return StartCoroutine(EvasiveBackStepSequence());
            lastActionWasBackStep = true;
        }

        ChainNextAttackOrIdle();
    }

    private IEnumerator Attack9DashRoutine()
    {
        yield return new WaitForSeconds(attack9DashDelay);

        Vector3 startPos = transform.position;
        Vector3 playerPos = Player.Instance.transform.position;

        Vector3 dirToPlayer = (playerPos - startPos);
        dirToPlayer.y = 0;
        if (dirToPlayer.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(dirToPlayer.normalized);
        }

        Vector3 forwardDir = transform.forward;
        forwardDir.y = 0f;

        if (forwardDir.sqrMagnitude > 0.001f)
            forwardDir.Normalize();
        else
            forwardDir = transform.forward;

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
            {
                break;
            }

            transform.position = nextPos;
            yield return null;
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