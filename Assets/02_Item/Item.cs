using System.Collections;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;
using static TutorialManager;

[System.Serializable]
public class ItemEntry
{
    public Weapon Weapon;
    public Armor Armor;
    public Consumable Consumable;
    public NonConsumable NonConsumable;
    public SkillBookBase SkillBook;

    public bool IsEmpty =>
        Weapon == null && Armor == null && Consumable == null && NonConsumable == null && SkillBook == null;

    // 한 Entry에 여러 타입이 들어있으면 우선순위대로 한 가지만 남깁니다 (Weapon, Armor, Food, SkillBook, Important)
    public void Normalize()
    {
        if (Weapon != null)
        {
            Armor = null;
            NonConsumable = null;
            SkillBook = null;
            Consumable = null;
            return;
        }

        if (Armor != null)
        {
            Weapon = null;
            NonConsumable = null;
            SkillBook = null;
            Consumable = null;
            return;
        }

        if (NonConsumable != null)
        {
            Weapon = null;
            Armor = null;
            SkillBook = null;
            Consumable = null;
            return;
        }

        if (SkillBook != null)
        {
            Weapon = null;
            Armor = null;
            NonConsumable = null;
            Consumable = null;
            return;
        }

        if (Consumable != null)
        {
            Weapon = null;
            Armor = null;
            NonConsumable = null;
            SkillBook = null;
            return;
        }
    }
}

public class Item : InteractableObj
{
    public override int Type => 2;

    #region [ 고유 식별자 (GUID) ]
    // 💡 에디터에서 씬에 배치될 때 자동으로 생성되어 저장되는 고유 ID입니다.
    [SerializeField, HideInInspector]
    private string uniqueId;

    public string UniqueID => uniqueId;
    #endregion

    // 여러 아이템을 입력받는 배열
    public ItemEntry[] Items = new ItemEntry[0];

    [Header("Execution Function After Obtained This Item")]
    public UnityEvent ExecutionFunctionSet = new UnityEvent();

    private void OnValidate()
    {
        if (Items != null)
        {
            // 각 엔트리 내부에서 중복 타입을 정리
            for (int i = 0; i < Items.Length; i++)
            {
                if (Items[i] == null) Items[i] = new ItemEntry();
                Items[i].Normalize();
            }
        }

#if UNITY_EDITOR
        // 고유 ID가 비어있다면 새로 발급합니다.
        if (string.IsNullOrEmpty(uniqueId))
        {
            uniqueId = System.Guid.NewGuid().ToString();
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }

#if UNITY_EDITOR
    // 💡 씬에서 복제(Ctrl+D)한 경우 ID가 중복되므로 이 메뉴를 눌러 ID를 갱신해야 합니다.
    [ContextMenu("Generate New Unique ID")]
    private void GenerateNewID()
    {
        uniqueId = System.Guid.NewGuid().ToString();
        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log($"[{gameObject.name}] 새로운 고유 ID가 발급되었습니다: {uniqueId}");
    }
#endif

    public override void Interact(bool ShowPanelImmediately = true, bool LookPlayer = true)
    {
        base.Interact();
        // 모든 엔트리의 아이템을 인벤토리에 추가
        foreach (var entry in Items)
        {
            if (entry == null) continue;

            if (entry.Weapon != null) InventoryManager.Instance.AddItem(entry.Weapon);
            else if (entry.Armor != null) InventoryManager.Instance.AddItem(entry.Armor);
            else if (entry.Consumable != null) InventoryManager.Instance.AddItem(entry.Consumable);
            else if (entry.SkillBook != null) InventoryManager.Instance.AddItem(entry.SkillBook);
            else if (entry.NonConsumable != null) InventoryManager.Instance.AddItem(entry.NonConsumable);
        }
        if (ShowPanelImmediately)
        {
            TutorialManager.instance.CheckTutorial(TutorialType.Item);
            ShowItemGainPanel();
        }
    }

    public void ShowItemGainPanel()
    {
        string[] texts = new string[Items.Length];
        Sprite[] sprites = new Sprite[Items.Length];

        for (int i = 0; i < Items.Length; i++)
        {
            var entry = Items[i];
            if (entry == null) continue;

            if (entry.Weapon != null)
            {
                texts[i] = entry.Weapon.weaponName;
                sprites[i] = entry.Weapon.icon;
            }
            else if (entry.Armor != null)
            {
                texts[i] = entry.Armor.armorName;
                sprites[i] = entry.Armor.icon;
            }
            else if (entry.NonConsumable != null)
            {
                texts[i] = entry.NonConsumable.NonConsumableName;
                sprites[i] = entry.NonConsumable.icon;
            }
            else if (entry.SkillBook != null)
            {
                texts[i] = entry.SkillBook.skillBookName;
                sprites[i] = entry.SkillBook.icon;
            }
            else if (entry.Consumable != null)
            {
                texts[i] = entry.Consumable.ConsumableName;
                sprites[i] = entry.Consumable.icon;
            }
        }

        UIManager.Instance.StartGainRoutine(texts, sprites, ExecutionFunctionSet);

        // 💡 획득 시 ItemDropManager에 고유 ID를 전달하여 영구 저장
        if (ItemDropManager.Instance != null)
        {
            ItemDropManager.Instance.RegisterItemObtained(this);
        }

        Destroy(gameObject);
    }
}