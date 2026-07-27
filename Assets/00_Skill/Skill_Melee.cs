using System;
using System.Collections.Generic;
using Unity.VisualScripting.FullSerializer;
using UnityEngine;

#region Hit Data Structure
[Serializable]
public class MeleeHitData
{
    public Vector3 centerOffset;
    public Vector3 boxSize = Vector3.one;
    public Vector3 boxRotation;
    public float damageMultiplier = 1f;
    public float knockbackForce = 0f;
    public AudioClip hitSound;

    public SkillBase.EnemyHitEffectDirection effectDirection;
    public HitEffectData playerHitEffect;

    public bool useGuardSettings = false;
    public bool useStunSettings = false;
    public GuardableData guardData;
    public StunEffectData stunData;

    public bool useAreaSpawn = false;
    public GameObject areaPrefab;
    public Vector3 areaOffset;
    public float areaDuration = 6f;
    public float areaTickInterval = 1f;
    public float areaDamageMultiplier = 1f;
}
#endregion

[CreateAssetMenu(menuName = "Skills/Attack")]
public class Skill_Melee : SkillBase
{
    [Header("Melee Hit Sequence")]
    public List<MeleeHitData> hitDataList = new List<MeleeHitData>();

    private const float AnglePerClockHour = 30f;
    private const float ClockRotationYOffset = 90f;

    #region Editor Synchronization
    private void OnValidate()
    {
        if (hitDataList == null || hitDataList.Count == 0) return;

        int count = hitDataList.Count;
        damageMultipliers = new float[count];
        HitSound = new AudioClip[count];
        enemyHitEffectDirections = new EnemyHitEffectDirection[count];
        playerHitEffects = new HitEffectData[count];
        for (int i = 0; i < count; i++)
        {

            damageMultipliers[i] = hitDataList[i].damageMultiplier;
            HitSound[i] = hitDataList[i].hitSound;
            enemyHitEffectDirections[i] = hitDataList[i].effectDirection;
            playerHitEffects[i] = hitDataList[i].playerHitEffect;
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
        if (hitDataList == null || hitIndex < 0 || hitIndex >= hitDataList.Count)
        {
            return new List<IDamageable>();
        }
        Physics.SyncTransforms();
        MeleeHitData hitData = hitDataList[hitIndex];
        bool isCasterPlayer = casterTransform.GetComponent<Player>() != null;
        int targetLayerMask = isCasterPlayer ? LayerMask.GetMask("Enemy") : LayerMask.GetMask("Player");

        Vector3 worldCenter = casterTransform.position + (casterTransform.rotation * hitData.centerOffset);
        Quaternion orientation = casterTransform.rotation * Quaternion.Euler(hitData.boxRotation);

        Collider[] hitColliders = Physics.OverlapBox(worldCenter, hitData.boxSize / 2, orientation, targetLayerMask);

        List<IDamageable> hitTargets = new List<IDamageable>();

        foreach (Collider col in hitColliders)
        {
            if (col.TryGetComponent(out IDamageable target))
            {
                if (casterTransform != null && col.transform == casterTransform) continue;

                Vector3 hitPoint = col.ClosestPoint(worldCenter);
                float finalDamage = baseDamage * hitData.damageMultiplier;

                // 💡 적의 가드 상태 확인
                bool isGuarding = false;
                if (col.TryGetComponent(out EnemyCore enemyCore))
                {
                    isGuarding = enemyCore.isGuarding;
                }
                if(col.TryGetComponent(out Player player))
                {
                    isGuarding = player.isParrying;
                }
                // 데미지 처리는 그대로 진행 (Health 스크립트에서 가드 성공 이벤트를 트리거할 수 있도록)
                target.TakeDamage(finalDamage, this, casterTransform, hitData.playerHitEffect, hitIndex);

                // 💡 가드 상태가 아닐 때만 힛 이펙트 및 넉백 적용
                if (!isGuarding)
                {
                    if (isCasterPlayer)
                        SkillEffectManager.Instance.PlayHitEffect(col.gameObject, this, hitPoint, CalculateEffectRotation(enemyHitEffectDirections[hitIndex], casterTransform, hitPoint, col.transform), hitIndex);

                    if (hitData.knockbackForce > 0f)
                    {
                        if (target.transform.TryGetComponent<PlayerMove>(out var playerMove))
                            playerMove.KnockBack(hitData.knockbackForce, casterTransform.forward);
                        // else if (target.transform.TryGetComponent<EnemyCore>(out var enemy))
                        //     enemy.KnockBack(hitData.knockbackForce, casterTransform.forward);
                    }
                }

                hitTargets.Add(target);
            }
        }

        if (hitData.useAreaSpawn && hitData.areaPrefab != null)
        {
            Vector3 areaWorldPos = casterTransform.position + (casterTransform.rotation * hitData.areaOffset);
            GameObject areaObject = Instantiate(hitData.areaPrefab, areaWorldPos, Quaternion.identity);

            if (!areaObject.TryGetComponent<AreaField>(out var areaField))
                areaField = areaObject.AddComponent<AreaField>();
            areaField.transform.position = casterTransform.position + (casterTransform.rotation * hitData.areaOffset);
            areaField.Setup(this, casterTransform, hitData.areaDuration, hitData.areaTickInterval, hitData.areaDamageMultiplier, targetLayerMask, hitIndex);
        }

        return hitTargets;
    }

    private Quaternion CalculateEffectRotation(EnemyHitEffectDirection effectDir, Transform casterTransform, Vector3 hitPoint, Transform targetTransform)
    {
        int dirIndex = (int)effectDir;
        if (dirIndex <= 11)
        {
            Vector3 dirToEnemy = targetTransform.position - casterTransform.position;
            dirToEnemy.y = 0;
            if (dirToEnemy == Vector3.zero) dirToEnemy = casterTransform.forward;

            float rotY = Quaternion.LookRotation(dirToEnemy, casterTransform.up).eulerAngles.y;
            float rotX = dirIndex * AnglePerClockHour;
            return Quaternion.Euler(rotX, rotY + ClockRotationYOffset, 0f);
        }

        Vector3 effectDirection = casterTransform.forward;
        Vector3 effectUp = casterTransform.up;

        if (effectDir == EnemyHitEffectDirection.Forward)
        {
            effectDirection = Quaternion.AngleAxis(0, casterTransform.forward) * casterTransform.up;
            effectUp = -casterTransform.forward;
        }
        else if (effectDir == EnemyHitEffectDirection.Backward)
        {
            effectDirection = Quaternion.AngleAxis(0, -casterTransform.forward) * casterTransform.up;
            effectUp = casterTransform.forward;
        }

        if (effectDirection == Vector3.zero) effectDirection = casterTransform.forward;
        return Quaternion.LookRotation(effectDirection, effectUp);
    }

    public override void DrawSkillGizmos(Transform casterTransform)
    {
        if (hitDataList == null) return;

        Matrix4x4 originalMatrix = Gizmos.matrix;
        for (int i = 0; i < hitDataList.Count; i++)
        {
            var hitData = hitDataList[i];
            Gizmos.color = Color.HSVToRGB((float)i / hitDataList.Count, 0.8f, 1f);

            Vector3 worldCenter = casterTransform.position + (casterTransform.rotation * hitData.centerOffset);
            Quaternion worldRotation = casterTransform.rotation * Quaternion.Euler(hitData.boxRotation);

            Gizmos.matrix = Matrix4x4.TRS(worldCenter, worldRotation, Vector3.one);

            Gizmos.DrawWireCube(Vector3.zero, hitData.boxSize);
        }
        Gizmos.matrix = originalMatrix;
    }
    #endregion
}