using Redcode.Pools;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
public class SkillEffectManager : MonoBehaviour
{
    public static SkillEffectManager Instance;
    PoolManager poolManager;

    private void Awake()
    {
        poolManager = GetComponent<PoolManager>();
        Instance = this;
    }
    public void GuardEffect(Vector3 position, Vector3 rotation, Transform enemy, string psName)
    {
        var guardEffect = poolManager.GetFromPool<SkillEffect>(psName);

        if (guardEffect == null) return;
        guardEffect.gameObject.SetActive(false);

        if (enemy != null)
        {
            guardEffect.transform.position = position;
            guardEffect.transform.LookAt(enemy.transform.position);
        }
        else
        {
            guardEffect.transform.eulerAngles = rotation;
        }
        Vector3 rotatedOffset = guardEffect.transform.rotation * guardEffect.offset;
        guardEffect.transform.position = position + rotatedOffset;

        guardEffect.gameObject.SetActive(true);
        StartCoroutine(guardEffect.ReturnToPool());
    }

    public SkillEffect PlaySkillEffect(Transform caster, SkillBase skill)
    {
        try
        {
            SkillEffect effect = poolManager.GetFromPool<SkillEffect>(skill.effect.name);

            effect.transform.position = caster.position + (caster.rotation * effect.offset);
            effect.transform.rotation = caster.rotation * Quaternion.Euler(effect.rotation);
            effect.skill = skill;
            StartCoroutine(effect.ReturnToPool());
            return effect;
        }
        catch
        {
            return null;
        }
        

    }

    public void PlayHitEffect(GameObject target, SkillBase skill, Vector3 hitPoint, Quaternion boxRotation, int hitIndex = 0)
    {
        HitEffect hitEffectInstance = poolManager.GetFromPool<HitEffect>("HitEffect");
        if (hitEffectInstance == null) return;

        hitEffectInstance.gameObject.SetActive(false);

        hitEffectInstance.transform.position = hitPoint;
        hitEffectInstance.transform.rotation = boxRotation;

        hitEffectInstance.gameObject.SetActive(true);
    }

    private Coroutine hitStopCoroutine;

    public void PlayCriticalHitEffect(GameObject target, SkillBase skill, Vector3 hitPoint, Quaternion boxRotation, int hitIndex = 0)
    {
        HitEffect hitEffectInstance = poolManager.GetFromPool<HitEffect>("CriticalHitEffect");
        if (hitEffectInstance == null) return;
        hitEffectInstance.gameObject.SetActive(false);

        hitEffectInstance.transform.position = hitPoint;
        hitEffectInstance.transform.rotation = boxRotation;

        hitEffectInstance.gameObject.SetActive(true);

        if (hitStopCoroutine != null)
        {
            StopCoroutine(hitStopCoroutine);
        }
        hitStopCoroutine = StartCoroutine(HitStopRoutine());
    }

    private IEnumerator HitStopRoutine()
    {
        Time.timeScale = 0f;

        yield return new WaitForSecondsRealtime(0.04f);

        Time.timeScale = 1f;
        hitStopCoroutine = null;
    }

    public void ReturnSkillEffect(SkillEffect effect, string name)
    {
        if(effect != null && effect.gameObject.activeSelf)
            poolManager.TakeToPool<SkillEffect>(name, effect);
    }

    public void ReturnSkillEffect(HitEffect effect)
    {
        try
        {
            if (effect.name.Contains("Critical"))
            {
            poolManager.TakeToPool<HitEffect>("CriticalHitEffect", effect);

            }
            else poolManager.TakeToPool<HitEffect>("HitEffect", effect);

        }
        catch { print(effect); }
    }
}