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

    private HpBarCanvas hpBarCanvas;
    private Coroutine stunCoroutine;

    // 💡 EnemyHealth 처럼 글로벌 코어가 아닌 로컬 이벤트를 사용하도록 추가
    public event Action<float, float> OnHpChanged;
    public event Action<float, float> OnMpChanged;

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
        // 💡 여기서 MP가 회복될 때마다 불필요한 과부하가 걸리지 않도록 로컬 이벤트 사용
        if (!core.IsDead && core.currentState != EnemyState.Stun && currentMp < maxMP && !isTransitioningPhase)
            UpdateMP(currentMp + (mpRecoveryPerSecond * Time.deltaTime));

        if (hpBarCanvas != null)
        {
            if (isTransitioningPhase || core.IsDead)
            {
                if (hpBarCanvas.gameObject.activeSelf)
                    hpBarCanvas.gameObject.SetActive(false);
            }
            else
            {
                bool isLockedOn = EnemyLockOnImage.Instance != null &&
                                  EnemyLockOnImage.Instance.target != null &&
                                  EnemyLockOnImage.Instance.target.IsChildOf(transform);

                if (isLockedOn || currentHp < maxHP)
                {
                    if (!hpBarCanvas.gameObject.activeSelf)
                        hpBarCanvas.gameObject.SetActive(true);
                }
                else
                {
                    if (hpBarCanvas.gameObject.activeSelf)
                        hpBarCanvas.gameObject.SetActive(false);
                }
            }
        }
    }

    private void HandleDamage(float damage)
    {
        if (isTransitioningPhase) return;

        if (core.isGuarding)
        {
            bool isFacingPlayer = false;

            if (Player.Instance != null)
            {
                Vector3 dirToPlayer = (Player.Instance.transform.position - transform.position).normalized;
                dirToPlayer.y = 0;
                Vector3 forward = transform.forward;
                forward.y = 0;

                if (Vector3.Angle(forward, dirToPlayer) <= 75f)
                {
                    isFacingPlayer = true;
                }
            }

            if (isFacingPlayer)
            {
                core.TriggerGuardSuccess();
                return;
            }
            else
            {
                core.isGuarding = false;
            }
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

        if (!core.isTakingDoT)
        {
            // 💡 널 레퍼런스 에러 방지를 위해 ?.Invoke 사용
            core.PlayHitSound?.Invoke(core.HittedSkill, core.HitIndex);
        }

        currentHp -= damage;

        // 💡 과부하 방지 (EnemyHealth와 동일하게 로컬 호출)
        OnHpChanged?.Invoke(currentHp, maxHP);

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

            OnHpChanged?.Invoke(currentHp, maxHP);
            OnMpChanged?.Invoke(currentMp, maxMP);

            if (hpBarCanvas != null)
            {
                hpBarCanvas.UpdateHP(currentHp, maxHP);
                hpBarCanvas.UpdateMP(currentMp, maxMP);
            }

            yield return null;
        }

        currentHp = targetHP;
        currentMp = targetMP;

        OnHpChanged?.Invoke(currentHp, maxHP);
        OnMpChanged?.Invoke(currentMp, maxMP);

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
        stunCoroutine = null;
    }

    public void UpdateMP(float value)
    {
        currentMp = Mathf.Clamp(value, 0, maxMP);

        // 💡 핵심 끊김 해결: core.TriggerMpChanged (글로벌) 호출을 지우고 로컬 호출로 변경
        OnMpChanged?.Invoke(currentMp, maxMP);

        if (hpBarCanvas != null)
            hpBarCanvas.UpdateMP(currentMp, maxMP);
    }

    private void ResetHealth()
    {
        isTransitioningPhase = false;
        currentPhase = 1;
        currentHp = maxHP;
        currentMp = maxMP;

        if (stunCoroutine != null)
        {
            StopCoroutine(stunCoroutine);
            stunCoroutine = null;
        }

        OnHpChanged?.Invoke(currentHp, maxHP);
        OnMpChanged?.Invoke(currentMp, maxMP);

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