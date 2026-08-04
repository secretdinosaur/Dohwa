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

    [Header("Hit Settings (Required to prevent AI Freeze)")]
    public float damageMultiplier = 1f;
    public AudioClip hitSound;
    public HitEffectData playerHitEffect;
    public EnemyHitEffectDirection effectDirection;
    public GuardableData guardData;
    public StunEffectData stunData;

    // 💡 런타임 시에도 무조건 배열이 초기화되도록 보장
    private void OnEnable()
    {
        InitArrays();
    }

    private void OnValidate()
    {
        InitArrays();
    }

    private void InitArrays()
    {
        damageMultipliers = new float[1] { damageMultiplier };
        HitSound = new AudioClip[1] { hitSound };
        HitEffects = new HitEffectData[1] { playerHitEffect };
        enemyHitEffectDirections = new EnemyHitEffectDirection[1] { effectDirection };
    }

    public override SkillEffect Activate(Transform casterTransform)
    {
        return SkillEffectManager.Instance.PlaySkillEffect(casterTransform, this);
    }

    public override GuardableData GetGuardData(int hitIndex)
    {
        return guardData;
    }

    public override StunEffectData GetStunData(int hitIndex)
    {
        return stunData;
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