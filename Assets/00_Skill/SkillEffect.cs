using System.Collections;
using UnityEngine;

public class SkillEffect : MonoBehaviour
{
    public float returnTime = 5;
    public SkillBase skill;
    public Vector3 offset;
    public Vector3 rotation;
    public IEnumerator ReturnToPool()
    {
        yield return new WaitForSeconds(returnTime);
        if (skill == null)
            SkillEffectManager.Instance.ReturnSkillEffect(this, "GuardPS");
        else
            SkillEffectManager.Instance.ReturnSkillEffect(this, skill.name);
    }
}
