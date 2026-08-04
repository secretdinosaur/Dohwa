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
    public float startFightDelay = 2.0f;

    [Header("Behavior Tuning")]
    public float walkApproachFactor = 1.5f;
    public float chaseBuffer = 1.0f;
    public float closeWanderMin = 1.5f;
    public float closeWanderMax = 3.5f;
    public float stateChangeDelay = 2.0f;

    [Header("Melee Behavior Adjustments")]
    public float parriedBackStepChance = 60f;
    public float normalBackStepChance = 20f;
    [Range(0f, 100f)] public float strafeOnCloseChance = 30f;

    [Header("Strafe & Step Adjustments")]
    [Range(0f, 100f)] public float idleStrafeChance = 15f;

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

    private SkillSelector skillSelector;
    private float nextWanderTime = 0f;
    private Vector3 cooldownWanderTarget;

    private float lastEvasionTime = -9999f;
    private bool isStepping = false;
    private bool isTransitioning = false;
    private bool isStartingFight = false;
    private float currentStrafeDir = 1f;

    private float nextStateChangeTime = 0f;

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
        if (agent != null && !agent.enabled)
        {
            agent.enabled = true;
            core.CmdTrySnapToNavMesh?.Invoke();
        }

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

        if (agent != null)
        {
            agent.enabled = false;
        }

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
            agent.enabled = true;
            core.CmdTrySnapToNavMesh?.Invoke();
        }

        isTransitioning = false;
        skillSelector.SetPhase(2);
        isAttacking = false;
    }

    public void PhaseTransitionKnockback()
    {
        if (Player.Instance == null || Player.Instance.PlayerMove == null) return;

        Vector3 myPos = transform.position;
        Vector3 playerPos = Player.Instance.transform.position;

        // Y축 높이 차이 무시 (평면 기준 각도 계산)
        Vector3 dirToPlayer = playerPos - myPos;
        dirToPlayer.y = 0f;

        if (dirToPlayer.sqrMagnitude < 0.001f) return;
        dirToPlayer.Normalize();

        Vector3 myForward = transform.forward;
        myForward.y = 0f;
        myForward.Normalize();

        // 내 정면과 플레이어 사이의 각도 계산
        float angle = Vector3.Angle(myForward, dirToPlayer);

        // 각도가 30도 미만(좌우 합쳐서 60도 부채꼴 범위)일 경우 넉백 실행
        if (angle < 30f)
        {
            float knockbackPower = 15f; // 밀려나는 힘 (필요에 따라 조절)
            float upwardPower = 2f;     // 살짝 띄우는 힘

            Vector3 knockbackForce = (dirToPlayer * knockbackPower) + (Vector3.up * upwardPower);

            // 앞서 수정했던 넉다운 로직을 활용 (Type: 1, Duration: 1.5초)
            Player.Instance.PlayerMove.KnockBack(knockbackForce, 1, 1.5f);

            // 원한다면 데미지를 주거나 효과음을 재생하는 코드를 추가할 수 있습니다.
            // Player.Instance.TakeArbitraryDamage(10);
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
            }

            if (selectedSkill == null)
            {
                if (distToLastPos > 5f)
                {
                    UpdateChaseTarget(1f);
                }
                else
                {
                    HandleCooldownWalk(skillSelector.GetOptimalDistance(null));
                }
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
                        actionCoroutine = StartCoroutine(StrafeRoutine(UnityEngine.Random.Range(strafeMinDuration, strafeMaxDuration)));
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
                            actionCoroutine = StartCoroutine(StepAttackSequence(selectedSkill, "Step_Left", sideStepDuration));
                        else if (rightStepSkills.Contains(selectedSkill))
                            actionCoroutine = StartCoroutine(StepAttackSequence(selectedSkill, "Step_Right", sideStepDuration));
                        else if (preBackStepSkills.Contains(selectedSkill))
                            actionCoroutine = StartCoroutine(StepAttackSequence(selectedSkill, "Step_Back", attackBackStepDuration));
                        else
                            actionCoroutine = StartCoroutine(AttackSequence(selectedSkill));

                        lastActionWasBackStep = false;
                    }
                    else
                    {
                        actionCoroutine = StartCoroutine(AdjustPositionRoutine(selectedSkill));
                    }
                }
                else
                {
                    if (distToLastPos < evadeRange)
                    {
                        if (UnityEngine.Random.Range(0f, 100f) < strafeOnCloseChance)
                        {
                            actionCoroutine = StartCoroutine(StrafeRoutine(UnityEngine.Random.Range(strafeMinDuration, strafeMaxDuration)));
                        }
                        else
                        {
                            actionCoroutine = StartCoroutine(EvasiveBackStepSequence());
                            lastActionWasBackStep = true;
                        }
                    }
                    else
                    {
                        actionCoroutine = StartCoroutine(StrafeRoutine(UnityEngine.Random.Range(strafeMinDuration, strafeMaxDuration)));
                        lastActionWasBackStep = false;
                    }
                }
            }
            else
            {
                if (UnityEngine.Random.Range(0f, 100f) < idleStrafeChance && Time.time > nextWanderTime)
                {
                    actionCoroutine = StartCoroutine(StrafeRoutine(UnityEngine.Random.Range(strafeMinDuration, strafeMaxDuration)));
                    nextWanderTime = Time.time + UnityEngine.Random.Range(closeWanderMin, closeWanderMax);
                    lastActionWasBackStep = false;
                    return;
                }

                float baseThreshold = executeRange * walkApproachFactor;
                float currentThreshold = isRunningForward ? (baseThreshold - chaseBuffer) : (baseThreshold + chaseBuffer);
                bool shouldRun = distToLastPos > currentThreshold;

                if (shouldRun != isRunningForward && Time.time >= nextStateChangeTime)
                {
                    nextStateChangeTime = Time.time + stateChangeDelay;
                }
                else if (Time.time < nextStateChangeTime)
                {
                    shouldRun = isRunningForward;
                }

                if (shouldRun)
                {
                    UpdateChaseTarget(executeRange * 0.8f);
                }
                else
                {
                    HandleCooldownWalk(skillSelector.GetOptimalDistance(selectedSkill));
                }

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

    private IEnumerator AdjustPositionRoutine(SkillBase skill)
    {
        isStepping = true;
        float elapsed = 0f;

        float optimalDist = 2.5f;
        float boxDepth = 1.0f;

        if (skill is Skill_Melee m && m.hitDataList.Count > 0)
        {
            optimalDist = Mathf.Abs(m.hitDataList[0].centerOffset.z);
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
            if (core.IsDead || CheckPlayerInSkillBox(skill))
            {
                break;
            }

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
                break;
            }

            yield return null;
        }

        core.CmdStopMovement?.Invoke();
        isStepping = false;
        actionCoroutine = null;
    }

    private IEnumerator StrafeRoutine(float duration)
    {
        isStepping = true;
        float elapsed = 0f;

        if (agent != null) agent.updateRotation = false;

        core.CmdStopMovement?.Invoke();
        core.ChangeState(EnemyState.Chase);

        currentStrafeDir = UnityEngine.Random.value > 0.5f ? 1f : -1f;
        float targetDist = Vector3.Distance(transform.position, lastKnownPlayerPos);

        core.SetMoveAnimations(currentStrafeDir, 0f, 1f);

        while (elapsed < duration)
        {
            if (core.IsDead || isTransitioning)
            {
                break;
            }

            elapsed += Time.deltaTime;

            Vector3 myPos = transform.position;
            Vector3 playerPosPlane = new Vector3(lastKnownPlayerPos.x, myPos.y, lastKnownPlayerPos.z);
            Vector3 dirToPlayer = (playerPosPlane - myPos).normalized;
            float currentDist = Vector3.Distance(myPos, playerPosPlane);

            float lookAngle = Mathf.Lerp(0f, 60f * currentStrafeDir, Mathf.Clamp01(elapsed / 0.25f));
            Vector3 lookDir = Quaternion.Euler(0, lookAngle, 0) * dirToPlayer;
            core.CmdLookAt?.Invoke(myPos + lookDir);

            Vector3 orbitDir = Vector3.Cross(Vector3.up, dirToPlayer).normalized * currentStrafeDir;

            if (Physics.SphereCast(myPos + (Vector3.up * 1.5f), 0.3f, orbitDir, out _, 0.5f, wallLayer))
            {
                currentStrafeDir *= -1f;
                orbitDir = Vector3.Cross(Vector3.up, dirToPlayer).normalized * currentStrafeDir;
                core.SetMoveAnimations(currentStrafeDir, 0f, 1f);
            }

            Vector3 finalMoveDir = orbitDir;

            if (elapsed < 0.25f)
            {
                targetDist = currentDist;
            }
            else
            {
                float distError = currentDist - targetDist;

                if (Mathf.Abs(distError) > 0.4f)
                {
                    float correctionForce = Mathf.Clamp(distError, -1f, 1f) * 0.5f;
                    finalMoveDir = (orbitDir + dirToPlayer * correctionForce).normalized;
                }
            }

            core.CmdMoveDirection?.Invoke(finalMoveDir, strafeSpeedMultiplier);

            yield return null;
        }

        core.CmdStopMovement?.Invoke();
        isStepping = false;
        core.ChangeState(EnemyState.Idle);
        actionCoroutine = null;
    }

    private void UpdateChaseTarget(float stopDist)
    {
        if (agent != null) agent.updateRotation = false;

        core.ChangeState(EnemyState.Chase);
        core.SetMoveAnimations(0f, 1f, 2f);

        isRunningForward = true;
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
        core.SetMoveAnimations(0f, 1f, 1f);

        isRunningForward = false;
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
        core.SetMoveAnimations(0f, 1f, 1f);

        isRunningForward = false;

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

    private void ChainNextAttackOrIdle()
    {
        if (skillSelector.HasPendingCombo)
        {
            float currentDist = Vector3.Distance(transform.position, Player.Instance.transform.position);
            selectedSkill = skillSelector.GetNextSkill(currentDist);

            if (selectedSkill == null)
            {
                isAttacking = false;
                core.ChangeState(EnemyState.Idle);
                actionCoroutine = null;
                return;
            }

            if (leftStepSkills.Contains(selectedSkill))
                actionCoroutine = StartCoroutine(StepAttackSequence(selectedSkill, "Step_Left", sideStepDuration));
            else if (rightStepSkills.Contains(selectedSkill))
                actionCoroutine = StartCoroutine(StepAttackSequence(selectedSkill, "Step_Right", sideStepDuration));
            else if (preBackStepSkills.Contains(selectedSkill))
                actionCoroutine = StartCoroutine(StepAttackSequence(selectedSkill, "Step_Back", attackBackStepDuration));
            else
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

    private IEnumerator StepAttackSequence(SkillBase skill, string stepAnimName, float stepDuration)
    {
        if (skill == null)
        {
            ChainNextAttackOrIdle();
            yield break;
        }

        isAttacking = true;
        isStepping = true;

        yield return null;

        skillSelector.isInstantComboTriggered = false;
        core.CmdStopMovement?.Invoke();

        if (stepAnimName == "Step_Back") SoundManager.Instance.PlaySound(BackStepSound);
        else SoundManager.Instance.PlaySound(StepSound);

        core.animator.CrossFade(stepAnimName, 0.1f);

        float optimalDist = 2.5f;
        if (skill is Skill_Melee m && m.hitDataList.Count > 0)
        {
            optimalDist = Mathf.Abs(m.hitDataList[0].centerOffset.z);
        }
        else if (skill is Skill_Range r)
        {
            optimalDist = r.range * 0.8f;
        }
        float elapsed = 0f;
        while (elapsed < stepDuration)
        {
            elapsed += Time.deltaTime;

            if (Player.Instance != null)
            {
                Vector3 playerPos = Player.Instance.transform.position;
                core.CmdLookAt?.Invoke(playerPos);

                Vector3 myPos = transform.position;
                Vector3 playerPosPlane = new Vector3(playerPos.x, myPos.y, playerPos.z);
                float currentDist = Vector3.Distance(myPos, playerPosPlane);

                Vector3 dirToPlayer = (playerPosPlane - myPos).normalized;
                Vector3 offset = dirToPlayer * (currentDist - optimalDist) * (Time.deltaTime * 10f);

                if (agent != null && agent.enabled)
                {
                    agent.Move(offset);
                }
                else
                {
                    transform.position += offset;
                }
            }
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
        core.SetMoveAnimations(0f, 0f, 0f);
        SoundManager.Instance.PlaySound(BackStepSound);

        core.animator.CrossFade("Step_Back", 0.1f);

        float elapsed = 0f;
        while (elapsed < evasionDuration)
        {
            elapsed += Time.deltaTime;
            core.CmdLookAt?.Invoke(lastKnownPlayerPos);
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
        if (skill == null)
        {
            ChainNextAttackOrIdle();
            yield break;
        }

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
        LeftTrail(0);
        RightTrail(0);
        if (skillSelector.isInstantComboTriggered) skillSelector.isInstantComboTriggered = false;

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