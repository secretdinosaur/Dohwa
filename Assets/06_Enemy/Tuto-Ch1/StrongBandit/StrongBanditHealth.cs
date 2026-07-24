using UnityEngine;
using System;
using System.Collections;

public class StrongBanditHealth : MonoBehaviour
{
    public float maxHP = 100f;
    public float maxMP = 100f;
    public float mpRecoveryPerSecond = 5f;
    public float stunDuration = 3f;

    [Header("Phase Settings")]
    public int maxPhase = 1;
    public int currentPhase = 1;
    public float phaseTransitionDelay = 7f;
    public float phaseTransitionHealDuration = 4f;

    private float currentHp, currentMp;
    private EnemyCore core;
    private bool isTransitioningPhase = false;

    // 개별 HP 바 참조
    private HpBarCanvas hpBarCanvas;

    private void Awake()
    {
        core = GetComponent<EnemyCore>();
        hpBarCanvas = GetComponentInChildren<HpBarCanvas>();

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

        core.PlayHitSound(core.HittedSkill, core.HitIndex);

        currentHp -= damage;
        core.TriggerHpChanged(currentHp, maxHP);

        if (hpBarCanvas != null)
            hpBarCanvas.UpdateHP(currentHp, maxHP);

        if (currentHp <= 0)
        {
            if (currentPhase < maxPhase)
            {
                currentHp = 0;
                StartCoroutine(PhaseTransitionRoutine());
            }
            else
            {
                if (GradeManager.Instance != null)
                    GradeManager.Instance.GetExp(GradeManager.ExpGainBehavior.KillEnemy);

                core.TriggerDeath();

                if (hpBarCanvas != null)
                    hpBarCanvas.gameObject.SetActive(false);
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

        isTransitioningPhase = false;
        core.ChangeState(EnemyState.Idle);
    }

    protected virtual void OnPhaseChanged(int newPhase)
    {
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

            if (hpBarCanvas != null)
            {
                hpBarCanvas.UpdateHP(currentHp, maxHP);
                hpBarCanvas.UpdateMP(currentMp, maxMP);
            }

            yield return null;
        }

        currentHp = targetHP;
        currentMp = targetMP;

        core.TriggerHpChanged(currentHp, maxHP);
        core.TriggerMpChanged(currentMp, maxMP);

        if (hpBarCanvas != null)
        {
            hpBarCanvas.UpdateHP(currentHp, maxHP);
            hpBarCanvas.UpdateMP(currentMp, maxMP);
        }
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

        if (hpBarCanvas != null)
            hpBarCanvas.UpdateMP(currentMp, maxMP);
    }

    private void ResetHealth()
    {
        isTransitioningPhase = false;
        currentPhase = 1;
        currentHp = maxHP;
        currentMp = maxMP;
        core.TriggerHpChanged(currentHp, maxHP);
        core.TriggerMpChanged(currentMp, maxMP);

        if (hpBarCanvas != null)
        {
            hpBarCanvas.UpdateHP(currentHp, maxHP);
            hpBarCanvas.UpdateMP(currentMp, maxMP);
            hpBarCanvas.gameObject.SetActive(false);
        }
    }

    private bool HasEnoughMP(float requiredMp) => currentMp >= requiredMp;
    private void ConsumeMP(float amount) => UpdateMP(currentMp - amount);
}