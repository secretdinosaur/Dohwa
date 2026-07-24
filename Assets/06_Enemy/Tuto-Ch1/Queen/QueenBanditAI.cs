using UnityEngine;
using System;
using System.Collections;
using System.Linq;
using UnityEngine.UIElements;

[RequireComponent(typeof(BossSkillSelector))]
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
    public float chaseBuffer = 1.0f; // 상태 전환 안정화를 위한 버퍼
    public float closeWanderMin = 1.5f;
    public float closeWanderMax = 3.5f;

    [Header("Special Skill Logic (Step Before Attack)")]
    public SkillBase[] leftStepSkills;
    public SkillBase[] rightStepSkills;
    public SkillBase[] preBackStepSkills;

    // 💡 지진 공격 등 이펙트 지연 해제가 필요한 스킬들 할당
    [Header("Delayed Effect Skills")]
    public SkillBase[] earthquakeSkills;
    public float earthquakeEffectDelay = 10f; // 10초 뒤 사라짐

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

    private BossSkillSelector skillSelector;
    private float nextWanderTime = 0f;
    private Vector3 cooldownWanderTarget;

    private float lastEvasionTime = -9999f;
    private bool isStepping = false;
    private bool isTransitioning = false;
    private bool isStartingFight = false;

    protected override void Awake()
    {
        base.Awake();
        skillSelector = GetComponent<BossSkillSelector>();
        agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        core.OnStunned += HandleInterruptSpecial;
        core.OnDeath += HandleInterruptSpecial;
        core.OnPhaseTransitionStart += HandlePhaseTransition;
        core.OnRevived += HandleRevived;
        core.OnPlayerDetected += HandlePlayerDetected;
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
        core.SetMoveAnimations(false, false, false);
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
        core.SetMoveAnimations(false, false, false);
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

    protected override void HandleBehaviorLogic(float distToLastPos)
    {
        if (isAttacking || isStepping || isTransitioning || isStartingFight || core.currentState == EnemyState.BackWalk) return;
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
                if (distToLastPos > 5f) UpdateChaseTarget();
                else HandleCooldownWalk(skillSelector.GetOptimalDistance(null));
                return;
            }

            float executeRange = GetSkillRange(selectedSkill) * skillRangeMultiplier;
            bool canAttackGlobal = isDetectedPlayer && (Time.time >= nextAttackTime || skillSelector.isNextSkillCombo);
            bool hasEnoughMP = core.QueryHasEnoughMP?.Invoke(selectedSkill.MP_cost) ?? false;

            if (canAttackGlobal && hasEnoughMP)
            {
                if (distToLastPos <= executeRange)
                {
                    float evasionRange = executeRange * evasionTriggerFactor;
                    bool canEvade = Time.time >= lastEvasionTime + evasionCooldown && UnityEngine.Random.value <= evasionProbability;
                    if (distToLastPos < evasionRange && canEvade)
                    {
                        actionCoroutine = StartCoroutine(EvasiveBackStepSequence());
                    }
                    else
                    {
                        if (leftStepSkills.Contains(selectedSkill))
                            actionCoroutine = StartCoroutine(StepAttackSequence(selectedSkill, sideStepDistance, -35f, "leftStep", sideStepDuration));
                        else if (rightStepSkills.Contains(selectedSkill))
                            actionCoroutine = StartCoroutine(StepAttackSequence(selectedSkill, sideStepDistance, 35f, "rightStep", sideStepDuration));
                        else if (preBackStepSkills.Contains(selectedSkill))
                            actionCoroutine = StartCoroutine(StepAttackSequence(selectedSkill, attackBackStepDistance, 180f, "backStep", attackBackStepDuration));
                        else
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
                // 히스테리시스 적용: 현재 상태에 따라 추적을 멈출 임계값 조정
                float threshold = (core.currentState == EnemyState.Chase)
                    ? (executeRange * walkApproachFactor) + chaseBuffer
                    : (executeRange * walkApproachFactor);

                if (distToLastPos > threshold) UpdateChaseTarget();
                else HandleCooldownWalk(skillSelector.GetOptimalDistance(selectedSkill));
            }
        }
        else
        {
            core.ChangeState(EnemyState.Idle);
            core.CmdStopMovement?.Invoke();
            core.SetMoveAnimations(false, false, false);

            if (BossMusic != null && BossMusic.Length > 0 && !isDetectedPlayer)
            {
                SoundManager.Instance.StopSpecialBGMWithFade();
            }
        }
    }

    private void UpdateChaseTarget()
    {
        if (agent != null) agent.updateRotation = false;
        core.ChangeState(EnemyState.Chase);
        core.SetMoveAnimations(true, false, false);
        core.CmdLookAt?.Invoke(lastKnownPlayerPos);

        if (Time.time >= nextTargetUpdateTime)
        {
            nextTargetUpdateTime = Time.time + targetUpdateInterval;

            Vector3 dirFromPlayer = (transform.position - lastKnownPlayerPos).normalized;
            Vector3 targetPos = lastKnownPlayerPos + (dirFromPlayer * 1f);

            core.CmdMoveTo?.Invoke(targetPos, chaseSpeedMultiplier);
        }
    }

    private void UpdateWalkApproachTarget()
    {
        if (agent != null) agent.updateRotation = false;
        core.ChangeState(EnemyState.Chase);
        core.SetMoveAnimations(false, true, false);
        core.CmdLookAt?.Invoke(lastKnownPlayerPos);

        if (Time.time >= nextTargetUpdateTime)
        {
            nextTargetUpdateTime = Time.time + targetUpdateInterval;

            Vector3 dirFromPlayer = (transform.position - lastKnownPlayerPos).normalized;
            Vector3 targetPos = lastKnownPlayerPos + (dirFromPlayer * 1f);

            core.CmdMoveTo?.Invoke(targetPos, 1f);
        }
    }

    private void HandleCooldownWalk(float optimalDistance)
    {
        if (agent != null) agent.updateRotation = false;

        core.ChangeState(EnemyState.CooldownWalk);
        core.SetMoveAnimations(false, true, false);

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

            if (leftStepSkills.Contains(selectedSkill))
                actionCoroutine = StartCoroutine(StepAttackSequence(selectedSkill, sideStepDistance, -35f, "leftStep", sideStepDuration));
            else if (rightStepSkills.Contains(selectedSkill))
                actionCoroutine = StartCoroutine(StepAttackSequence(selectedSkill, sideStepDistance, 35f, "rightStep", sideStepDuration));
            else if (preBackStepSkills.Contains(selectedSkill))
                actionCoroutine = StartCoroutine(StepAttackSequence(selectedSkill, attackBackStepDistance, 180f, "backStep", attackBackStepDuration));
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

    private IEnumerator StepAttackSequence(SkillBase skill, float distance, float angle, string stepAnimName, float stepDuration)
    {
        isAttacking = true;
        isStepping = true;

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
            nextAttackTime = Time.time + skillSelector.GetLastSkillDelay();
            attackFinished = true;
        };

        core.OnAttackCompleted += onComplete;
        core.CmdPerformAttack?.Invoke(skill);

        yield return new WaitUntil(() => attackFinished || skillSelector.isInstantComboTriggered);

        core.OnAttackCompleted -= onComplete;

        if (skillSelector.isInstantComboTriggered) skillSelector.isInstantComboTriggered = false;

        ChainNextAttackOrIdle();
    }

    private IEnumerator EvasiveBackStepSequence()
    {
        isStepping = true;
        lastEvasionTime = Time.time;

        core.CmdStopMovement?.Invoke();
        core.ChangeState(EnemyState.BackWalk);
        core.SetMoveAnimations(false, false, true);
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
        core.SetMoveAnimations(false, false, false);

        isStepping = false;
        core.ChangeState(EnemyState.Idle);
        actionCoroutine = null;
    }

    private IEnumerator AttackSequence(SkillBase skill)
    {
        isAttacking = true;

        skillSelector.isInstantComboTriggered = false;

        core.CmdStopMovement?.Invoke();
        core.ChangeState(EnemyState.Attack);

        // 💡 Attack3(Push Attack)일 경우 플레이어 무시 로직 켜기
        bool isPushAttack = PushSkill != null && PushSkill == skill;
        if (isPushAttack)
        {
            core.CmdSetIgnorePlayerCollision?.Invoke(false);
        }

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

        if (isPushAttack)
        {
            core.CmdSetIgnorePlayerCollision?.Invoke(true);
        }

        bool isEarthquake = earthquakeSkills != null && earthquakeSkills.Contains(skill);
        float stopDelay = isEarthquake ? earthquakeEffectDelay : 0f;
        core.StopEffects(skill.name, stopDelay);

        if (skillSelector.isInstantComboTriggered) skillSelector.isInstantComboTriggered = false;

        ChainNextAttackOrIdle();
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

    public void LeftTrail(int status)
    {
        if (leftTrail != null) leftTrail.emitting = status != 0;
    }

    public void RightTrail(int status)
    {
        if (rightTrail != null) rightTrail.emitting = status != 0;
    }
}