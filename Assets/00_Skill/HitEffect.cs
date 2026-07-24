using UnityEngine;
using System.Collections;
public class HitEffect : MonoBehaviour
{
    private void OnEnable()
    {
        ApplyParticleSettings();
    }

    private void ApplyParticleSettings()
    {
        StartCoroutine(returnRoutine());
    }

    IEnumerator returnRoutine()
    {
        yield return new WaitForSeconds(3f);
        SkillEffectManager.Instance.ReturnSkillEffect(this);
    }
}
