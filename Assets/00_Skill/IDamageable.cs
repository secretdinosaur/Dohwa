using UnityEngine;

public interface IDamageable
{
    Transform transform { get; }
    void TakeDamage(float damage, SkillBase skill, Transform caster, HitEffectData hitEffect = null, int hitIndex = 0);
}