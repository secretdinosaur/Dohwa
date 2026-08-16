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

        try
        {
            if (skill == null && name.Contains("ParryEffect"))
                SkillEffectManager.Instance.ReturnSkillEffect(this, "ParryEffect");
            else if (skill == null && name.Contains("GuardEffect"))
                SkillEffectManager.Instance.ReturnSkillEffect(this, "GuardEffect");
            else if(name.Contains("JangpungGuardPS")) 
                SkillEffectManager.Instance.ReturnSkillEffect(this, "JangpungGuardPS");
            else
                SkillEffectManager.Instance.ReturnSkillEffect(this, skill.name);
        }
        catch (System.Exception e)
        {
            print(name);
            print(skill);
        }
    }
}
