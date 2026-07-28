using System.Collections;
using UnityEngine;

public class Projectile : MonoBehaviour
{
    #region Settings
    [Header("Projectile Settings")]
    [SerializeField] private float defaultLifeTime = 10f;
    #endregion

    #region State Variables
    public float Damage;
    public float Speed;
    [HideInInspector] public SkillBase SkillFired;
    [HideInInspector] public GameObject Shooter;
    [HideInInspector] public int HitIndex { get; private set; }

    protected bool isShooterPlayer = false;
    protected bool isFired = false;
    protected bool isTracking = false;

    private Vector3 moveDirection;
    private Transform targetToFollow;
    #endregion

    #region Initialization
    public virtual void Init(GameObject shooter, float damage, float speed, SkillBase skillFired, int hitIndex = 0)
    {
        Shooter = shooter;
        Damage = damage;
        Speed = speed;
        SkillFired = skillFired;
        HitIndex = hitIndex;
        isShooterPlayer = shooter != null && shooter.GetComponent<Player>() != null;

        StartCoroutine(DestroyRoutine());
    }

    public virtual void ShootToPoint(Vector3 destination)
    {
        moveDirection = (destination - transform.position).normalized;
        isTracking = false;
        isFired = true;
    }

    public virtual void ShootToTarget(Transform target)
    {
        targetToFollow = target;
        isTracking = true;
        isFired = true;
    }
    #endregion

    #region Movement Logic
    private void Update()
    {
        if (!isFired) return;

        if (isTracking && targetToFollow != null)
        {
            moveDirection = (targetToFollow.position - transform.position).normalized;
        }

        if (moveDirection != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(moveDirection);
        }

        transform.position += moveDirection * Speed * Time.deltaTime;
    }
    #endregion

    #region Collision & Damage Logic
    private void OnTriggerEnter(Collider other)
    {
        if (Shooter != null && other.gameObject == Shooter) return;

        if (other.TryGetComponent<IDamageable>(out var target))
        {
            bool isTargetPlayer = other.GetComponent<Player>() != null;

            if (isShooterPlayer && isTargetPlayer) return;
            if (!isShooterPlayer && !isTargetPlayer) return;

            OnHit(target, other);
        }
        else
        {
            int obstacleMask = LayerMask.GetMask("Default", "Wall", "Ground");
            if (((1 << other.gameObject.layer) & obstacleMask) != 0)
            {
                DestroyThis();
            }
        }
    }

    public virtual void OnHit(IDamageable target, Collider targetCollider)
    {
        Transform casterTransform = Shooter != null ? Shooter.transform : transform;

        if (SkillFired is Skill_Range rangeSkill)
        {
            rangeSkill.OnProjectileHit(gameObject, targetCollider, casterTransform, Damage / rangeSkill.hitDataList[HitIndex].damageMultiplier, HitIndex);
        }
        else
        {
            HitEffectData effectData = null;
            if (SkillFired != null && SkillFired.HitEffects != null && SkillFired.HitEffects.Length > HitIndex)
            {
                effectData = SkillFired.HitEffects[HitIndex];
            }
            target.TakeDamage(Damage, SkillFired, casterTransform, effectData, HitIndex);
        }

        DestroyThis();
    }
    #endregion

    #region Lifecycle Helpers
    public virtual void DestroyThis()
    {
        Destroy(gameObject);
    }

    private IEnumerator DestroyRoutine()
    {
        yield return new WaitForSeconds(defaultLifeTime);
        DestroyThis();
    }
    #endregion
}