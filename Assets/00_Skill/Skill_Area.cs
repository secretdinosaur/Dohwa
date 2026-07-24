using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Skills/Area")]
public class Skill_Area : SkillBase
{
    [Header("Area Settings")]
    public GameObject areaPrefab;
    public Vector3 offset;

    [Header("Tuning Settings")]
    [SerializeField] private float defaultDuration = 6f;
    [SerializeField] private float defaultTickInterval = 1f;

    public override SoundObject Activate(Transform casterTransform)
    {
        if (sound) return SoundManager.Instance.PlaySound(sound);
        return null;
    }
    public override GuardableData GetGuardData(int hitIndex)
    {
        return null;
    }

    public override StunEffectData GetStunData(int hitIndex)
    {
        return null;
    }
    public override List<IDamageable> ExecuteHitCheck(Transform casterTransform, float baseDamage, int hitIndex)
    {
        if (casterTransform == null) return new List<IDamageable>();

        Vector3 targetPosition = casterTransform.position;

        if (casterTransform.GetComponent<EnemyCore>() != null && Player.Instance != null)
        {
            targetPosition = Player.Instance.transform.position;
        }

        ActivateAtPosition(targetPosition, casterTransform, baseDamage, hitIndex);
        return new List<IDamageable>();
    }

    public void ActivateAtPosition(Vector3 worldPos, Transform caster, float baseDamage, int hitIndex)
    {
        if (areaPrefab == null) return;

        Vector3 worldOffset = caster.rotation * offset;
        GameObject areaObject = Object.Instantiate(areaPrefab, worldPos + worldOffset, Quaternion.identity);

        if (!areaObject.TryGetComponent<AreaField>(out var areaField))
        {
            areaField = areaObject.AddComponent<AreaField>();
        }

        bool isCasterPlayer = caster.GetComponent<Player>() != null;
        LayerMask targetLayer = isCasterPlayer ? LayerMask.GetMask("Enemy") : LayerMask.GetMask("Player");

        float dmgMultiplier = (damageMultipliers != null && hitIndex < damageMultipliers.Length) ? damageMultipliers[hitIndex] : 1f;
        areaField.Setup(this, caster, defaultDuration, defaultTickInterval, dmgMultiplier, targetLayer, hitIndex);
    }
}