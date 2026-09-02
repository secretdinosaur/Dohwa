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
        // 💡 [디버그 1] 데미지 함수 진입 여부와 전달된 데미지 값 확인
        Debug.Log($"<color=cyan>[StrongBanditHealth] HandleDamage 진입! 전달받은 데미지: {damage} | 현재 체력: {currentHp} | 가드중 여부: {core.isGuarding} | 현재 상태: {core.currentState}</color>");

        if (isTransitioningPhase)
        {
            Debug.LogWarning("<color=red>[피격 무시] 페이즈 전환 중이라 데미지가 무시되었습니다.</color>");
            return;
        }

        if (core.isGuarding)
        {
            bool isFacingPlayer = false;

            if (Player.Instance != null)
            {
                Vector3 dirToPlayer = (Player.Instance.transform.position - transform.position).normalized;
                dirToPlayer.y = 0;
                Vector3 forward = transform.forward;
                forward.y = 0;

                float angle = Vector3.Angle(forward, dirToPlayer);
                if (angle <= 75f)
                {
                    isFacingPlayer = true;
                }
                Debug.Log($"<color=yellow>[가드 각도 체크] 플레이어와의 각도: {angle}도 (정면 여부: {isFacingPlayer})</color>");
            }

            if (isFacingPlayer)
            {
                Debug.Log("<color=green>[가드 성공!] 정면 공격이므로 데미지를 씹고 카운터를 시도합니다.</color>");
                core.TriggerGuardSuccess();
                return;
            }
            else
            {
                Debug.Log("<color=magenta>[가드 무효화] 측/후면 공격이므로 가드를 강제로 해제하고 데미지를 받습니다.</color>");
                core.isGuarding = false;
            }
        }

        if (core.currentState == EnemyState.Stun)
        {
            Debug.Log("<color=white>[피격] 스턴 상태에서 피격당함. 스턴 해제 처리</color>");
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
            core.PlayHitSound?.Invoke(core.HittedSkill, core.HitIndex);
        }

        // 실제 체력 차감 연산
        float prevHp = currentHp;
        currentHp -= damage;
        Debug.Log($"<color=green>[체력 차감 성공!] 기존 체력: {prevHp} -> 깎인 후 체력: {currentHp} (차감된 양: {damage})</color>");

        OnHpChanged?.Invoke(currentHp, maxHP);

        if (hpBarCanvas != null)
            hpBarCanvas.UpdateHP(currentHp, maxHP);

        if (currentHp <= 0)
        {
            Debug.Log("<color=red>[사망/페이즈 전환] 체력이 0 이하가 되었습니다.</color>");
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