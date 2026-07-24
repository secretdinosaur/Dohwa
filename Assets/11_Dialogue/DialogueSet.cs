using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Localization;
[CreateAssetMenu(menuName = "DialogueSet")]
public class DialogueSet : ScriptableObject
{
    public LocalizedString[] Script;

    public bool Whisper;
    public bool Shout;

    [Space(10f)]
    public GettingWhat gettingWhat;

    public bool isChoiceNode = false;
    public ChoiceOption[] choiceOptions;
    public DialogueSet NextMainDialogue;
    public bool needExecutionFunction = false;
    public int ExecutionFunctionIndex = 0;

    private bool lastWhisper;
    private bool lastShout;


    private void OnValidate()
    {
        if (Whisper && !lastWhisper)
        {
            Shout = false;
        }
        else if (Shout && !lastShout)
        {
            Whisper = false;
        }

        lastWhisper = Whisper;
        lastShout = Shout;
    }

    public void GetImmadiately()
    {
        if (Player.Instance == null) return;
        if (gettingWhat.GettingType.HasFlag(GettingType.Item))
        {
            Debug.Log("Give Item");
        }
        if (gettingWhat.GettingType.HasFlag(GettingType.Stat))
        {
            if (StatManager.Instance != null)
                StatManager.Instance.GetStatPoint(gettingWhat.GetStatPoint);
        }
        if (gettingWhat.GettingType.HasFlag(GettingType.Gold))
        {

        }
        if (gettingWhat.GettingType.HasFlag(GettingType.Function))
        {
            gettingWhat.OnDialogueEnd?.Invoke();
        }
    }
}
public enum QuestType
{
    None,
    HasItem,        // 특정 아이템 보유(개수)
    KillCount,      // 특정 적을 n회 처치
    MinimumGrade,   // 등급/단계가 n 이상 (GradeManager.CurrentGradeIndex 사용)
    MinimumGold     // 골드가 n 이상
}

[Flags]
public enum GettingType
{
    None = 0,
    Item = 1 << 0,
    Stat = 1 << 1,
    Gold = 1 << 2,
    Function = 1 << 3,
}

[Flags]
public enum RewardType
{
    None = 0,
    Item = 1 << 0,        // 아이템
    Stat = 1 << 1,       // 꺠달음포인트
}


[Serializable]
public class ChoiceOption
{
    public LocalizedString Text;
    [Space(10f)]
    [Header("NextDialogueSet에 들어있는 대화들 중 \n다음 대화로 선택될 대화를 조건을 통해서 찾을거면 True")]
    public bool needCheckConditionForChooseNextDialogue = false;
    public List<ConditionalDialogueSet> NextDialogueSet = new List<ConditionalDialogueSet>();
    [Space(10f)]
    [Header("이 대화가 ShowDialogue 중, 조건에 따라서 \n선택 가능한 옵션으로 등장하게 하고 싶으면 True")]
    public bool needCheckCondition = false;
    public List<Condition> conditions = new List<Condition>();
    [Space(10f)]
    public bool isQuest;
    [Tooltip("Only fill below if is Quest")]
    public QuestOption questOption;
    public QuestRewardOption questRewardOption;

    public bool CheckConditions()
    {
        if (!needCheckCondition || conditions == null || conditions.Count == 0)
            return true;

        foreach (var condition in conditions)
        {
            if (condition != null && !condition.IsMet())
            {
                return false;
            }
        }

        return true;
    }
    public DialogueSet GetValidNextDialogue()
    {
        if (NextDialogueSet == null || NextDialogueSet.Count == 0)
            return null;

        // 위에서부터 순서대로 검사해서 '첫 번째로 조건을 통과한 대화'를 반환
        foreach (var conditionalSet in NextDialogueSet)
        {
            if (conditionalSet != null && conditionalSet.IsMet())
            {
                return conditionalSet.dialogueSet;
            }
        }

        return null; // 조건에 맞는 대화가 하나도 없을 때
    }
    public bool IsQuestClear()
    {
        if (questOption == null) return false;

        switch (questOption.questType)
        {
            case QuestType.None:
                return false;

            case QuestType.HasItem:
                if (InventoryManager.Instance == null) return false;
                if (questOption.requireItem == null || questOption.requireItem.Length == 0) return false;

                foreach (var req in questOption.requireItem)
                {
                    if (req == null || req.requireItem == null) return false;

                    InventorySlot slot = null;
                    string id = null;
                    InventoryManager.InventoryType invType = default;

                    if (req.requireItem.Weapon != null)
                    {
                        if (string.IsNullOrEmpty(req.requireItem.Weapon.Id)) return false;
                        invType = InventoryManager.InventoryType.Weapon;
                        id = req.requireItem.Weapon.Id;
                    }
                    else if (req.requireItem.Armor != null)
                    {
                        if (string.IsNullOrEmpty(req.requireItem.Armor.Id)) return false;
                        invType = InventoryManager.InventoryType.Armor;
                        id = req.requireItem.Armor.Id;
                    }
                    else if (req.requireItem.SkillBook != null)
                    {
                        if (string.IsNullOrEmpty(req.requireItem.SkillBook.Id)) return false;
                        invType = InventoryManager.InventoryType.SkillBook;
                        id = req.requireItem.SkillBook.Id;
                    }
                    else if (req.requireItem.Consumable != null)
                    {
                        if (string.IsNullOrEmpty(req.requireItem.Consumable.Id)) return false;
                        invType = InventoryManager.InventoryType.Item;
                        id = req.requireItem.Consumable.Id;
                    }
                    else if (req.requireItem.NonConsumable != null)
                    {
                        if (string.IsNullOrEmpty(req.requireItem.NonConsumable.Id)) return false;
                        invType = InventoryManager.InventoryType.Item;
                        id = req.requireItem.NonConsumable.Id;
                    }
                    else
                    {
                        // Unknown/unsupported entry
                        return false;
                    }

                    slot = InventoryManager.Instance.FindItem(invType, id);
                    if (slot == null || slot.item == null) return false;
                    if (slot.item.Count < req.requireItemCount) return false;
                }

                return true;

            case QuestType.KillCount:
                return false;

            case QuestType.MinimumGrade:
                return false;

            case QuestType.MinimumGold:
                return false;

            default:
                return false;
            }
    }

    public void GiveQuestReward()
    {
        if (Player.Instance == null) return;
        if (questRewardOption.rewardType.HasFlag(RewardType.Item))
        {
            Debug.Log("Give Item");
        }
        if (questRewardOption.rewardType.HasFlag(RewardType.Stat))
        {
            if (StatManager.Instance != null)
                StatManager.Instance.GetStatPoint(questRewardOption.Point);
        }
    }
}
[System.Serializable]
public class QuestOption
{
    [Tooltip("Only fill below if is Quest")]
    public string QuestKey;
    public DialogueSet NextDialogueSetIfQuestClear;
    public bool QuestReward = false;

    [Header("Quest condition (only when isQuest)")]
    public QuestType questType = QuestType.None;

    [Header("Item")]
    public RequireItem[] requireItem;

    [Header("Kill")]
    public string KillTargetKey;
    public int RequiredKillCount = 1;

    [Header("Grade")]
    public int RequiredGradeIndex = 0;

    [Header("Gold")]
    public int RequiredGold = 0;
}
[System.Serializable]
public class RequireItem
{
    public ItemEntry requireItem;
    public int requireItemCount;
    public bool isDeleteItem = false;
}

[System.Serializable]
public class QuestRewardOption
{

    [Header("Reward condition (only when isQuest)")]
    public RewardType rewardType = RewardType.None;

    [Header("Item")]
    public InventoryManager.InventoryType ItemType;
    public string ItemId;
    public int ItemCount = 1;
    public bool isDeleteItem = false;

    [Header("Stat")]
    public int Point = 0;
}
[System.Serializable]
public struct GettingWhat
{
    public GettingType GettingType;
    [Space(20f)]
    public int GetStatPoint;
    public UnityEvent OnDialogueEnd;
}
[Serializable]
public class ConditionalDialogueSet
{
    [Tooltip("조건을 만족할 때 이동할 대화")]
    public DialogueSet dialogueSet;

    [Header("이 대화로 이동하기 위한 조건")]
    public List<Condition> conditions = new List<Condition>();

    // 해당 대화의 조건을 만족하는지 검사
    public bool IsMet()
    {
        // 조건을 검사하지 않거나 조건 목록이 비어있으면 항상 통과 (기본 대화로 사용)
        if (conditions == null || conditions.Count == 0)
            return true;

        foreach (var condition in conditions)
        {
            if (condition != null && !condition.IsMet())
            {
                return false; // 하나라도 실패하면 통과 불가
            }
        }

        return true;
    }
}


