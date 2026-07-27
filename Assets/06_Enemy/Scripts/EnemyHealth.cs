using UnityEngine;
using System;
using System.Collections;

public class EnemyHealth : MonoBehaviour
{
    public float maxHP = 100f;
    public float maxMP = 100f;
    public float mpRecoveryPerSecond = 5f;
    public float stunDuration = 3f;

    private float currentHp, currentMp;
    private EnemyCore core;
    private HpBarCanvas hpBarCanvas;
    private DecalGenerator decalGenerator;

    private bool hasTriggered66 = false;
    private bool hasTriggered33 = false;

    public event Action<float, float> OnHpChanged;
    public event Action<float, float> OnMpChanged;

    private void Awake()
    {
        core = GetComponent<EnemyCore>();
        hpBarCanvas = GetComponentInChildren<HpBarCanvas>();
        decalGenerator = GetComponent<DecalGenerator>();
        currentHp = maxHP;
        currentMp = maxMP;
    }

    private void OnEnable()
    {
        if(hpBarCanvas != null)
            hpBarCanvas.gameObject.SetActive(false);

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
        if (!core.IsDead && core.currentState != EnemyState.Stun && currentMp < maxMP)
            UpdateMP(currentMp + (mpRecoveryPerSecond * Time.deltaTime));
    }

    private void HandleDamage(float damage)
    {
        if(hpBarCanvas != null)
            hpBarCanvas.gameObject.SetActive(true);
            
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

        OnHpChanged?.Invoke(currentHp, maxHP);
        core.PlayHitSound(core.HittedSkill, core.HitIndex);

        if (hpBarCanvas != null)
            hpBarCanvas.UpdateHP(currentHp, maxHP);
        
        if (currentHp <= 0) 
        { 
            GradeManager.Instance.GetExp(GradeManager.ExpGainBehavior.KillEnemy);  
            core.TriggerDeath(); 
            hpBarCanvas.gameObject.SetActive(false); 
        }
    }

    private void HandleParryDamage(float mpDamage)
    {
        UpdateMP(0);

        if (currentMp <= 0 && core.currentState != EnemyState.Stun)
        {
            core.TriggerParried();
            StartCoroutine(StunRoutine());
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
        OnMpChanged?.Invoke(currentMp, maxMP);
        hpBarCanvas.UpdateMP(currentMp, maxMP);
    }

    private void ResetHealth()
    {
        currentHp = maxHP; currentMp = maxMP;
        hasTriggered66 = false;
        hasTriggered33 = false;
        
        OnHpChanged?.Invoke(currentHp, maxHP);
        OnMpChanged?.Invoke(currentMp, maxMP);
        
        hpBarCanvas.UpdateHP(currentHp, maxHP);
        hpBarCanvas.UpdateMP(currentMp, maxMP);

        hpBarCanvas.gameObject.SetActive(false);
    }

    private bool HasEnoughMP(float requiredMp) => currentMp >= requiredMp;
    private void ConsumeMP(float amount) => UpdateMP(currentMp - amount);
}