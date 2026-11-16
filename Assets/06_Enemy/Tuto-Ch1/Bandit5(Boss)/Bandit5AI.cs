using UnityEngine;
using System;
using System.Collections;

[RequireComponent(typeof(SkillSelector))]
public class Bandit5AI : EnemyAIBase
{
    [Header("Behavior Tuning")]
    public float walkApproachFactor = 1.5f;
    [Range(0f, 1f)] public float walkApproachChance = 0.2f;
    private bool isChoosingWalkApproach = false;

    [Header("Run Attack Specific")]
    public SkillBase runAttackSkill;
    public float runDuration = 0.6f;
    public float runStopOffset = 1.5f;

    [Header("Jump Attack Specific")]
    public SkillBase jumpAttackSkill;
    public AnimationCurve jumpCurve = new AnimationCurve(new Keyframe(0, 0), new Keyframe(0.5f, 1), new Keyframe(1, 0));
    public float jumpHeight = 3f;
    public float jumpDuration = 0.8f;
    public float jumpLandingOffset = 1.5f;

    [Header("Weapon Decal Specific (무기 흔적)")]
    public GameObject currentWeapon;
    public Material[] customDecalMaterials;
    public Vector2 customDecalSize = new Vector2(0.8f, 2.5f);

    private SkillSelector skillSelector;

    protected override void Awake()
    {
        base.Awake();
        skillSelector = GetComponent<SkillSelector>();

        // 💡 [버그 픽스 1] 플레이어를 발견하는 즉시 배회(Wander) 코루틴을 끊어내기 위한 이벤트 구독
        core.OnPlayerDetected += HandlePlayerDetected;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (core != null)
        {
            core.OnPlayerDetected -= HandlePlayerDetected;
        }
    }

    private void HandlePlayerDetected()
    {
        StopActionRoutine(); // 플레이어를 발견한 첫 순간에만 하던 행동을 정지합니다.
    }

    protected override void HandleBehaviorLogic(float distToLastPos)
    {
        if (core.IsDead) return;
        if (isAttacking || actionCoroutine != null) return;

        if (isDetectedPlayer || isMemorizingPlayer)
        {
            // 🚨 [핵심 버그 픽스 2] 매 프레임마다 AI의 이동을 강제로 멈추게 만들던 StopActionRoutine(); 삭제!

            if (selectedSkill == null)
                selectedSkill = skillSelector.GetNextSkill(distToLastPos);

            if (selectedSkill == null)
            {
                if (distToLastPos > 2.5f) UpdateChaseTarget();
                else actionCoroutine = StartCoroutine(StrafeRoutine(UnityEngine.Random.Range(strafeMinDuration, strafeMaxDuration)));
                return;
            }

            float executeRange = GetSkillRange(selectedSkill) * skillRangeMultiplier;
            float backWalkRange = executeRange * backWalkTriggerFactor;

            bool canAttackGlobal = isDetectedPlayer && (Time.time >= nextAttackTime || skillSelector.isNextSkillCombo);
            bool hasEnoughMP = core.QueryHasEnoughMP?.Invoke(selectedSkill.MP_cost) ?? false;
            bool isWithinAttackBox = IsPlayerInSkillBox(selectedSkill, skillRangeMultiplier);

            if (isWithinAttackBox)
            {
                if (canAttackGlobal && hasEnoughMP)
                {
                    if (selectedSkill == jumpAttackSkill)
                    {
                        actionCoroutine = StartCoroutine(ExecuteSkillAndChain(selectedSkill));
                        lastActionWasEvasion = false;
                    }
                    else if (selectedSkill == runAttackSkill)
                    {
                        if (core.currentState == EnemyState.Chase && isRunningForward)
                        {
                            actionCoroutine = StartCoroutine(ExecuteSkillAndChain(selectedSkill));
                            lastActionWasEvasion = false;
                        }
                        else
                        {
                            UpdateChaseTarget();
                            lastActionWasEvasion = false;
                        }
                    }
                    else
                    {
                        actionCoroutine = StartCoroutine(ExecuteSkillAndChain(selectedSkill));
                        lastActionWasEvasion = false;
                    }
                }
                else
                {
                    if (distToLastPos < backWalkRange && !lastActionWasEvasion && !skillSelector.HasPendingCombo)
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
            else
            {
                if (distToLastPos < backWalkRange)
                {
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
                    if (canAttackGlobal && hasEnoughMP)
                    {
                        if (selectedSkill == jumpAttackSkill) HandleDistantApproach(executeRange);
                        else if (selectedSkill == runAttackSkill) UpdateChaseTarget();
                        else
                        {
                            if (distToLastPos <= executeRange * walkApproachFactor) UpdateWalkApproachTarget();
                            else HandleDistantApproach(executeRange);
                        }
                    }
                    else HandleDistantApproach(executeRange);
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
                isChoosingWalkApproach = UnityEngine.Random.value < walkApproachChance;
            else
                isChoosingWalkApproach = true;
        }

        if (isChoosingWalkApproach) UpdateWalkApproachTarget();
        else UpdateChaseTarget();
    }

    protected override IEnumerator RouteAndExecuteSkill(SkillBase skill)
    {
        if (skill == jumpAttackSkill) yield return StartCoroutine(JumpAttackSequence(skill));
        else if (skill == runAttackSkill) yield return StartCoroutine(RunAttackSequence(skill));
        else yield return StartCoroutine(base.RouteAndExecuteSkill(skill));
    }

    private IEnumerator JumpAttackSequence(SkillBase skill)
    {
        isAttacking = true;
        core.CmdStopMovement?.Invoke();
        core.ChangeState(EnemyState.Attack);
        isRunningForward = false;
        bool attackFinished = false;

        currentAttackCompleteHandler = (nextTime) => {
            float delay = skillSelector.GetLastSkillDelay();
            nextAttackTime = Time.time + delay;
            attackFinished = true;
        };
        core.OnAttackCompleted += currentAttackCompleteHandler;

        try
        {
            core.CmdPerformAttack?.Invoke(skill);

            float prepTime = 0f;
            while (prepTime < jumpDuration * 0.3f)
            {
                if (skillSelector.isInstantComboTriggered) break;
                prepTime += Time.deltaTime;
                yield return null;
            }

            Vector3 startPos = transform.position;
            Vector3 playerPos = Player.Instance.transform.position;
            Vector3 dirToPlayer = (playerPos - startPos).normalized;
            dirToPlayer.y = 0f;

            Vector3 targetPos = playerPos - (dirToPlayer * jumpLandingOffset);
            targetPos.y = startPos.y;

            float elapsed = 0f;
            float moveDuration = jumpDuration * (0.7916f - 0.3f);

            while (elapsed < moveDuration)
            {
                if (skillSelector.isInstantComboTriggered) break;
                float t = elapsed / moveDuration;
                Vector3 currentPos = Vector3.Lerp(startPos, targetPos, t);
                currentPos.y += jumpCurve.Evaluate(t) * jumpHeight;
                transform.position = currentPos;
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (!skillSelector.isInstantComboTriggered)
            {
                core.CmdLookAt?.Invoke(Player.Instance.transform.position);
                transform.position = targetPos;
                core.CmdTrySnapToNavMesh?.Invoke();
            }

            yield return new WaitUntil(() => attackFinished || skillSelector.isInstantComboTriggered || core.wasParried || core.currentState != EnemyState.Attack || core.IsDead);
        }
        finally
        {
            ClearAttackHandler();
            // 💡 [핵심 버그 픽스 3] 공격 로직이 정상 종료되든, 스턴에 걸려 강제 종료되든 무조건 행동 잠금을 해제합니다.
            isAttacking = false;
            actionCoroutine = null;
        }

        if (skillSelector.isInstantComboTriggered) skillSelector.isInstantComboTriggered = false;
        StopAttackEffects(skill);
    }

    private IEnumerator RunAttackSequence(SkillBase skill)
    {
        isAttacking = true;
        core.CmdStopMovement?.Invoke();
        core.ChangeState(EnemyState.Attack);
        isRunningForward = false;
        bool attackFinished = false;

        currentAttackCompleteHandler = (nextTime) => {
            float delay = skillSelector.GetLastSkillDelay();
            nextAttackTime = Time.time + delay;
            attackFinished = true;
        };
        core.OnAttackCompleted += currentAttackCompleteHandler;

        try
        {
            core.CmdPerformAttack?.Invoke(skill);

            float elapsed = 0f;
            yield return new WaitForSeconds(2f);

            Vector3 startPos = transform.position;

            while (elapsed < runDuration)
            {
                if (skillSelector.isInstantComboTriggered) break;
                Vector3 currentPlayerPos = Player.Instance.transform.position;
                Vector3 dirToPlayer = (currentPlayerPos - startPos).normalized;
                dirToPlayer.y = 0f;

                Vector3 targetPos = currentPlayerPos - (dirToPlayer * runStopOffset);
                targetPos.y = startPos.y;

                float t = elapsed / runDuration;
                transform.position = Vector3.Lerp(startPos, targetPos, t);
                core.CmdLookAt?.Invoke(currentPlayerPos);

                elapsed += Time.deltaTime;
                yield return null;
            }

            if (!skillSelector.isInstantComboTriggered)
            {
                core.CmdLookAt?.Invoke(Player.Instance.transform.position);
                core.CmdTrySnapToNavMesh?.Invoke();
            }

            yield return new WaitUntil(() => attackFinished || skillSelector.isInstantComboTriggered || core.wasParried || core.currentState != EnemyState.Attack || core.IsDead);
        }
        finally
        {
            ClearAttackHandler();
            // 💡 [핵심 버그 픽스 3] 여기도 마찬가지로 잠금을 해제해줍니다.
            isAttacking = false;
            actionCoroutine = null;
        }

        if (skillSelector.isInstantComboTriggered) skillSelector.isInstantComboTriggered = false;
        StopAttackEffects(skill);
    }

    public void SpawnWeaponGroundDecal()
    {
        if (currentWeapon == null || customDecalMaterials == null || customDecalMaterials.Length == 0) return;
        if (MapDecalManager.Instance == null) return;

        Vector3 rayStart = currentWeapon.transform.position;
        int groundLayer = LayerMask.GetMask("Ground");

        if (Physics.Raycast(rayStart + Vector3.up, Vector3.down, out RaycastHit hit, 15.0f, groundLayer))
        {
            Material selectedMat = customDecalMaterials[UnityEngine.Random.Range(0, customDecalMaterials.Length)];
            MapDecalManager.Instance.SpawnCustomDecal(
                hit.point, hit.normal, transform.forward, selectedMat,
                customDecalSize.x * UnityEngine.Random.Range(0.8f, 1.25f),
                customDecalSize.y * UnityEngine.Random.Range(0.8f, 1.25f)
            );
        }
    }
}