using System;
using System.Collections.Generic;
using UnityEngine;

#region Hit Data Structure
[Serializable]
public class RangeHitData
{
    public Vector3 spawnOffset;
    public float damageMultiplier = 1f;
    public Vector3 knockbackForce = Vector3.zero;
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

        // 💡 수정됨: 기본 방향은 정면이지만, 타겟이 있다면 타겟의 가슴(중심) 높이를 향하도록 상하 각도 보정
        Vector3 shootDirection = casterTransform.forward;

        if (casterTransform.GetComponent<EnemyCore>() != null && Player.Instance != null)
        {
            // 적이 플레이어에게 쏠 때 (플레이어 중심점 Y 보정, 보통 1.0f 높이)
            Vector3 targetPos = Player.Instance.transform.position + (Vector3.up * 1.0f);
            shootDirection = (targetPos - spawnPos).normalized;
        }
        else if (casterTransform.GetComponent<Player>() != null)
        {
            // 플레이어가 적에게 쏠 때 (락온된 타겟이 있다면 그곳으로)
            if (EnemyLockOnImage.Instance != null && EnemyLockOnImage.Instance.target != null)
            {
                shootDirection = (EnemyLockOnImage.Instance.target.position - spawnPos).normalized;
            }
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

            if (isCasterPlayer)
                SkillEffectManager.Instance.PlayHitEffect(targetCollider.gameObject, this, hitPoint, projectileGo.transform.rotation, hitIndex);

            float finalDamage = baseDamage * hitData.damageMultiplier;
            target.TakeDamage(finalDamage, this, casterTransform, hitData.playerHitEffect, hitIndex);

            if (hitData.knockbackForce.magnitude > 0f)
            {
                if (targetCollider.transform.TryGetComponent<PlayerMove>(out var playerMove))
                {
                    Vector3 worldKnockback = projectileGo.transform.rotation * hitData.knockbackForce;
                    playerMove.KnockBack(worldKnockback);
                }
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