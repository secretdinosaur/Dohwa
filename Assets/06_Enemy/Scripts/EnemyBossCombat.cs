using UnityEngine;
using DG.Tweening;
using System.Collections.Generic;
using System.Collections;

public class EnemyBossCombat : MonoBehaviour
{
    public float Power = 1f;
    public Vector2 attackCooldownRange;

    private float hitStopTimeScale = 0.1f, hitStopDuration = 0.1f, hitStopRestoreDuration = 0.025f;
    private float preAttackLookDelay = 0.25f;

    private EnemyCore core;
    private EnemySound sound;
    private AnimatorOverrideController overrideController;
    private HashSet<int> processedHitIndices = new HashSet<int>();
    private SkillBase currentActiveSkill;
    private Coroutine attackRoutine;
    private const string HITSTOP_ID = "HitStop";

    private int currentSkillStateIndex = 1;

    private void Awake()
    {
        core = GetComponent<EnemyCore>();
        sound = GetComponent<EnemySound>();
    }

    private void Start()
    {
        overrideController = new AnimatorOverrideController(core.animator.runtimeAnimatorController);
        core.animator.runtimeAnimatorController = overrideController;
    }

    private void OnEnable()
    {
        core.OnDamageTaken += HandleHit;
        core.OnDeath += CancelCurrentSkillAction;
        core.OnStunned += CancelCurrentSkillAction;

        core.CmdPerformAttack += InitiateAttack;
        // 💡 추가됨: 즉시 스킬 공격 발동 연결
        core.CmdPerformInstantAttack += InitiateInstantAttack;
        core.CmdCancelAttack += CancelCurrentSkillAction;
    }

    private void OnDisable()
    {
        core.OnDamageTaken -= HandleHit;
        core.OnDeath -= CancelCurrentSkillAction;
        core.OnStunned -= CancelCurrentSkillAction;

        core.CmdPerformAttack -= InitiateAttack;
        core.CmdPerformInstantAttack -= InitiateInstantAttack;
        core.CmdCancelAttack -= CancelCurrentSkillAction;
    }

    private void InitiateAttack(SkillBase skill)
    {
        bool isCombo = attackRoutine != null;

        if (attackRoutine != null) StopCoroutine(attackRoutine);
        // 기본 딜레이 적용
        attackRoutine = StartCoroutine(PerformAttackRoutine(skill, isCombo, isCombo ? 0.2f : 0.1f));
    }

    // 💡 추가됨: 크로스페이드 없이 즉시 공격 시작
    private void InitiateInstantAttack(SkillBase skill)
    {
        bool isCombo = attackRoutine != null;
        print("InitiateInstantAttack");
        if (attackRoutine != null) StopCoroutine(attackRoutine);
        // 전환 시간(crossFadeDuration)을 0f로 넘겨줌
        attackRoutine = StartCoroutine(PerformAttackRoutine(skill, isCombo, 0f));
    }

    private IEnumerator PerformAttackRoutine(SkillBase skill, bool isCombo, float crossFadeDuration)
    {
        core.CmdStopMovement?.Invoke();
        currentActiveSkill = skill;
        core.ChangeState(EnemyState.Attack);

        if (!isCombo && crossFadeDuration != 0f)
        {
            float elapsed = 0f;
            while (elapsed < preAttackLookDelay)
            {
                core.CmdFastLookAt?.Invoke(Player.Instance.transform.position);
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        Animator_PlaySkill(skill, crossFadeDuration);
        core.CmdConsumeMP?.Invoke(skill.MP_cost);
        if (sound != null) sound.PlaySkillSound(skill.sound);
        core.PlayEffects(skill.name);

        yield return new WaitForSeconds(skill.clip.length);

        float nextAttackTime = Time.time + UnityEngine.Random.Range(attackCooldownRange.x, attackCooldownRange.y);
        core.OnAttackCompleted?.Invoke(nextAttackTime);
        attackRoutine = null;
    }

    private void Animator_PlaySkill(SkillBase skill, float crossFadeDuration = 0.1f)
    {
        string targetState = "SkillState" + currentSkillStateIndex;

        overrideController[targetState] = skill.clip;
        processedHitIndices.Clear();

        // 💡 0 이하일 경우 CrossFade 대신 Play를 사용하여 프레임 끊김 없이 즉시 재생
        if (crossFadeDuration <= 0f)
        {
            print("anim play");
            core.animator.Play(targetState, 0, 0f);
        }
        else
        {
            core.animator.CrossFadeInFixedTime(targetState, crossFadeDuration);
        }

        currentSkillStateIndex = currentSkillStateIndex == 1 ? 2 : 1;
    }

    private void HandleHit(float damage)
    {
        if (core.isGuarding) return;
        TriggerHitStop();

        if (core.currentState == EnemyState.Dead) return;
    }

    private void TriggerHitStop()
    {
    }

    public void OnAttackHitEvent(int index)
    {
        if (processedHitIndices.Contains(index) || currentActiveSkill == null) return;
        if (core.animator.IsInTransition(0)) return;

        var target = currentActiveSkill.ExecuteHitCheck(transform, Power, index);
        if (target.Count > 0) processedHitIndices.Add(index);
    }

    private void CancelCurrentSkillAction()
    {
        if (currentActiveSkill != null)
        {
            core.StopEffects(currentActiveSkill.name);
        }

        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;

            float nextAttackTime = Time.time + UnityEngine.Random.Range(attackCooldownRange.x, attackCooldownRange.y);
            core.OnAttackCompleted?.Invoke(nextAttackTime);
        }

        currentActiveSkill = null;
    }
}