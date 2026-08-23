using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;
using System.Collections;
using Unity.VisualScripting.FullSerializer;
// 로직 베이스 클래스 (순수 C#)
[System.Serializable]
public abstract class PassiveLogic
{
    public abstract void Apply();
    public abstract void Remove();
}



[CreateAssetMenu(menuName = "Skills/PassiveSkillData")]
public class PassiveSkillData : ScriptableObject
{
    [Header("Localization")]
    public LocalizedString Localize_passiveName;
    public LocalizedString Localize_description;
    public LocalizedString Localize_EffectDescription;

    public string passiveName => Localize_passiveName.GetLocalizedString();
    public string description => Localize_description.GetLocalizedString();
    public string EffectDescription => Localize_EffectDescription.GetLocalizedString();

    public Sprite icon;
    [SerializeReference, SubclassSelector]
    public PassiveLogic skillLogic;
}

[System.Serializable]
public class ParringTimeIncrease : PassiveLogic
{
    public override void Apply()
    {
        StatManager.Instance.ParringTime = 0.15f;
    }
    public override void Remove()
    {
        StatManager.Instance.ParringTime = 0.1f;
    }
}

[System.Serializable]
public class ParringSkill : PassiveLogic
{
    public override void Apply()
    {
        Player.Instance.animator.SetBool("LearnParring", true);
        Player.Instance.acquiredParring = true;
    }
    public override void Remove()
    {
        Player.Instance.animator.SetBool("LearnParring", false);
        Player.Instance.acquiredParring = false;

    }
}

[System.Serializable]
public class DashCooldown : PassiveLogic
{
    public override void Apply() { StatManager.Instance.dashCooldown -= 0.5f; }
    public override void Remove() { StatManager.Instance.dashCooldown += 0.5f; }
}

[System.Serializable]
public class B0_HpRecovery : PassiveLogic
{
    Coroutine hpRecoveryCoroutine;
    public override void Apply() 
    {
        hpRecoveryCoroutine = Player.Instance.StartCoroutine(Player.Instance.HpRecoveryRoutine());
    }
    public override void Remove() 
    { 
        Player.Instance.StopCoroutine(hpRecoveryCoroutine);
    }
}

[System.Serializable]
public class UpgradeSkillPassive : PassiveLogic
{
    public SkillBase baseSkill;

    public SkillBase explicitUpgradedSkill;

    public bool replaceDefaultSkill = true;

    // 되돌리기용 저장
    private List<int> replacedIndices = new List<int>();
    private List<SkillBase> originalSkills = new List<SkillBase>();
    private bool defaultReplaced = false;
    private SkillBase originalDefaultSkill = null;

    public override void Apply()
    {
        if (Player.Instance == null || Player.Instance.PlayerAttack == null)
        {
            Debug.LogWarning("[UpgradeSkillPassive] Player 또는 PlayerAttack이 없습니다.");
            return;
        }

        if (baseSkill == null)
        {
            Debug.LogWarning("[UpgradeSkillPassive] baseSkill이 지정되지 않았습니다.");
            return;
        }

        var pa = Player.Instance.PlayerAttack;

        replacedIndices.Clear();
        originalSkills.Clear();
        defaultReplaced = false;
        originalDefaultSkill = null;

        // acquiredSkills에서 일치하는 항목만 런타임 복제해서 교체 (원본 ScriptableObject 변경 방지)
        if (pa.acquiredSkills != null)
        {
            for (int i = 0; i < pa.acquiredSkills.Count; i++)
            {
                if (pa.acquiredSkills[i] == baseSkill)
                {
                    replacedIndices.Add(i);
                    originalSkills.Add(pa.acquiredSkills[i]);

                    // 복제본 생성 후 isUpgraded=true 설정
                    SkillBase clone = Object.Instantiate(baseSkill);
                    clone.isUpgraded = true;
                    // 명시된 업그레이드 스킬이 있으면 링크 유지 (선택사항)
                    if (explicitUpgradedSkill != null)
                        clone.UpgradedSkill = explicitUpgradedSkill;

                    pa.acquiredSkills[i] = clone;
                }
            }
        }
    }

    public override void Remove()
    {
        if (Player.Instance == null || Player.Instance.PlayerAttack == null)
        {
            Debug.LogWarning("[UpgradeSkillPassive] Player 또는 PlayerAttack이 없습니다.");
            return;
        }

        var pa = Player.Instance.PlayerAttack;

        // acquiredSkills 복구 (저장된 인덱스와 원본 참조 사용)
        for (int k = 0; k < replacedIndices.Count; k++)
        {
            int idx = replacedIndices[k];
            SkillBase orig = originalSkills[k];
            if (pa.acquiredSkills != null && idx >= 0 && idx < pa.acquiredSkills.Count)
            {
                // 현재가 우리가 만든 복제인지 확인 후 교체 (안전성)
                if (pa.acquiredSkills[idx] != null && pa.acquiredSkills[idx].isUpgraded)
                {
                    pa.acquiredSkills[idx] = orig;
                }
            }
        }
        replacedIndices.Clear();
        originalSkills.Clear();
    }
}

[System.Serializable]
public class ParryAttackBuffPassive : PassiveLogic
{
    [Header("Buff Settings")]
    public float buffAmount = 10f;     // 증가할 공격력
    public float buffDuration = 5f;    // 지속 시간

    private Coroutine buffCoroutine;
    private bool isBuffActive = false;

    public override void Apply()
    {
        if (Player.Instance != null)
        {
            Player.Instance.OnParrySuccess += HandleParrySuccess;
        }
    }

    public override void Remove()
    {
        if (Player.Instance != null)
        {
            Player.Instance.OnParrySuccess -= HandleParrySuccess;

            if (buffCoroutine != null)
            {
                Player.Instance.StopCoroutine(buffCoroutine);
                buffCoroutine = null;
            }

            if (isBuffActive)
            {
                StatManager.Instance.AttackPower -= buffAmount;
                isBuffActive = false;
            }
        }
    }

    private void HandleParrySuccess()
    {
        if (buffCoroutine != null)
        {
            Player.Instance.StopCoroutine(buffCoroutine);
        }
        buffCoroutine = Player.Instance.StartCoroutine(BuffRoutine());
    }

    private IEnumerator BuffRoutine()
    {
        if (!isBuffActive)
        {
            StatManager.Instance.AttackPower += buffAmount;
            isBuffActive = true;
        }

        yield return new WaitForSeconds(buffDuration);

        StatManager.Instance.AttackPower -= buffAmount;
        isBuffActive = false;
        buffCoroutine = null;
    }
}