using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class AreaField : MonoBehaviour
{
    #region Components & References
    [Header("Audio Settings")]
    public AudioSource[] audios;
    public AudioClip HitSound;

    private Collider areaCollider;
    #endregion

    #region Internal State Variables
    private SkillBase ownerSkill;
    private Transform caster;
    private float duration;
    private float tickInterval;
    private float tickDamage;
    private LayerMask targetLayer;

    private Coroutine tickRoutine;
    private int audioPlayCount = 0;

    int hitindex = 0;
    #endregion

    #region Constants
    private const float MinTickInterval = 0.05f;
    private const float DestroyDelayOffset = 0.2f;
    private const float PlayerDamageDivisionFactor = 100f;
    #endregion

    #region Setup & Lifecycle
    public void Setup(SkillBase skill, Transform casterTransform, float duration, float tickInterval, float damageMultiplier, LayerMask targetLayer, int hitindex)
    {
        this.ownerSkill = skill;
        this.caster = casterTransform;
        this.duration = duration;
        this.tickInterval = Mathf.Max(MinTickInterval, tickInterval);
        this.targetLayer = targetLayer;
        this.hitindex = hitindex;
        CalculateTickDamage(casterTransform, damageMultiplier);
        EnsureCollider();

        tickRoutine = StartCoroutine(TickRoutine());
        Destroy(gameObject, duration + DestroyDelayOffset);
    }

    private void CalculateTickDamage(Transform casterTransform, float multiplier)
    {
        if (casterTransform != null)
        {
            if (casterTransform.TryGetComponent<EnemyCore>(out var enemy))
            {
                tickDamage = multiplier;
            }
            else
            {
                tickDamage = (StatManager.Instance != null ? StatManager.Instance.AttackPower : 0f) * multiplier / PlayerDamageDivisionFactor;
            }
        }
        else
        {
            tickDamage = multiplier;
        }
    }

    private void EnsureCollider()
    {
        areaCollider = GetComponent<Collider>();
        if (areaCollider != null)
        {
            areaCollider.isTrigger = true;
        }
    }
    #endregion

    #region Tick & Hit Detection Logic
    private IEnumerator TickRoutine()
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            ApplyTick();
            yield return new WaitForSeconds(tickInterval);
            elapsed += tickInterval;
        }
    }

    private void ApplyTick()
    {
        if (areaCollider == null) return;

        if (areaCollider is SphereCollider sc)
        {
            Vector3 worldCenter = transform.TransformPoint(sc.center);
            float worldRadius = sc.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z);
            DoOverlapSphere(worldCenter, worldRadius);
        }
        else if (areaCollider is BoxCollider bc)
        {
            Vector3 worldCenter = transform.TransformPoint(bc.center);
            Vector3 halfExtents = Vector3.Scale(bc.size * 0.5f, transform.lossyScale);
            Quaternion orientation = transform.rotation;

            Collider[] cols = Physics.OverlapBox(worldCenter, halfExtents, orientation, targetLayer);
            ProcessColliders(cols);
        }
        else
        {
            Bounds b = areaCollider.bounds;
            float approxRadius = Mathf.Max(b.extents.x, b.extents.z);
            DoOverlapSphere(b.center, approxRadius);
        }
    }

    private void DoOverlapSphere(Vector3 center, float radius)
    {
        Collider[] cols = Physics.OverlapSphere(center, radius, targetLayer);
        ProcessColliders(cols);
    }

    private void ProcessColliders(Collider[] cols)
    {
        if (cols == null || cols.Length == 0) return;

        foreach (var col in cols)
        {
            if (col == null) continue;
            if (col.TryGetComponent<IDamageable>(out var target))
            {
                if (caster != null && col.transform == caster) continue;

                PlayHitSound();
                target.TakeDamage(tickDamage, ownerSkill, caster, null, hitindex);
            }
        }
    }

    private void PlayHitSound()
    {
        if (audios != null && audios.Length > 0 && HitSound != null)
        {
            audios[audioPlayCount % audios.Length].PlayOneShot(HitSound);
            audioPlayCount++;
        }
    }
    #endregion

    #region Debug Gizmos
    private void OnDrawGizmosSelected()
    {
        if (areaCollider is SphereCollider sc)
        {
            Vector3 worldCenter = transform.TransformPoint(sc.center);
            float worldRadius = sc.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z);
            Gizmos.color = Color.red * 0.6f;
            Gizmos.DrawWireSphere(worldCenter, worldRadius);
        }
        else if (areaCollider is BoxCollider bc)
        {
            Gizmos.color = Color.red * 0.6f;
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, transform.lossyScale);
            Gizmos.DrawWireCube(bc.center, bc.size);
            Gizmos.matrix = Matrix4x4.identity;
        }
        else if (areaCollider != null)
        {
            Bounds b = areaCollider.bounds;
            Gizmos.color = Color.red * 0.6f;
            Gizmos.DrawWireSphere(b.center, Mathf.Max(b.extents.x, b.extents.z));
        }
    }
    #endregion
}