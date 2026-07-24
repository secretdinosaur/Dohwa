using UnityEngine;
using System;
using System.Collections;

[RequireComponent(typeof(BossSkillSelector))]
public class Bandit5AI : EnemyAIBase
{
    [Header("Behavior Tuning")]
    public float walkApproachFactor = 1.5f;
    public float closeWanderMin = 1.5f;
    public float closeWanderMax = 3.5f;
    [Range(0f, 1f)] public float walkApproachChance = 0.2f; // 💡 멀어졌을 때 걸어갈 확률 (40%)
    private bool isChoosingWalkApproach = false;            // 💡 결정된 이동 방식 유지용

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

    private BossSkillSelector skillSelector;
    private float nextWanderTime = 0f;
    private Vector3 cooldownWanderTarget;

    protected override void Awake()
    {
        base.Awake();
        skillSelector = GetComponent<BossSkillSelector>();
    }

    protected override void HandleBehaviorLogic(float distToLastPos)
    {
        if (isAttacking || core.currentState == EnemyState.BackWalk) return;
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
                if (selectedSkill == jumpAttackSkill)
                {
                    actionCoroutine = StartCoroutine(JumpAttackSequence(selectedSkill));
                }
                else if (selectedSkill == runAttackSkill)
                {
                    actionCoroutine = StartCoroutine(RunAttackSequence(selectedSkill));
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
                if (distToLastPos > executeRange * walkApproachFactor)
                {
                    HandleDistantApproach(executeRange);
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

        ChainNextAttackOrIdle();
    }

    private IEnumerator RunAttackSequence(SkillBase skill)
    {
        isAttacking = true;
        core.CmdStopMovement?.Invoke();
        core.ChangeState(EnemyState.Attack);

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

        ChainNextAttackOrIdle();
    }

    private IEnumerator AttackSequence(SkillBase skill)
    {
        isAttacking = true;
        core.CmdStopMovement?.Invoke();
        core.ChangeState(EnemyState.Attack);

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

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
    }
#endif
}