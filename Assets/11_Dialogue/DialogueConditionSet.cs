using System;
using Unity.VisualScripting;
using UnityEngine;

public class DialogueConditionSet : MonoBehaviour
{
    
}

public enum ConditionType
{
    None,
    PlayerPrefs_HasKey,       // PlayerPrefs 키 존재 여부
    PlayerPrefs_IntValue,     // PlayerPrefs 정수 값 비교
    Gold_Check,               // 골드 체크 (예시)
    Check_EventClear,         // 이벤트 클리어 여부 체크 (예시)
    Item_Check,               // 아이템 소유 여부 체크 (예시)
}

public enum CompareType
{
    Equal,              // 같다 (==)
    GreaterThanOrEqual, // 이상 (>=)
    LessThanOrEqual,     // 이하 (<=)
    GreaterThan,
    LessThan,

}

[Serializable]
public class Condition
{
    [Tooltip("검사할 조건의 종류를 선택하세요.")]
    public ConditionType conditionType;

    public string keyName;
    public bool shouldExist = false; // HasKey용
    public int compareValue;        // IntValue 비교용
    public RequireItem req;        // Item_Check용
    public CompareType compareType;

    // 조건을 검사하는 메인 함수
    public bool IsMet()
    {
        switch (conditionType)
        {
            case ConditionType.None:
                return true;
            case ConditionType.PlayerPrefs_HasKey:
                if (string.IsNullOrEmpty(keyName)) return false;
                return PlayerPrefs.HasKey(keyName) == shouldExist;

            case ConditionType.PlayerPrefs_IntValue:
                if (string.IsNullOrEmpty(keyName) || !PlayerPrefs.HasKey(keyName)) return false;
                int savedValue = PlayerPrefs.GetInt(keyName, 0);
                return CompareInt(savedValue, compareValue, compareType);

            case ConditionType.Gold_Check:
                // 실제 프로젝트의 골드 변수 연결 (예: PlayerManager.Instance.Gold >= requiredAmount)
                return true;

            case ConditionType.Check_EventClear:
                return DataManager.Instance.IsEventCleared(keyName) == shouldExist;

            case ConditionType.Item_Check:
                if (keyName == null || req.requireItem == null) return false;

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
                    return false;
                }

                if (InventoryManager.Instance == null) return false;

                InventorySlot slot = InventoryManager.Instance.FindItem(invType, id);
                if (slot == null || slot.item == null) return false;

                // 아이템 개수 비교 (기본적으로 이상(>=) 비교 또는 CompareInt 활용 가능)
                return CompareInt(slot.item.Count, req.requireItemCount, compareType);

            default:
                return true;
        }
    }

    private bool CompareInt(int a, int b, CompareType type)
    {
        switch (type)
        {
            case CompareType.Equal:
                return a == b;
            case CompareType.GreaterThanOrEqual:
                return a >= b;
            case CompareType.LessThanOrEqual:
                return a <= b;
            case CompareType.GreaterThan:
                return a > b;
            case CompareType.LessThan:
                return a < b;
            default:
                return false;
        }
    }
}