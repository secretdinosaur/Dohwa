using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;

#region Auxiliary Classes
[Serializable]
public class StunEffectData
{
    public Player.StunType stunType;
    public Vector3 offset;
    public bool isRealtimeFollow = false;
    public float stunDuration;
}

[Serializable]
public class GuardableData
{
    public bool guardable = true;
    public bool parryable = true;
}
#endregion

public abstract class SkillBase : ScriptableObject
{
    public enum Command { W, A, S, D, J }
    public enum EnemyHitEffectDirection { H12, H1, H2, H3, H4, H5, H6, H7, H8, H9, H10, H11, Forward, Backward }
    public enum TriggerType { None, OnParry, OnEnemyParryStuned }

    [Header("Upgrade & System Flags")]
    public bool isEnemySkill = false;

    [HideInInspector] public bool isUpgraded = false;
    [HideInInspector] public SkillBase UpgradedSkill;

    public Command[] commands;
    public float MP_cost;

    [Header("Assets")]
    public AnimationClip clip;
    public Sprite icon;
    public AudioClip sound;
    public HitEffect enemyHitEffect;
    public SkillEffect effect;
    public SkillEffect SpecialGuardEffect;
    
    [Header("Localization")]
    public LocalizedString Localize_skillName;
    public LocalizedString Localize_skillItemDesc;
    public LocalizedString Localize_skillEffectDesc;

    public string skillName => Localize_skillName.GetLocalizedString();
    public string skillItemDesc => Localize_skillItemDesc.GetLocalizedString();
    public string skillEffectDesc => Localize_skillEffectDesc.GetLocalizedString();

    [HideInInspector] public float[] damageMultipliers;
    [HideInInspector] public AudioClip[] HitSound;
    [HideInInspector] public HitEffectData[] playerHitEffects;
    [HideInInspector] public EnemyHitEffectDirection[] enemyHitEffectDirections;
    
    public bool isSpecialSkill = false;
    public TriggerType triggerType = TriggerType.None;
    public float triggerThreshold = 0.5f;
    public float triggerCooldown = 5f;
    [Range(0f, 1f)] public float triggerChance = 1f;
    public int triggerMaxUsesPerCaster = 0;

    #region Public Methods
    public virtual void OnTriggered(Transform caster)
    {
        if (caster == null)
        {
            Activate(null);
            return;
        }
        caster.TryGetComponent<EnemyCore>(out var e);
        Activate(caster);
    }

    public virtual void DrawSkillGizmos(Transform casterTransform)
    {
    }
    public abstract GuardableData GetGuardData(int hitIndex);
    public abstract StunEffectData GetStunData(int hitIndex);

    public abstract SoundObject Activate(Transform casterTransform);
    public abstract List<IDamageable> ExecuteHitCheck(Transform casterTransform, float baseDamage, int hitIndex);
    #endregion
}