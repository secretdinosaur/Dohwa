using System;
using System.Collections.Generic;
using System.Net;
using UnityEngine;

#region Hit Data Structure
[Serializable]
public class RangeHitData
{
    public Vector3 spawnOffset;
    public float damageMultiplier = 1f;
    public float knockbackForce = 0f;
    public AudioClip hitSound;
    public HitEffectData playerHitEffect;

    public bool useGuardSettings = false;
    public bool useStunSettings = false;
    public GuardableData guardData;
    public StunEffectData stunData;
}
#endregion

[CreateAssetMenu(menuName = "Skills/Range")]
public class Skill_Range : SkillBase
{
    #region Projectile Settings
    [Header("Projectile Settings")]
    public GameObject projectilePrefab;
    public float range = 999f;

    public List<RangeHitData> hitDataList = new List<RangeHitData>();
    #endregion

    #region Editor Synchronization
    private void OnValidate()
    {
        if (hitDataList == null || hitDataList.Count == 0) return;

        int count = hitDataList.Count;
        damageMultipliers = new float[count];
        HitEffects = new HitEffectData[count];
        for (int i = 0; i < count; i++)
        {
            damageMultipliers[i] = hitDataList[i].damageMultiplier;
            HitEffects[i] = hitDataList[i].playerHitEffect;
        }
    }
    #endregion

    #region Core Logic
    public override SkillEffect Activate(Transform casterTransform)
    {
        return SkillEffectManager.Instance.PlaySkillEffect(casterTransform, this);
    }

    public override GuardableData GetGuardData(int hitIndex)
    {
        if (hitDataList[hitIndex].useGuardSettings)
            return hitDataList[hitIndex].guardData;
        else
            return null;
    }

    public override StunEffectData GetStunData(int hitIndex)
    {
        if (hitDataList[hitIndex].useStunSettings)
            return hitDataList[hitIndex].stunData;
        else
            return null;
    }

    public override List<IDamageable> ExecuteHitCheck(Transform casterTransform, float baseDamage, int hitIndex)
    {
        if (projectilePrefab == null || hitDataList == null || hitIndex < 0 || hitIndex >= hitDataList.Count)
            return new List<IDamageable>();

        RangeHitData hitData = hitDataList[hitIndex];
        Vector3 spawnPos = casterTransform.position + (casterTransform.rotation * hitData.spawnOffset);

        Vector3 shootDirection = casterTransform.forward;
        if (casterTransform.TryGetComponent<EnemyCore>(out var enemy) && Player.Instance != null)
        {
            shootDirection = ((Player.Instance.transform.position + Vector3.up * 1.5f) - spawnPos);
        }

        float finalDamage = baseDamage * hitData.damageMultiplier;

        GameObject go = Instantiate(projectilePrefab, spawnPos, Quaternion.LookRotation(shootDirection));
        if (go.TryGetComponent<Projectile>(out var proj))
        {
            proj.Init(casterTransform.gameObject, finalDamage, proj.Speed, this, hitIndex);
            proj.ShootToPoint(spawnPos + shootDirection * range);
        }

        return new List<IDamageable>();
    }

    public void OnProjectileHit(GameObject projectileGo, Collider targetCollider, Transform casterTransform, float baseDamage, int hitIndex)
    {
        if (hitDataList == null || hitIndex < 0 || hitIndex >= hitDataList.Count) return;
        RangeHitData hitData = hitDataList[hitIndex];

        if (targetCollider.TryGetComponent<IDamageable>(out var target))
        {

            Vector3 hitPoint = targetCollider.ClosestPoint(projectileGo.transform.position);
            bool isCasterPlayer = casterTransform.GetComponent<Player>() != null;

            if(isCasterPlayer)
                SkillEffectManager.Instance.PlayHitEffect(targetCollider.gameObject, this, hitPoint, projectileGo.transform.rotation, hitIndex);

            float finalDamage = baseDamage * hitData.damageMultiplier;
            target.TakeDamage(finalDamage, this, casterTransform, hitData.playerHitEffect, hitIndex);

            if (hitData.knockbackForce > 0f)
            {
                Vector3 pushDirection = projectileGo.transform.forward;
                pushDirection.y = 0f;

                if (targetCollider.transform.TryGetComponent<PlayerMove>(out var playerMove))
                    playerMove.KnockBack(hitData.knockbackForce, pushDirection);
                //else if (targetCollider.transform.TryGetComponent<EnemyCore>(out var enemy))
                    //enemy.KnockBack(hitData.knockbackForce);

            }
        }
    }
    #endregion

    #region Projectile Helpers
    public override void DrawSkillGizmos(Transform casterTransform)
    {
        if (hitDataList == null) return;

        Gizmos.color = Color.cyan;
        for (int i = 0; i < hitDataList.Count; i++)
        {
            Vector3 startPos = casterTransform.position + (casterTransform.rotation * hitDataList[i].spawnOffset);
            Vector3 endPos = startPos + casterTransform.forward * range;
            Gizmos.DrawLine(startPos, endPos);
        }
    }
    #endregion
}