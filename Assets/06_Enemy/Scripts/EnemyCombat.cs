using UnityEngine;
using DG.Tweening;
using System.Collections.Generic;
using System.Collections;

public class EnemyCombat : MonoBehaviour
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
        core.OnParried += CancelCurrentSkillAction;
        core.OnDeath += CancelCurrentSkillAction;
        core.OnStunned += CancelCurrentSkillAction;

        core.CmdPerformAttack += InitiateAttack;
    }

    private void OnDisable()
    {
        core.OnDamageTaken -= HandleHit;
        core.OnParried -= CancelCurrentSkillAction;
        core.OnDeath -= CancelCurrentSkillAction;
        core.OnStunned -= CancelCurrentSkillAction;

        core.CmdPerformAttack -= InitiateAttack;
    }

    private void InitiateAttack(SkillBase skill)
    {
        if (attackRoutine != null) StopCoroutine(attackRoutine);
        attackRoutine = StartCoroutine(PerformAttackRoutine(skill));
    }

    private IEnumerator PerformAttackRoutine(SkillBase skill)
    {
        core.CmdStopMovement?.Invoke();
        currentActiveSkill = skill;
        core.ChangeState(EnemyState.Attack);

        float elapsed = 0f;
        while (elapsed < preAttackLookDelay)
        {
            core.CmdFastLookAt?.Invoke(Player.Instance.transform.position);
            elapsed += Time.deltaTime;
            yield return null;
        }

        Animator_PlaySkill(skill);
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
        core.animator.CrossFadeInFixedTime(targetState, crossFadeDuration);

        currentSkillStateIndex = currentSkillStateIndex == 1 ? 2 : 1;
    }

    private void HandleHit(float damage)
    {
        if (core.isGuarding) return;
        if(core.isTakingDoT) return;
        TriggerHitStop();

        if (core.currentState == EnemyState.Dead) return;

        CancelCurrentSkillAction();
        core.animator.SetTrigger(UnityEngine.Random.Range(0, 2) == 0 ? "Hitted1" : "Hitted2");
    }

    private void TriggerHitStop()
    {
        Player.Instance.impulseSource.GenerateImpulse(0.5f);
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

        core.CmdCancelAttack?.Invoke();
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