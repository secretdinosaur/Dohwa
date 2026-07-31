using UnityEngine;

[RequireComponent(typeof(EnemyCore))]
public class EnemySound : MonoBehaviour
{
    private AudioSource footstepAS;
    private AudioSource attackAS;
    private AudioSource etcAS;

    public AudioClip[] footstepSounds;
    public AudioClip startChaseSound;
    public AudioClip deathSound;

    private EnemyCore core;

    private float lastFootstepTime = -1f;
    private const float FootstepCooldown = 0.2f;

    private void Awake()
    {
        core = GetComponent<EnemyCore>();
        AudioSource[] sources = GetComponents<AudioSource>();
        if (sources.Length > 0) footstepAS = sources[0];
        if (sources.Length > 1) attackAS = sources[1];
        if (sources.Length > 2) etcAS = sources[2];

        if (attackAS != null) attackAS.spatialBlend = 0.2f;

        core.OnDeath += PlayDeathSound;
        core.OnPlayerDetected += PlayChaseSound;

        core.OnDeath += CancelAttackSound;
        core.OnStunned += CancelAttackSound;
        core.CmdCancelAttack += CancelAttackSound;
        core.PlayHitSound += PlayHitSound;
    }

    private void OnDestroy()
    {
        if (core != null)
        {
            core.OnDeath -= PlayDeathSound;
            core.OnPlayerDetected -= PlayChaseSound;

            core.OnDeath -= CancelAttackSound;
            core.OnStunned -= CancelAttackSound;
            core.CmdCancelAttack -= CancelAttackSound;
            core.PlayHitSound -= PlayHitSound;
        }
    }

    private void PlayHitSound(SkillBase skill, int hitIndex)
    {
        if (skill.HitSound != null && skill.HitSound.Length > hitIndex && skill.HitSound[hitIndex])
            SoundManager.Instance.PlaySound(skill.HitSound[hitIndex]);
    }

    public void PlayFootstepSound()
    {
        if (Time.time - lastFootstepTime < FootstepCooldown) return;

        if (footstepSounds != null && footstepSounds.Length > 0 && footstepAS != null)
        {
            lastFootstepTime = Time.time;
            footstepAS.PlayOneShot(footstepSounds[Random.Range(0, footstepSounds.Length)]);
        }
    }

    private void PlayChaseSound()
    {
        if (startChaseSound != null && etcAS != null)
            etcAS.PlayOneShot(startChaseSound);
    }

    private void PlayDeathSound()
    {
        if (deathSound != null && etcAS != null)
            etcAS.PlayOneShot(deathSound);
    }

    public void PlaySkillSound(AudioClip clip)
    {
        if (clip != null && attackAS != null)
            attackAS.PlayOneShot(clip);
    }

    public void CancelAttackSound()
    {
        if (attackAS != null)
            attackAS.Stop();
    }
}