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

    [Header("Strafe & Evasion Specific")]
    public float backWalkTriggerFactor = 0.7f;
    [Range(0f, 100f)] public float strafeOnCloseChance = 30f;
    private bool lastActionWasBackWalk = false;

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
    public Vector2 customDecalSize = new Vector2(0.8f, 2.5f); // 데칼의 기본 가로세로 크기

    private SkillSelector skillSelector;

    protected override void Awake()
    {
        base.Awake();
        skillSelector = GetComponent<SkillSelector>();
    }

    protected override void HandleBehaviorLogic(float distToLastPos)
    {
        if (isAttacking || core.currentState == EnemyState.BackWalk || core.currentState == EnemyState.CooldownWalk || core.currentState == EnemyState.Dead) return;
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
                if (distToLastPos > 2.5f)
                {
                    UpdateChaseTarget();
                }
                else
                {
                    actionCoroutine = StartCoroutine(StrafeRoutine(UnityEngine.Random.Range(strafeMinDuration, strafeMaxDuration)));
                }
                return;
            }

            float executeRange = GetSkillRange(selectedSkill) * skillRangeMultiplier;
            bool canAttackGlobal = isDetectedPlayer && (Time.time >= nextAttackTime || skillSelector.isNextSkillCombo);
            bool hasEnoughMP = core.QueryHasEnoughMP?.Invoke(selectedSkill.MP_cost) ?? false;

            if (canAttackGlobal && hasEnoughMP)
            {
                if (selectedSkill == jumpAttackSkill)
                {
                    if (distToLastPos <= executeRange)
                        actionCoroutine = StartCoroutine(JumpAttackSequence(selectedSkill));
                    else
                        HandleDistantApproach(executeRange);
                }
                else if (selectedSkill == runAttackSkill)
                {
                    if (distToLastPos <= executeRange && core.currentState == EnemyState.Chase && isRunningForward)
                    {
                        actionCoroutine = StartCoroutine(RunAttackSequence(selectedSkill));
                    }
                    else
                    {
                        UpdateChaseTarget();
                    }
                }
                else if (distToLastPos <= executeRange)
                {
                    actionCoroutine = StartCoroutine(AttackSequence(selectedSkill));
                }
                else if (distToLastPos <= executeRange * walkApproachFactor)
                {
                    UpdateWalkApproachTarget();
                }
                else
                {
                    HandleDistantApproach(executeRange);
                }
            }
            else
            {
                if (distToLastPos > executeRange)
                {
                    HandleDistantApproach(executeRange);
                }
                else
                {
                    float backWalkRange = executeRange * backWalkTriggerFactor;
                    if (distToLastPos < backWalkRange && !lastActionWasBackWalk)
                    {
                        if (UnityEngine.Random.Range(0f, 100f) < strafeOnCloseChance)
                        {
                            actionCoroutine = StartCoroutine(StrafeRoutine(UnityEngine.Random.Range(strafeMinDuration, strafeMaxDuration)));
                        }
                        else
                        {
                            actionCoroutine = StartCoroutine(BackWalkRoutine(backWalkDist));
                        }
                        lastActionWasBackWalk = true;
                    }
                    else
                    {
                        actionCoroutine = StartCoroutine(StrafeRoutine(UnityEngine.Random.Range(strafeMinDuration, strafeMaxDuration)));
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

    private void HandleDistantApproach(float executeRange)
    {
        float currentDist = Vector3.Distance(transform.position, lastKnownPlayerPos);

        if (Time.time >= nextTargetUpdateTime)
        {
            if (currentDist > executeRange * walkApproachFactor * 1.5f)
            {
                isChoosingWalkApproach = UnityEngine.Random.value < walkApproachChance;
            }
            else
            {
                isChoosingWalkApproach = true;
            }
        }

        if (isChoosingWalkApproach)
        {
            UpdateWalkApproachTarget();
        }
        else
        {
            UpdateChaseTarget();
        }
    }

    private void ChainNextAttackOrIdle()
    {
        if (skillSelector.HasPendingCombo)
        {
            float currentDist = Vector3.Distance(transform.position, Player.Instance.transform.position);
            selectedSkill = skillSelector.GetNextSkill(currentDist);

            if (selectedSkill == jumpAttackSkill)
            {
                actionCoroutine = StartCoroutine(JumpAttackSequence(selectedSkill));
            }
            else if (selectedSkill == runAttackSkill)
            {
                actionCoroutine = StartCoroutine(RunAttackSequence(selectedSkill));
            }
            else
            {
                actionCoroutine = StartCoroutine(AttackSequence(selectedSkill));
            }
        }
        else
        {
            isAttacking = false;
            selectedSkill = null;
            core.ChangeState(EnemyState.Idle);
            actionCoroutine = null;
        }
    }

    private IEnumerator JumpAttackSequence(SkillBase skill)
    {
        isAttacking = true;
        core.CmdStopMovement?.Invoke();
        core.ChangeState(EnemyState.Attack);
        isRunningForward = false;

        bool attackFinished = false;

        Action<float> onComplete = (nextTime) => {
            float delay = skillSelector.GetLastSkillDelay();
            nextAttackTime = Time.time + delay;
            attackFinished = true;
        };

        core.OnAttackCompleted += onComplete;
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

        yield return new WaitUntil(() => attackFinished || skillSelector.isInstantComboTriggered);

        core.OnAttackCompleted -= onComplete;

        if (skillSelector.isInstantComboTriggered)
        {
            skillSelector.isInstantComboTriggered = false;
        }
        core.StopEffects(skill.name);

        ChainNextAttackOrIdle();
    }

    private IEnumerator RunAttackSequence(SkillBase skill)
    {
        isAttacking = true;
        core.CmdStopMovement?.Invoke();
        core.ChangeState(EnemyState.Attack);
        isRunningForward = false;

        bool attackFinished = false;

        Action<float> onComplete = (nextTime) => {
            float delay = skillSelector.GetLastSkillDelay();
            nextAttackTime = Time.time + delay;
            attackFinished = true;
        };

        core.OnAttackCompleted += onComplete;
        core.CmdPerformAttack?.Invoke(skill);

        float elapsed = 0f;
        yield return new WaitForSeconds(1);

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

        yield return new WaitUntil(() => attackFinished || skillSelector.isInstantComboTriggered);

        core.OnAttackCompleted -= onComplete;

        if (skillSelector.isInstantComboTriggered)
        {
            skillSelector.isInstantComboTriggered = false;
        }

        // RunAttackSequence에도 누락되었던 이펙트 종료 로직 추가
        core.StopEffects(skill.name);

        ChainNextAttackOrIdle();
    }

    private IEnumerator AttackSequence(SkillBase skill)
    {
        isAttacking = true;
        core.CmdStopMovement?.Invoke();
        core.ChangeState(EnemyState.Attack);
        isRunningForward = false;

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

        if (skillSelector.isInstantComboTriggered)
        {
            skillSelector.isInstantComboTriggered = false;
        }
        core.StopEffects(skill.name);
        ChainNextAttackOrIdle();
    }

    // ============================================================
    // 새로 추가된 무기 데칼 생성 함수 (애니메이션 이벤트 등에서 호출)
    // ============================================================
    public void SpawnWeaponGroundDecal()
    {
        if (currentWeapon == null || customDecalMaterials == null || customDecalMaterials.Length == 0) return;
        if (MapDecalManager.Instance == null) return;

        Vector3 rayStart = currentWeapon.transform.position;
        int groundLayer = LayerMask.GetMask("Ground");
        // 무기 위치에서 수직 방향(아래)으로 레이캐스트
        if (Physics.Raycast(rayStart + Vector3.up, Vector3.down, out RaycastHit hit, 15.0f, groundLayer))
        {
            Material selectedMat = customDecalMaterials[UnityEngine.Random.Range(0, customDecalMaterials.Length)];

            // 데칼의 방향(velocity 인자)을 캐릭터가 바라보는 방향(transform.forward)으로 넘겨주어 
            // 캐릭터의 전진/공격 방향에 맞춰 길쭉한 자국이 남도록 설정합니다.

            MapDecalManager.Instance.SpawnCustomDecal(
                hit.point,
                hit.normal,
                transform.forward,
                selectedMat,
                customDecalSize.x * UnityEngine.Random.Range(0.8f, 1.25f),
                customDecalSize.y * UnityEngine.Random.Range(0.8f, 1.25f)
            );
        }
    }
}