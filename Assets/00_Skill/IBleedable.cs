using UnityEngine;

public interface IBleedable
{
    void ApplyBleed(int ticksPerSecond, SkillBase bleedSkill, HitEffectData bleedEffect, SkillBase.EnemyHitEffectDirection effectDir);
}