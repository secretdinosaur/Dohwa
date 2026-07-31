using UnityEngine;

public class EnemyWeaponFire : MonoBehaviour
{
    public Animator weaponAnimator;
    EnemyCore core;

    private void Awake()
    {
        core = GetComponentInParent<EnemyCore>();
        core.CmdPerformAttack += WeaponFire;
        core.OnParried += HandleParried;
    }

    private void OnDestroy()
    {
        if (core != null)
        {
            core.CmdPerformAttack -= WeaponFire;
            core.OnParried -= HandleParried;
        }
    }

    private void WeaponFire(SkillBase s)
    {
        if (weaponAnimator != null)
        {
            weaponAnimator.SetTrigger("Fire");
        }
    }

    private void HandleParried()
    {
        if (core != null)
        {
            weaponAnimator.Play("Idle");
        }
    }
}