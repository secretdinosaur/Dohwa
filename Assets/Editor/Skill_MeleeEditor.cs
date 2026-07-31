using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Skill_Melee))]
public class Skill_MeleeEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        List<string> excludedProps = new List<string> { "m_Script" };

        bool isEnemy = serializedObject.FindProperty("isEnemySkill").boolValue;

        if (isEnemy)
        {
            // 플레이어 전용 로직 및 기타 필드 전부 명시적 제외
            excludedProps.AddRange(new string[] {
                "Localize_skillName",
                "Localize_skillItemDesc",
                "Localize_skillEffectDesc",
                "commands",
                "isUpgraded",
                "UpgradedSkill",
                "IsAdvancedSkill",
                "isSpecialSkill",
                "icon",
                "triggerType",
                "triggerThreshold",
                "triggerCooldown",
                "triggerChance",
                "triggerMaxUsesPerCaster",
            });
        }
        else
        {
            if (!serializedObject.FindProperty("isSpecialSkill").boolValue)
            {
                excludedProps.AddRange(new string[] { "triggerType", "triggerThreshold", "triggerCooldown", "triggerChance", "triggerMaxUsesPerCaster" });
            }
        }

        // 가드, 스턴, 넉다운 속성이 MeleeHitData(hitDataList) 내부로 들어갔으므로,
        // 별도의 커스텀 그리기 없이 기본 그리기만 호출하면 리스트 내부에 모두 정상적으로 표시됩니다.
        DrawPropertiesExcluding(serializedObject, excludedProps.ToArray());

        serializedObject.ApplyModifiedProperties();
    }
}