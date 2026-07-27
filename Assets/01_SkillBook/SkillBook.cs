using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;

[CreateAssetMenu(menuName = "SkillBook/Basic")]
public class SkillBookBase : ScriptableObject
{
    public GameObject SkillTreePrefab;
    [Header("Localization")]
    public LocalizedString Localize_skillBookName;
    public LocalizedString Localize_skillBookItemDesc;
    public LocalizedString Localize_skillBookFirstPage;

    [SerializeField] public string id;
    public string Id => id;

    public string skillBookName => Localize_skillBookName.GetLocalizedString();
    public string skillBookItemDesc => Localize_skillBookItemDesc.GetLocalizedString();
    public string skillBookFirstPage => Localize_skillBookFirstPage.GetLocalizedString();
    public Sprite icon;

    public event Action<int> OnNodeUnlocked;

    public bool IsComplete
    {
        get
        {
            foreach (SkillTreeNode node in skillTree)
            {
                if (!node.unlocked) return false;
            }
            return true;
        }
    }

    [Serializable]
    public enum NodeType { ActiveSkill, PassiveSkill, StatSkill }

    [Serializable]
    public enum ConditionType { None, ReachGrade, EventCleared, hasTeatherItem }

    [Serializable]
    public struct StatModifier
    {
        public StatManager.StatType statType;
        public float statValue;
    }

    [Serializable]
    public struct SkillCondition
    {
        public ConditionType conditionType;
        public string targetValue;
    }

    [Serializable]
    public class SkillTreeNode
    {
        public NodeType nodeType;

        public SkillBase activeSkill;
        public PassiveSkillData passiveSkill;
        public StatModifier[] statModifiers;

        public LocalizedString localizeStatNodeName;
        public LocalizedString localizeStatNodeDesc;

        [Header("Unlock Settings")]
        public int cost = 1;

        [Header("Condition Settings")]
        public bool useCondition = false;
        public bool autoUnlockOnCondition = false;
        public SkillCondition[] conditions;

        [HideInInspector] public bool unlocked = false;

        public int[] prerequisiteIndices;
        public bool isStartingNode = false;

        public string GetNodeName()
        {
            switch (nodeType)
            {
                case NodeType.ActiveSkill:
                    return activeSkill != null ? activeSkill.skillName : "비어있음";
                case NodeType.PassiveSkill:
                    return passiveSkill != null ? passiveSkill.passiveName : "비어있음";
                case NodeType.StatSkill:
                    return (localizeStatNodeName != null && !localizeStatNodeName.IsEmpty)
                        ? localizeStatNodeName.GetLocalizedString()
                        : "능력치";
                default:
                    return "알 수 없음";
            }
        }

        // 💡 추가됨: 노드의 기본 설명 (Item Desc) 반환
        public string GetNodeBaseDesc()
        {
            switch (nodeType)
            {
                case NodeType.ActiveSkill:
                    return activeSkill != null ? activeSkill.skillItemDesc : "설명 없음";
                case NodeType.PassiveSkill:
                    return passiveSkill != null ? passiveSkill.description : "설명 없음";
                case NodeType.StatSkill:
                    return (localizeStatNodeDesc != null && !localizeStatNodeDesc.IsEmpty)
                        ? localizeStatNodeDesc.GetLocalizedString()
                        : "능력치 상승.";
                default:
                    return "설명 없음";
            }
        }

        // 💡 추가됨: 노드의 효과 설명 (Effect Desc) 반환
        public string GetNodeEffectDesc()
        {
            switch (nodeType)
            {
                case NodeType.ActiveSkill:
                    return activeSkill != null ? activeSkill.skillEffectDesc : "효과 없음";
                case NodeType.PassiveSkill:
                    return passiveSkill != null ? passiveSkill.EffectDescription : "효과 없음";
                case NodeType.StatSkill:
                    return ""; // Stat 스킬의 동적 효과 문자열은 툴팁(UI)에서 계산 후 덧붙임
                default:
                    return "효과 없음";
            }
        }
    }

    [Header("Skill Tree")]
    public SkillTreeNode[] skillTree;

    public bool IsConditionMet(SkillTreeNode node)
    {
        if (!node.useCondition || node.conditions == null || node.conditions.Length == 0) return true;

        foreach (var condition in node.conditions)
        {
            switch (condition.conditionType)
            {
                case ConditionType.ReachGrade:
                    if (int.TryParse(condition.targetValue, out int targetGrade))
                    {
                        if (GradeManager.Instance == null || GradeManager.Instance.CurrentGradeIndex < targetGrade)
                            return false;
                    }
                    else return false;
                    break;

                case ConditionType.EventCleared:
                    if (DataManager.Instance == null || !DataManager.Instance.IsEventCleared(condition.targetValue))
                        return false;
                    break;

                case ConditionType.hasTeatherItem:
                    if (int.TryParse(condition.targetValue, out int target))
                    {
                        if (InventoryManager.Instance.GetItemCount("cbbfcb75-fb88-4cff-8caf-c038f57ed59d") < target)
                            return false;
                    }
                    else return false;
                    break;
            }
        }

        return true;
    }

    private bool CheckPrerequisites(SkillTreeNode node)
    {
        if (node.prerequisiteIndices == null || node.prerequisiteIndices.Length == 0) return true;
        foreach (int req in node.prerequisiteIndices)
        {
            if (req < 0 || req >= skillTree.Length) return false;
            if (!skillTree[req].unlocked) return false;
        }
        return true;
    }

    public bool CanUnlockNode(int nodeIndex, int availablePoints)
    {
        if (skillTree == null || nodeIndex < 0 || nodeIndex >= skillTree.Length) return false;
        var node = skillTree[nodeIndex];

        if (node.unlocked) return false;
        if (!CheckPrerequisites(node)) return false;

        if (node.useCondition)
        {
            if (!IsConditionMet(node)) return false;
            if (node.autoUnlockOnCondition) return true;
        }

        return availablePoints >= node.cost;
    }

    public void CheckAndProcessAutoUnlocks()
    {
        if (skillTree == null) return;
        for (int i = 0; i < skillTree.Length; i++)
        {
            var node = skillTree[i];
            if (node.unlocked) continue;

            if (node.useCondition && node.autoUnlockOnCondition)
            {
                if (CheckPrerequisites(node) && IsConditionMet(node))
                {
                    ForceUnlockNode(i);
                }
            }
        }
    }

    public void ForceUnlockNode(int nodeIndex)
    {
        if (skillTree == null || nodeIndex < 0 || nodeIndex >= skillTree.Length) return;
        var node = skillTree[nodeIndex];

        if (node.unlocked) return;

        ApplyNodeEffect(node);

        node.unlocked = true;
        PlayerPrefs.SetInt($"SkillBook_{Id}_Node_{nodeIndex}_Unlocked", 1);
        OnNodeUnlocked?.Invoke(nodeIndex);
    }

    public bool UnlockNode(int nodeIndex)
    {
        if (skillTree == null || nodeIndex < 0 || nodeIndex >= skillTree.Length) return false;
        var node = skillTree[nodeIndex];

        if (node.unlocked) return false;
        if (!CheckPrerequisites(node)) return false;

        if (node.useCondition)
        {
            if (!IsConditionMet(node)) return false;
        }

        bool needsCost = !(node.useCondition && node.autoUnlockOnCondition);
        if (needsCost)
        {
            if (StatManager.Instance.StatPoint < node.cost) return false;
            StatManager.Instance.StatPoint -= node.cost;
        }

        ApplyNodeEffect(node);

        node.unlocked = true;
        if (SoundManager.Instance != null) SoundManager.Instance.PlayUISound(7);
        OnNodeUnlocked?.Invoke(nodeIndex);

        if (IsComplete && GradeManager.Instance != null)
            GradeManager.Instance.GetExp(GradeManager.ExpGainBehavior.CompletelyAcquiredSkillBook);

        return true;
    }

    private void ApplyNodeEffect(SkillTreeNode node)
    {
        switch (node.nodeType)
        {
            case NodeType.ActiveSkill:
                if (node.activeSkill != null && Player.Instance != null)
                {
                    if (!Player.Instance.PlayerAttack.acquiredSkills.Contains(node.activeSkill))
                        Player.Instance.PlayerAttack.acquiredSkills.Add(node.activeSkill);
                }
                break;
            case NodeType.PassiveSkill:
                if (node.passiveSkill != null && node.passiveSkill.skillLogic != null)
                    node.passiveSkill.skillLogic.Apply();
                break;
            case NodeType.StatSkill:
                if (node.statModifiers != null && StatManager.Instance != null)
                {
                    foreach (var modifier in node.statModifiers)
                        StatManager.Instance.ApplySingleStat(modifier.statType, modifier.statValue);
                }
                break;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrEmpty(id))
        {
            id = System.Guid.NewGuid().ToString();
            UnityEditor.EditorUtility.SetDirty(this);
        }
    }
#endif
}