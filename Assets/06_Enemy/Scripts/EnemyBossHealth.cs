using UnityEngine;
using System;
using System.Collections;

public class EnemyBossHealth : MonoBehaviour
{
    public float maxHP = 100f;
    public float maxMP = 100f;
    public float mpRecoveryPerSecond = 5f;
    public float stunDuration = 3f;

    [Header("Phase Settings")]
    public int maxPhase = 1;
    public int currentPhase = 1;
    public float phaseTransitionDelay = 7f;
    public float afterPhaseTransitionDelay = 3f;
    public float phaseTransitionHealDuration = 4f;

    private float currentHp, currentMp;
    private EnemyCore core;
    private bool isTransitioningPhase = false;

    private void Awake()
    {
        core = GetComponent<EnemyCore>();
        currentHp = maxHP;
        currentMp = maxMP;
        currentPhase = 1;
    }

    private void OnEnable()
    {
        core.OnDamageTaken += HandleDamage;
        core.OnParryDamageTaken += HandleParryDamage;
        core.OnRevived += ResetHealth;

        core.QueryHasEnoughMP = HasEnoughMP;
        core.CmdConsumeMP += ConsumeMP;
    }

    private void OnDisable()
    {
        core.OnDamageTaken -= HandleDamage;
        core.OnParryDamageTaken -= HandleParryDamage;
        core.OnRevived -= ResetHealth;

        core.QueryHasEnoughMP = null;
        core.CmdConsumeMP -= ConsumeMP;
    }

    private void Update()
    {
        if (!core.IsDead && core.currentState != EnemyState.Stun && currentMp < maxMP && !isTransitioningPhase)
            UpdateMP(currentMp + (mpRecoveryPerSecond * Time.deltaTime));
    }

    private void HandleDamage(float damage)
    {
        if (isTransitioningPhase) return;

        // 💡 범용 가드 상태 체크
        if (core.isGuarding)
        {
            core.TriggerGuardSuccess();
            return;
        }

        currentHp -= damage;
        core.TriggerHpChanged(currentHp, maxHP);
        UIManager.Instance.UpdateBossHpUI(currentHp / maxHP);
        core.PlayHitSound(core.HittedSkill, core.HitIndex);
        if (currentHp <= 0)
        {
            if (currentPhase < maxPhase)
            {
                currentHp = 0;
                StartCoroutine(PhaseTransitionRoutine());
            }
            else
            {
                core.TriggerDeath();
            }
        }
    }

    private void HandleParryDamage(float mpDamage)
    {
        if (isTransitioningPhase) return;
        ConsumeMP(mpDamage);

        if (currentMp <= 0 && core.currentState != EnemyState.Stun)
        {
            core.TriggerParried();
            StartCoroutine(StunRoutine());
        }
    }

    private IEnumerator PhaseTransitionRoutine()
    {
        isTransitioningPhase = true;

        core.ChangeState(EnemyState.Dead);
        core.CmdStopMovement?.Invoke();

        currentPhase += 1;
        core.TriggerPhaseTransitionStart(currentPhase);
        OnPhaseChanged(currentPhase);

        yield return new WaitForSeconds(phaseTransitionDelay);

        yield return StartCoroutine(RecoverStatusRoutine(maxHP, maxMP, phaseTransitionHealDuration));
        yield return new WaitForSeconds(0.2f);
        yield return new WaitForSeconds(afterPhaseTransitionDelay);

        isTransitioningPhase = false;
        core.ChangeState(EnemyState.Idle);
    }

    protected virtual void OnPhaseChanged(int newPhase)
    {
        // 특정 보스의 애니메이션, 모델 교체, 이펙트 재생 등을 여기에 구현
    }

    private IEnumerator RecoverStatusRoutine(float targetHP, float targetMP, float duration)
    {
        float startHP = currentHp;
        float startMP = currentMp;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            currentHp = Mathf.Lerp(startHP, targetHP, t);
            currentMp = Mathf.Lerp(startMP, targetMP, t);

            core.TriggerHpChanged(currentHp, maxHP);
            core.TriggerMpChanged(currentMp, maxMP);

            UIManager.Instance.UpdateBossHpUI(currentHp / maxHP);
            UIManager.Instance.UpdateBossMpUI(currentMp / maxMP);

            yield return null;
        }

        currentHp = targetHP;
        currentMp = targetMP;

        core.TriggerHpChanged(currentHp, maxHP);
        core.TriggerMpChanged(currentMp, maxMP);
        UIManager.Instance.UpdateBossHpUI(currentHp / maxHP);
        UIManager.Instance.UpdateBossMpUI(currentMp / maxMP);
    }

    private IEnumerator StunRoutine()
    {
        core.TriggerStun();
        if (Player.Instance != null) SpecialSkillManager.Instance.Trigger(Player.Instance.transform, SkillBase.TriggerType.OnEnemyParryStuned);

        float elapsed = 0f;
        float startMP = currentMp;
        while (elapsed < stunDuration)
        {
            elapsed += Time.deltaTime;
            UpdateMP(Mathf.Lerp(startMP, maxMP, elapsed / stunDuration));
            yield return null;
        }
        UpdateMP(maxMP);
        core.TriggerStunEnd();
    }

    public void UpdateMP(float value)
    {
        currentMp = Mathf.Clamp(value, 0, maxMP);
        core.TriggerMpChanged(currentMp, maxMP);
        UIManager.Instance.UpdateBossMpUI(currentMp / maxMP);
    }

    private void ResetHealth()
    {
        isTransitioningPhase = false;
        currentPhase = 1;
        currentHp = maxHP;
        currentMp = maxMP;

        core.TriggerHpChanged(currentHp, maxHP);
        core.TriggerMpChanged(currentMp, maxMP);
        UIManager.Instance.UpdateBossHpUI(currentHp / maxHP);
        UIManager.Instance.UpdateBossMpUI(currentMp / maxMP);
    }

    private bool HasEnoughMP(float requiredMp) => currentMp >= requiredMp;
    private void ConsumeMP(float amount) => UpdateMP(currentMp - amount);
}