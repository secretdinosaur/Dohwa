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
    private DecalGenerator decalGenerator;
    private bool isTransitioningPhase = false;

    private bool hasTriggered66 = false;
    private bool hasTriggered33 = false;

    private Coroutine stunCoroutine;

    private void Awake()
    {
        core = GetComponent<EnemyCore>();
        decalGenerator = GetComponent<DecalGenerator>();
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
        core.QueryCurrentHP = () => currentHp;
    }

    private void OnDisable()
    {
        core.OnDamageTaken -= HandleDamage;
        core.OnParryDamageTaken -= HandleParryDamage;
        core.OnRevived -= ResetHealth;

        core.QueryHasEnoughMP = null;
        core.CmdConsumeMP -= ConsumeMP;
        core.QueryCurrentHP = null;
    }

    private void Update()
    {
        if (core.isCulled) return;
        if (!core.IsDead && core.currentState != EnemyState.Stun && currentMp < maxMP && !isTransitioningPhase)
            UpdateMP(currentMp + (mpRecoveryPerSecond * Time.deltaTime));

        if (UIManager.Instance != null && UIManager.Instance.BossStatus != null)
        {
            // 페이즈 전환 중이거나 죽었을 때는 무조건 숨김
            if (isTransitioningPhase || core.IsDead)
            {
                if (UIManager.Instance.BossStatus.gameObject.activeSelf)
                    UIManager.Instance.BossStatus.gameObject.SetActive(false);
            }
        }
    }

    private void HandleDamage(float damage)
    {
        if (isTransitioningPhase) return;

        if (core.isGuarding)
        {
            core.TriggerGuardSuccess();
            return;
        }

        if (core.currentState == EnemyState.Stun)
        {
            if (stunCoroutine != null)
            {
                StopCoroutine(stunCoroutine);
                stunCoroutine = null;
            }
            UpdateMP(maxMP);
            core.TriggerStunEnd();
        }

        currentHp -= damage;

        float hpPercentage = currentHp / maxHP;
        if (!hasTriggered66 && hpPercentage <= 0.66f)
        {
            hasTriggered66 = true;
            if (decalGenerator != null) decalGenerator.GenerateWoundsByPercentage(0.3f);
        }
        if (!hasTriggered33 && hpPercentage <= 0.33f)
        {
            hasTriggered33 = true;
            if (decalGenerator != null) decalGenerator.GenerateWoundsByPercentage(0.3f);
        }

        core.TriggerHpChanged(currentHp, maxHP);

        if (UIManager.Instance != null)
            UIManager.Instance.UpdateBossHpUI(currentHp / maxHP);

        if (!core.isTakingDoT)
        {
            core.PlayHitSound(core.HittedSkill, core.HitIndex);
        }

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
            if (stunCoroutine != null) StopCoroutine(stunCoroutine);
            stunCoroutine = StartCoroutine(StunRoutine());
        }
    }

    private IEnumerator PhaseTransitionRoutine()
    {
        isTransitioningPhase = true;

        core.ChangeState(EnemyState.Dead);
        core.CmdStopMovement?.Invoke();

        currentPhase += 1;
        hasTriggered66 = false;
        hasTriggered33 = false;

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

            if (UIManager.Instance != null)
            {
                UIManager.Instance.UpdateBossHpUI(currentHp / maxHP);
                UIManager.Instance.UpdateBossMpUI(currentMp / maxMP);
            }

            yield return null;
        }

        currentHp = targetHP;
        currentMp = targetMP;

        core.TriggerHpChanged(currentHp, maxHP);
        core.TriggerMpChanged(currentMp, maxMP);

        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateBossHpUI(currentHp / maxHP);
            UIManager.Instance.UpdateBossMpUI(currentMp / maxMP);
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
        stunCoroutine = null;
    }

    public void UpdateMP(float value)
    {
        currentMp = Mathf.Clamp(value, 0, maxMP);
        core.TriggerMpChanged(currentMp, maxMP);

        if (UIManager.Instance != null)
            UIManager.Instance.UpdateBossMpUI(currentMp / maxMP);
    }

    private void ResetHealth()
    {
        isTransitioningPhase = false;
        currentPhase = 1;
        currentHp = maxHP;
        currentMp = maxMP;

        hasTriggered66 = false;
        hasTriggered33 = false;

        if (stunCoroutine != null)
        {
            StopCoroutine(stunCoroutine);
            stunCoroutine = null;
        }

        core.TriggerHpChanged(currentHp, maxHP);
        core.TriggerMpChanged(currentMp, maxMP);

        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateBossHpUI(currentHp / maxHP);
            UIManager.Instance.UpdateBossMpUI(currentMp / maxMP);
        }
    }

    private bool HasEnoughMP(float requiredMp) => currentMp >= requiredMp;
    private void ConsumeMP(float amount) => UpdateMP(currentMp - amount);
}