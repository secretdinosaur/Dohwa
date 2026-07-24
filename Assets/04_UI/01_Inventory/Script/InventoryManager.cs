using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI; // UI 네임스페이스 (RawImage 사용 위함)
using static InventoryManager;
using static SkillBookBase;
using TMPro;

public class InventoryManager : MonoBehaviour
{
    public enum InventoryType
    {
        Weapon,
        Armor,
        SkillBook,
        Item,
    }

    public enum SlotType
    {
        Normal,
        Equipment
    }

    public enum TabType
    {
        None,
        Equipment,
        Info,
        Grade,
        SkillBook,
        Item,
        WeaponSub,
        ArmorSub,
        Setting,
        SettingGameSub, // 추가됨
        SettingKeySub,   // 추가됨
        SettingCreditSub
    }

    public static InventoryManager Instance;
    public bool isInventoryOpen = false;

    // ----- [추가된 부분: 배경 텍스처 배열] -----
    [Header("Background Textures")]
    public RenderTexture[] backgrounds;
    public Camera[] cameras;
    public GameObject uiMap;
    // 0: Info, 1: Grade, 2: Equipment, 3: SkillBook, 4: Item(Food), 5: Setting
    // -------------------------------------------

    [Header("Inventory")]
    public GameObject itemPrefab;

    public InventorySlot EquippedWeaponSlot;
    public InventorySlot EquippedArmorSlot;

    public GameObject WeaponInventory;
    public GameObject WeaponSlotContainer;
    public List<InventorySlot> WeaponSlots;

    public GameObject ArmorInventory;
    public GameObject ArmorSlotContainer;
    public List<InventorySlot> ArmorSlots;

    public GameObject SkillBookInventory;
    public GameObject SkillBookSlotContainer;
    public List<InventorySlot> SkillBookSlots;

    public GameObject ItemInventory;
    public GameObject ItemSlotContainer;
    public List<InventorySlot> ItemSlots;
    
    public ItemInfoPanel EquipItemInfoPanel;
    public ItemInfoPanel NormalItemInfoPanel;

    public Transform SkillBookPanel;

    public List<SkillBookBase> MySkillBooks;

    public GameObject Inventory;
    public GameObject InventoryBG;
    public GameObject Book;
    public GameObject BookInfo;
    public GameObject TreeInfo;
    public GameObject InformationTab;
    public GameObject GradeTab;
    public GameObject MouseItemInfo;
    public itemContextMenu ItemContextMenu;
    public GameObject CheckDeleteItemPenel;

    [Header("Setting UI")]
    public GameObject SettingTab;
    public GameObject SettingGamePanel; // 추가됨: Game 설정 내용이 들어갈 패널
    public GameObject SettingKeyPanel;  // 추가됨: Key 설정 내용이 들어갈 패널
    public GameObject SettingCreditPanel;

    public Button EquipmentButton;
    public Button InfoButton;
    public Button GradeButton;
    public Button SkillBookButton;
    public Button ItemButton;
    public Button WeaponButton;
    public Button ArmorButton;

    [Header("Setting Buttons")]
    public Button SettingButton;
    public Button SettingGameButton; // 추가됨
    public Button SettingKeyButton;  // 추가됨
    public Button SettingCreditButton;
    public GameObject TitlePanel;
    public GameObject QuitPanel;
    public InventorySlot[] QuickSlots;
    public GameObject QuickSlot;
    private TabType currentTab = TabType.None;

    private void Awake()
    {
        Instance = this;
        MySkillBooks = new List<SkillBookBase>();
    }

    private void Start()
    {
        CreateNewSlots(InventoryType.Weapon); //weapon
        CreateNewSlots(InventoryType.Armor); //armor
        CreateNewSlots(InventoryType.SkillBook); //skillbook
        CreateNewSlots(InventoryType.Item); //item
    }

    public void EquipWeapon(Weapon weapon, InventorySlot slot = null)
    {
        if (Player.Instance.equippedWeapon == weapon) return;

        InventorySlot EquippedWeapon = EquippedWeaponSlot.GetComponent<InventorySlot>();
        if (Player.Instance.equippedWeapon != null)
        {
            Player.Instance.UnequipWeapon();
        }
        if (slot != null)
        {
            if (EquippedWeapon.item.weapon != null)
            {
                slot.item.SetItem(EquippedWeapon.item.weapon);
                EquippedWeapon.item.SetItem(weapon);
            }
        }
        Player.Instance.equippedWeapon = weapon;
        Player.Instance.EquipWeapon(weapon);
        SetInfoPanel(0);
    }

    public void EquipArmor(Armor armor, InventorySlot slot = null)
    {
        InventorySlot EquippedArmor = EquippedArmorSlot.GetComponent<InventorySlot>();
        if (Player.Instance.equippedArmor == armor) return;
        if (Player.Instance.equippedArmor != null)
        {
            Player.Instance.UnequipArmor();
        }
        if (slot != null)
        {
            if(EquippedArmor.item.armor != null)
            {
                slot.item.SetItem(EquippedArmor.item.armor);
                EquippedArmor.item.SetItem(armor);
            }
            else
            {
                EquippedArmor.item.SetItem(armor);
            }
        }
        Player.Instance.equippedArmor = armor;
        Player.Instance.EquipArmor(armor);
        SetInfoPanel(1);
    }

    public void SetInfoPanel(int index)
    {
        if (index == 0)
        {
            if (Player.Instance.equippedWeapon != null)
            {
                EquipItemInfoPanel.ItemIcon.sprite = Player.Instance.equippedWeapon.icon;
                var iconColor = EquipItemInfoPanel.ItemIcon.color;
                iconColor.a = 1f;
                EquipItemInfoPanel.ItemNameText.text = Player.Instance.equippedWeapon.weaponName;
                EquipItemInfoPanel.ItemDescText.text = Player.Instance.equippedWeapon.EffectDescription;
                EquipItemInfoPanel.ItemEffectDescText.text = Player.Instance.equippedWeapon.ItemDescription;
                Player.Instance.Sword.GetComponent<MeshFilter>().mesh = Player.Instance.equippedWeapon.weaponMesh;
                if (Player.Instance.equippedWeapon.equipPassive != null)
                {
                    EquipItemInfoPanel.EffectNameText.text = Player.Instance.equippedWeapon.equipPassive.passiveName;
                    EquipItemInfoPanel.EffectDescText.text = Player.Instance.equippedWeapon.equipPassive.description;
                }
            }
            else
            {
                EquipItemInfoPanel.ItemIcon.sprite = null;
                var iconColor = EquipItemInfoPanel.ItemIcon.color;
                iconColor.a = 0f;
                EquipItemInfoPanel.ItemNameText.text = null;
                EquipItemInfoPanel.ItemDescText.text = null;
                EquipItemInfoPanel.ItemEffectDescText.text = null;
                EquipItemInfoPanel.EffectNameText.text = null;
                EquipItemInfoPanel.EffectDescText.text = null;
            }
        }
        else
        {
            if (Player.Instance.equippedArmor != null)
            {
                EquipItemInfoPanel.ItemIcon.sprite = Player.Instance.equippedArmor.icon;
                var iconColor = EquipItemInfoPanel.ItemIcon.color;
                iconColor.a = 1f;
                EquipItemInfoPanel.ItemIcon.color = iconColor;
                EquipItemInfoPanel.ItemNameText.text = Player.Instance.equippedArmor.armorName;
                EquipItemInfoPanel.ItemDescText.text = Player.Instance.equippedArmor.EffectDescription;
                EquipItemInfoPanel.ItemEffectDescText.text = Player.Instance.equippedArmor.ItemDescription;
                if (Player.Instance.equippedArmor.equipPassive != null)
                {
                    EquipItemInfoPanel.EffectNameText.text = Player.Instance.equippedArmor.equipPassive.passiveName;
                    EquipItemInfoPanel.EffectDescText.text = Player.Instance.equippedArmor.equipPassive.description;
                }
            }
            else
            {
                EquipItemInfoPanel.ItemIcon.sprite = null;
                var iconColor = EquipItemInfoPanel.ItemIcon.color;
                iconColor.a = 0f;
                EquipItemInfoPanel.ItemIcon.color = iconColor;
                EquipItemInfoPanel.ItemNameText.text = null;
                EquipItemInfoPanel.ItemDescText.text = null;
                EquipItemInfoPanel.ItemEffectDescText.text = null;
                EquipItemInfoPanel.EffectNameText.text = null;
                EquipItemInfoPanel.EffectDescText.text = null;
            }
        }
    }

    public void CreateNewSlots(InventoryType type)
    {
        Transform container = null;
        List<InventorySlot> targetList = null;

        switch (type)
        {
            case InventoryType.SkillBook: container = SkillBookSlotContainer.transform; targetList = SkillBookSlots; break;
            case InventoryType.Weapon: container = WeaponSlotContainer.transform; targetList = WeaponSlots; break;
            case InventoryType.Armor: container = ArmorSlotContainer.transform; targetList = ArmorSlots; break;
            case InventoryType.Item: container = ItemSlotContainer.transform; targetList = ItemSlots; break;
            default: return;
        }

        for (int i = 0; i < 20; i++)
        {
            GameObject obj = Instantiate(itemPrefab, container);
            InventorySlot slot = obj.GetComponentInChildren<InventorySlot>();

            if (slot != null)
            {
                slot.item.Clear();
                slot.slotMode = SlotType.Normal;
                slot.allowedType = type;

                if (slot.item != null)
                {
                    slot.item.itemType = type;
                }

                targetList.Add(slot);
            }
        }
    }

    public void RemoveNullSlots(InventoryType type)
    {
        List<InventorySlot> targetList = null;

        switch (type)
        {
            case InventoryType.SkillBook: targetList = SkillBookSlots; break;
            case InventoryType.Weapon: targetList = WeaponSlots; break;
            case InventoryType.Armor: targetList = ArmorSlots; break;
            case InventoryType.Item: targetList = ItemSlots; break;
            default: return;
        }

        int pageSize = 20;
        int pageCount = targetList.Count / pageSize;

        for (int page = pageCount - 1; page >= 0; page--)
        {
            if (page == pageCount - 1)
                continue;

            int start = page * pageSize;
            int end = Mathf.Min(start + pageSize, targetList.Count);

            bool hasItem = false;
            for (int i = start; i < end; i++)
            {
                var slot = targetList[i];
                if (slot != null && slot.item != null && (
                    slot.item.weapon != null ||
                    slot.item.armor != null ||
                    slot.item.skillBook != null ||
                    slot.item.consumable != null ||
                    slot.item.nonConsumable != null
                ))
                {
                    hasItem = true;
                    break;
                }
            }

            if (!hasItem)
            {
                for (int i = end - 1; i >= start; i--)
                {
                    if (targetList[i] != null)
                    {
                        Destroy(targetList[i].gameObject);
                    }
                    targetList.RemoveAt(i);
                }
            }
        }
    }

    public void UpdateSlots(InventoryType type)
    {
        List<InventorySlot> slots = null;

        switch (type)
        {
            case InventoryType.Weapon: slots = WeaponSlots; break;
            case InventoryType.Armor: slots = ArmorSlots; break;
            case InventoryType.SkillBook: slots = SkillBookSlots; break;
            case InventoryType.Item: slots = ItemSlots; break;
            default: return;
        }

        if (slots == null || slots.Count == 0) return;

        var filled = slots.Where(s => s != null && s.item != null && (
            s.item.weapon != null ||
            s.item.armor != null ||
            s.item.skillBook != null ||
            s.item.consumable != null ||
            s.item.nonConsumable != null
        )).ToList();

        var empty = slots.Where(s => s == null || s.item == null || (
            s.item.weapon == null &&
            s.item.armor == null &&
            s.item.skillBook == null &&
            s.item.consumable == null &&
            s.item.nonConsumable == null
        )).ToList();

        slots.Clear();
        slots.AddRange(filled);
        slots.AddRange(empty);

        for (int i = 0; i < slots.Count; i++)
        {
            slots[i].transform.SetSiblingIndex(i);
        }
    }

    public bool IsInventoryAvailable(InventoryType type)
    {
        return GetEmptySlot(type) != null;
    }

    public void AddItem(SkillBookBase skillBook)
    {
        InventorySlot[] SkillBookSlots = SkillBookSlotContainer.GetComponentsInChildren<InventorySlot>();
        foreach (InventorySlot Slot in SkillBookSlots)
        {
            if (Slot.item != null && Slot.item.skillBook == skillBook) return;
        }
        InventorySlot slot = GetEmptySlot(InventoryType.SkillBook);
        if (slot == null)
        {
            return;
        }
        MySkillBooks.Add(skillBook);
        slot.item.SetItem(skillBook);

        if (DataManager.Instance != null && !DataManager.Instance.IsLoading)
        {
            if (skillBook.skillTree != null)
            {
                // 특정 ID를 찾지 않고, 스킬트리 전체를 돌며 시작 노드를 찾아 모두 강제 해금
                for (int i = 0; i < skillBook.skillTree.Length; i++)
                {
                    if (skillBook.skillTree[i].isStartingNode)
                    {
                        skillBook.ForceUnlockNode(i);
                    }
                }
                PlayerPrefs.Save();
            }
        }

        if (SkillBookInventory != null) SkillBookInventory.SetActive(true);
    }

    public void AddItem(Weapon weapon)
    {
        InventorySlot slot = GetEmptySlot(InventoryType.Weapon);

        if (slot == null)
        {
            return;
        }
        slot.item.SetItem(weapon);
        UpdateSlots(InventoryType.Weapon);
    }

    public void AddItem(Armor armor)
    {
        InventorySlot slot = GetEmptySlot(InventoryType.Armor);

        if (slot == null)
        {
            return;
        }
        slot.item.SetItem(armor);
        UpdateSlots(InventoryType.Armor);
    }

    public void AddItem(Consumable consumable)
    {
        InventorySlot existingSlot = GetSlotWithSameItem(InventoryType.Item, consumable);

        if (existingSlot != null)
        {
            existingSlot.item.Count++;
            UIManager.Instance.SyncGameQuickSlots();

            return;
        }
        InventorySlot slot = GetEmptySlot(InventoryType.Item);
        slot.item.SetItem(consumable);
        UpdateSlots(InventoryType.Item);

        UIManager.Instance.SyncGameQuickSlots();
    }

    public void AddItem(NonConsumable nonConsumable)
    {
        InventorySlot existingSlot = GetSlotWithSameItem(InventoryType.Item, nonConsumable);

        if (existingSlot != null)
        {
            existingSlot.item.Count++;
            return;
        }
        InventorySlot slot = GetEmptySlot(InventoryType.Item);
        slot.item.SetItem(nonConsumable);
        UpdateSlots(InventoryType.Item);
    }

    private InventorySlot GetEmptySlot(InventoryType type)
    {
        Transform targetContainer = null;

        switch (type)
        {
            case InventoryType.Weapon:
                targetContainer = WeaponSlotContainer.transform;
                break;

            case InventoryType.Armor:
                targetContainer = ArmorSlotContainer.transform;
                break;

            case InventoryType.SkillBook:
                targetContainer = SkillBookSlotContainer.transform;
                break;

            case InventoryType.Item:
                targetContainer = ItemSlotContainer.transform;
                break;
            default:
                return null;
        }

        InventorySlot slot = FindEmptySlot(targetContainer, type);
        if (slot != null)
            return slot;

        CreateNewSlots(type);

        return FindEmptySlot(targetContainer, type);
    }

    private InventorySlot FindEmptySlot(Transform container, InventoryType type)
    {
        InventorySlot[] slots = container.GetComponentsInChildren<InventorySlot>(true);

        foreach (InventorySlot slot in slots)
        {
            if (slot == null)
                continue;

            if (slot.slotMode != SlotType.Normal)
                continue;

            if (slot.allowedType != type)
                continue;

            switch (type)
            {
                case InventoryType.Weapon:
                    if (slot.item.weapon == null)
                        return slot;
                    break;

                case InventoryType.Armor:
                    if (slot.item.armor == null)
                        return slot;
                    break;

                case InventoryType.SkillBook:
                    if (slot.item.skillBook == null)
                        return slot;
                    break;

                case InventoryType.Item:
                    if (slot.item.consumable == null && slot.item.nonConsumable == null)
                        return slot;
                    break;
            }
        }

        return null;
    }

    InventorySlot GetSlotWithSameItem(InventoryType type, Consumable consumable)
    {
        InventorySlot[] ConsumableSlots = ItemSlotContainer.GetComponentsInChildren<InventorySlot>();
        foreach (InventorySlot slot in ConsumableSlots)
        {
            if (slot != null && slot.slotMode == SlotType.Normal && slot.item != null && slot.item.itemType == type)
            {
                if (slot.item.consumable != null && slot.item.consumable.Id == consumable.Id)
                {
                    return slot;
                }
            }
        }
        foreach (QuickSlot slot in UIManager.Instance.GameQuickSlots)
        {
            if (slot != null && slot.slotMode == SlotType.Normal && slot.item != null && slot.item.itemType == type)
            {
                if (slot.item.consumable != null && slot.item.consumable.Id == consumable.Id)
                {
                    return slot;
                }
            }
        }
        return null;
    }

    InventorySlot GetSlotWithSameItem(InventoryType type, NonConsumable nonConsumable)
    {
        InventorySlot[] NonConsumableSlots = ItemSlotContainer.GetComponentsInChildren<InventorySlot>();
        foreach (InventorySlot slot in NonConsumableSlots)
        {
            if (slot != null && slot.slotMode == SlotType.Normal && slot.item != null && slot.item.itemType == type)
            {
                if (slot.item.nonConsumable != null && slot.item.nonConsumable.Id == nonConsumable.Id)
                {
                    return slot;
                }
            }
        }
        foreach (QuickSlot slot in UIManager.Instance.GameQuickSlots)
        {
            if (slot != null && slot.slotMode == SlotType.Normal && slot.item != null && slot.item.itemType == type)
            {
                if (slot.item.nonConsumable != null && slot.item.nonConsumable.Id == nonConsumable.Id)
                {
                    return slot;
                }
            }
        }
        return null;
    }

    public InventorySlot FindItem(InventoryType type, string id)
    {
        InventorySlot FoundedSlot;
        switch (type)
        {
            case InventoryType.Weapon:
                FoundedSlot = WeaponSlots
                    .FirstOrDefault(slot => slot.item.weapon != null && slot.item.weapon.Id == id);
                break;

            case InventoryType.Armor:
                FoundedSlot = ArmorSlots
                    .FirstOrDefault(slot => slot.item.armor != null && slot.item.armor.Id == id);
                break;

            case InventoryType.SkillBook:
                FoundedSlot = SkillBookSlots
                    .FirstOrDefault(slot => slot.item.skillBook != null && slot.item.skillBook.Id == id);
                break;

            case InventoryType.Item:
                FoundedSlot = ItemSlots
                    .FirstOrDefault(slot =>
                        slot.item.consumable?.Id == id ||
                        slot.item.nonConsumable?.Id == id);
                break;
            default:
                FoundedSlot = null;
                break;
        }
        return FoundedSlot;
    }

    public void RemoveItem(InventorySlot slot, int count)
    {
        if (slot != null)
        {
            if(count < slot.item.Count)
            {
                slot.item.Count -= count;
            }
            else
            {
                InventoryType inventoryType = slot.item.itemType;
                slot.item.Clear();
                RemoveNullSlots(inventoryType);
            }
        }
    }

    public void ShowItemTooltip(InventoryItem item)
    {
        if (item.itemType == InventoryType.Weapon)
        {
            NormalItemInfoPanel.ItemIcon.sprite = item.weapon.icon;
            NormalItemInfoPanel.ItemNameText.text = item.weapon.weaponName;
            NormalItemInfoPanel.ItemDescText.text = item.weapon.EffectDescription;
            NormalItemInfoPanel.ItemEffectDescText.text = item.weapon.ItemDescription;
            if (item.weapon.equipPassive != null)
            {
                NormalItemInfoPanel.EffectNameText.text = item.weapon.equipPassive.passiveName;
                NormalItemInfoPanel.EffectDescText.text = item.weapon.equipPassive.description;
                NormalItemInfoPanel.EffectIcon.sprite = item.weapon.equipPassive.icon;
            }
            else
            {
                NormalItemInfoPanel.EffectNameText.text = "";
                NormalItemInfoPanel.EffectDescText.text = "";
                NormalItemInfoPanel.EffectIcon.enabled = false;
            }
        }
        else if (item.itemType == InventoryType.Armor)
        {
            NormalItemInfoPanel.ItemIcon.sprite = item.armor.icon;
            NormalItemInfoPanel.ItemNameText.text = item.armor.armorName;
            NormalItemInfoPanel.ItemDescText.text = item.armor.EffectDescription;
            NormalItemInfoPanel.ItemEffectDescText.text = item.armor.ItemDescription;
            if (item.armor.equipPassive != null)
            {
                NormalItemInfoPanel.EffectNameText.text = item.armor.equipPassive.passiveName;
                NormalItemInfoPanel.EffectDescText.text = item.armor.equipPassive.description;
                NormalItemInfoPanel.EffectIcon.sprite = item.armor.equipPassive.icon;
            }
            else
            {
                NormalItemInfoPanel.EffectNameText.text = "";
                NormalItemInfoPanel.EffectDescText.text = "";
                NormalItemInfoPanel.EffectIcon.enabled = false;
            }
        }
        else if (item.itemType == InventoryType.SkillBook)
        {
            NormalItemInfoPanel.ItemIcon.sprite = item.skillBook.icon;
            NormalItemInfoPanel.ItemNameText.text = item.skillBook.skillBookName;
            NormalItemInfoPanel.ItemDescText.text = item.skillBook.skillBookItemDesc;

            System.Text.StringBuilder effectBuilder = new System.Text.StringBuilder();

            if (item.skillBook.skillTree != null)
            {
                foreach (var node in item.skillBook.skillTree)
                {
                    string alphaTag = node.unlocked ? "#FF" : "#80";
                    effectBuilder.AppendLine($"<alpha={alphaTag}>{node.GetNodeName()}");
                }
            }

            NormalItemInfoPanel.ItemEffectDescText.text = effectBuilder.ToString();
            NormalItemInfoPanel.EffectNameText.text = "";
            NormalItemInfoPanel.EffectDescText.text = "";
            NormalItemInfoPanel.EffectIcon.enabled = false;
        }
        else if (item.itemType == InventoryType.Item)
        {
            if(item.consumable != null)
            {
                NormalItemInfoPanel.ItemIcon.sprite = item.consumable.icon;
                NormalItemInfoPanel.ItemNameText.text = item.consumable.ConsumableName;
                NormalItemInfoPanel.ItemDescText.text = item.consumable.ItemDescription;
                NormalItemInfoPanel.ItemEffectDescText.text = item.consumable.EffectDescription;
                NormalItemInfoPanel.EffectNameText.text = "";
                NormalItemInfoPanel.EffectDescText.text = "";
                NormalItemInfoPanel.EffectIcon.enabled = false;
            }
            else if (item.nonConsumable != null)
            {
                NormalItemInfoPanel.ItemIcon.sprite = item.nonConsumable.icon;
                NormalItemInfoPanel.ItemNameText.text = item.nonConsumable.NonConsumableName;
                NormalItemInfoPanel.ItemDescText.text = item.nonConsumable.ItemDescription;
                NormalItemInfoPanel.ItemEffectDescText.text = item.nonConsumable.EffectDescription;
                NormalItemInfoPanel.EffectNameText.text = "";
                NormalItemInfoPanel.EffectDescText.text = "";
                NormalItemInfoPanel.EffectIcon.enabled = false;
            }

        }
        InventorySlot Slot = item.GetComponentInParent<InventorySlot>();
        float scaleFactor = InventoryBG.GetComponentInParent<Canvas>().scaleFactor;

        RectTransform infoRect = NormalItemInfoPanel.GetComponent<RectTransform>();

        float syncPivotY = Slot.CalculatedPivotY;
        infoRect.pivot = new Vector2(0f, syncPivotY);

        NormalItemInfoPanel.transform.position = Slot.transform.position + (Slot.TooltipOffset * scaleFactor);
        NormalItemInfoPanel.gameObject.SetActive(true);
    }

    public void HideItemTooltip()
    {
        NormalItemInfoPanel.gameObject.SetActive(false);
    }

    public InventorySlot selectedItem;
    public void ShowItmeContextMenu()
    {
        ItemContextMenu.transform.position = selectedItem.transform.position + (selectedItem.ContextMenuOffset * InventoryBG.GetComponentInParent<Canvas>().scaleFactor);

        ItemContextMenu.gameObject.SetActive(true);
        ItemContextMenu.slot = null;
        if (selectedItem.item.weapon != null || selectedItem.item.armor != null) 
        { ItemContextMenu.OnEquipmentContextMenu(); selectedItem.canvasGroup.blocksRaycasts = false; ItemContextMenu.slot = selectedItem; }
        else if (selectedItem.item.isQuickSlot ) { ItemContextMenu.OnQuickSlotContextMenu(); selectedItem.canvasGroup.blocksRaycasts = false; ItemContextMenu.slot = selectedItem; }
        else if (selectedItem.item.consumable != null) { ItemContextMenu.OnConsumableContextMenu(); selectedItem.canvasGroup.blocksRaycasts = false; ItemContextMenu.slot = selectedItem; }
        else if (selectedItem.item.nonConsumable != null) { ItemContextMenu.OnNonConsumableContextMenu(); selectedItem.canvasGroup.blocksRaycasts = false; ItemContextMenu.slot = selectedItem; }
        else if (selectedItem.item.skillBook != null) { HideItemContextMenu(); ShowSkillBookDetail(selectedItem.item.skillBook); }
        else { return; }
    }

    public void HideItemContextMenu()
    {
        if(selectedItem != null)
            selectedItem.canvasGroup.blocksRaycasts = true;
        ItemContextMenu.gameObject.SetActive(false);
    }

    GameObject LastSkillTree;
    public Sprite StatIcon;

    public void ShowSkillBookDetail(SkillBookBase skillBook)
    {
        if (LastSkillTree != null)
            Destroy(LastSkillTree);

        if (skillBook == null || skillBook.SkillTreePrefab == null) return;
        SoundManager.Instance.PlayUISound(4);
        LastSkillTree = Instantiate(skillBook.SkillTreePrefab, SkillBookPanel);

        BookInfo.SetActive(true);
        TreeInfo.SetActive(false);
        BookInfo.GetComponentsInChildren<TextMeshProUGUI>()[1].text = skillBook.skillBookFirstPage;
        BookInfo.GetComponentsInChildren<TextMeshProUGUI>()[0].text = skillBook.skillBookName;
        var skillUIs = LastSkillTree.GetComponentsInChildren<SkillNodeUI>(true);
        if (skillBook.skillTree != null && skillUIs != null)
        {
            foreach (var nodeUI in skillUIs)
            {
                int exactIndex = nodeUI.nodeIndex;

                if (exactIndex >= 0 && exactIndex < skillBook.skillTree.Length)
                {
                    var nodeData = skillBook.skillTree[exactIndex];

                    nodeUI.Init(nodeData, skillBook, exactIndex, StatIcon);
                }
            }
        }
    }
    private void HideAllTabs()
    {
        if (Inventory != null) Inventory.SetActive(false);
        if (InformationTab != null) InformationTab.SetActive(false);
        if (GradeTab != null) GradeTab.SetActive(false);
        if (SkillBookInventory != null) SkillBookInventory.SetActive(false);
        
        if (ItemInventory != null) ItemInventory.SetActive(false);


        if (WeaponInventory != null) WeaponInventory.SetActive(false);
        if (ArmorInventory != null) ArmorInventory.SetActive(false);
        if (EquippedWeaponSlot != null) EquippedWeaponSlot.gameObject.SetActive(false);
        if (EquippedArmorSlot != null) EquippedArmorSlot.gameObject.SetActive(false);
        if (EquipItemInfoPanel != null) EquipItemInfoPanel.gameObject.SetActive(false);
        if (ItemContextMenu != null) ItemContextMenu.gameObject.SetActive(false);

        if (WeaponButton != null) WeaponButton.gameObject.SetActive(false);
        if (ArmorButton != null) ArmorButton.gameObject.SetActive(false);

        // 새로 추가된 세팅 서브 버튼 및 패널 숨김 처리
        if (SettingGameButton != null) SettingGameButton.gameObject.SetActive(false);
        if (SettingKeyButton != null) SettingKeyButton.gameObject.SetActive(false);
        if (SettingCreditButton != null) SettingCreditButton.gameObject.SetActive(false);
        if (SettingGamePanel != null) SettingGamePanel.SetActive(false);
        if (SettingKeyPanel != null) SettingKeyPanel.SetActive(false);
        if (SettingCreditPanel != null) SettingCreditPanel.SetActive(false);
        MouseItemInfo.SetActive(false);
        PeachTree.Instance.gameObject.SetActive(false);
        GradeManager.Instance.gradePanel.DisableUI(0f);
        GradeManager.Instance.CloseGradePanel();
        SkillBookPanel.gameObject.SetActive(false);
        TreeInfo.SetActive(false);
        Book.SetActive(false);
        BookInfo.SetActive(false);

        EquipmentButton.GetComponentInChildren<TextMeshProUGUI>().color = new Color(1, 1, 1, 100 / 255f);
        SkillBookButton.GetComponentInChildren<TextMeshProUGUI>().color = new Color(1, 1, 1, 100 / 255f);
        ItemButton.GetComponentInChildren<TextMeshProUGUI>().color = new Color(1, 1, 1, 100 / 255f);
        InfoButton.GetComponentInChildren<TextMeshProUGUI>().color = new Color(1, 1, 1, 100 / 255f);
        GradeButton.GetComponentInChildren<TextMeshProUGUI>().color = new Color(1, 1, 1, 100 / 255f);
        SettingButton.GetComponentInChildren<TextMeshProUGUI>().color = new Color(1, 1, 1, 100 / 255f);

        WeaponButton.GetComponentInChildren<TextMeshProUGUI>().color = new Color(0, 0, 0, 150 / 255f);
        ArmorButton.GetComponentInChildren<TextMeshProUGUI>().color = new Color(0, 0, 0, 150 / 255f);
        if (SettingGameButton != null) SettingGameButton.GetComponentInChildren<TextMeshProUGUI>().color = new Color(0, 0, 0, 150 / 255f);
        if (SettingKeyButton != null) SettingKeyButton.GetComponentInChildren<TextMeshProUGUI>().color = new Color(0, 0, 0, 150 / 255f);
        if (SettingCreditButton != null) SettingCreditButton.GetComponentInChildren<TextMeshProUGUI>().color = new Color(0, 0, 0, 150 / 255f);
        foreach (Camera cam in cameras)
        {
            if (cam != null)
                cam.gameObject.SetActive(false);
        }
        QuitPanel.gameObject.SetActive(false);
        TitlePanel.gameObject.SetActive(false);
        SettingTab.gameObject.SetActive(false);
        QuickSlot.SetActive(false);
        UIManager.Instance.GameQuickSlot.SetActive(false);
        UIManager.Instance.InteractPanel.SetActive(false);

        GradeManager.Instance.gradePanel.gameObject.SetActive(false);
        Destroy(LastSkillTree);
    }

    // ----- [추가된 부분: 배경 적용 헬퍼 메서드] -----
    private void SetInventoryBackground(int index)
    {
        if (backgrounds != null && backgrounds.Length > index && backgrounds[index] != null)
        {
            RawImage rawImage = InventoryBG.GetComponent<RawImage>();
            if (rawImage != null)
            {
                rawImage.texture = backgrounds[index];
            }
            cameras[index].gameObject.SetActive(true);
        }
    }
    // --------------------------------------------------

    public void OnClickEquipmentTab()
    {
        if (currentTab == TabType.Equipment) return;

        HideAllTabs();
        currentTab = TabType.Equipment;

        // ----- [추가됨] -----
        SetInventoryBackground(2);

        Inventory.SetActive(true);
        if (WeaponButton != null) WeaponButton.gameObject.SetActive(true);
        if (ArmorButton != null) ArmorButton.gameObject.SetActive(true);
        EquipmentButton.GetComponentInChildren<TextMeshProUGUI>().color = new Color(1, 1, 1, 1);
        OnClickWeaponSubTab();
    }

    public void OnClickInfoTab()
    {
        if (currentTab == TabType.Info) return;

        HideAllTabs();
        currentTab = TabType.Info;

        // ----- [추가됨] -----
        SetInventoryBackground(0);

        if (InformationTab != null) InformationTab.SetActive(true);
        InfoButton.GetComponentInChildren<TextMeshProUGUI>().color = new Color(1, 1, 1, 1);
    }

    public void OnClickGradeTab()
    {
        if (currentTab == TabType.Grade) return;

        HideAllTabs();
        currentTab = TabType.Grade;

        // ----- [추가됨] -----
        SetInventoryBackground(1);

        if (GradeTab != null) GradeTab.SetActive(true);
        PeachTree.Instance.gameObject.SetActive(true);

        GradeManager.Instance.OnFlowerUIOpened();
        GradeButton.GetComponentInChildren<TextMeshProUGUI>().color = new Color(1, 1, 1, 1);
    }

    public void OnClickSkillBookTab()
    {
        if (currentTab == TabType.SkillBook) return;

        HideAllTabs();
        currentTab = TabType.SkillBook;

        // ----- [추가됨] -----
        SetInventoryBackground(3);

        SkillBookPanel.gameObject.SetActive(true);
        Inventory.SetActive(true);
        Book.SetActive(true);
        if (SkillBookInventory != null) SkillBookInventory.SetActive(true);
        SkillBookButton.GetComponentInChildren<TextMeshProUGUI>().color = new Color(1, 1, 1, 1);
    }

    public void OnClickItemTab()
    {
        if (currentTab == TabType.Item) return;

        HideAllTabs();
        currentTab = TabType.Item;

        // ----- [추가됨] -----
        SetInventoryBackground(4);

        Inventory.SetActive(true);
        ItemButton.GetComponentInChildren<TextMeshProUGUI>().color = new Color(1, 1, 1, 1);

        QuickSlot.SetActive(true);
        if (ItemInventory != null) ItemInventory.SetActive(true);
    }

    public void OnClickWeaponSubTab()
    {
        if (currentTab == TabType.WeaponSub) return;

        HideAllTabs();
        currentTab = TabType.WeaponSub;

        // ----- [추가됨] -----
        SetInventoryBackground(2);

        Inventory.SetActive(true);
        if (WeaponButton != null) WeaponButton.gameObject.SetActive(true);
        if (ArmorButton != null) ArmorButton.gameObject.SetActive(true);
        if (ArmorInventory != null) ArmorInventory.SetActive(false);
        if (EquippedArmorSlot != null) EquippedArmorSlot.gameObject.SetActive(false);

        if (WeaponInventory != null) WeaponInventory.SetActive(true);
        if (EquippedWeaponSlot != null) EquippedWeaponSlot.gameObject.SetActive(true);
        if (EquipItemInfoPanel != null) EquipItemInfoPanel.gameObject.SetActive(true);

        EquipmentButton.GetComponentInChildren<TextMeshProUGUI>().color = new Color(1, 1, 1, 1);
        WeaponButton.GetComponentInChildren<TextMeshProUGUI>().color = new Color(0, 0, 0, 1);
        SetInfoPanel(0);
    }

    public void OnClickArmorSubTab()
    {
        if (currentTab == TabType.ArmorSub) return;

        HideAllTabs();
        currentTab = TabType.ArmorSub;

        // ----- [추가됨] -----
        SetInventoryBackground(2);

        Inventory.SetActive(true);
        if (WeaponButton != null) WeaponButton.gameObject.SetActive(true);
        if (ArmorButton != null) ArmorButton.gameObject.SetActive(true);
        if (WeaponInventory != null) WeaponInventory.SetActive(false);
        if (EquippedWeaponSlot != null) EquippedWeaponSlot.gameObject.SetActive(false);

        if (ArmorInventory != null) ArmorInventory.SetActive(true);
        if (EquippedArmorSlot != null) EquippedArmorSlot.gameObject.SetActive(true);
        if (EquipItemInfoPanel != null) EquipItemInfoPanel.gameObject.SetActive(true);
        EquipmentButton.GetComponentInChildren<TextMeshProUGUI>().color = new Color(1, 1, 1, 1);
        ArmorButton.GetComponentInChildren<TextMeshProUGUI>().color = new Color(0, 0, 0, 1);
        SetInfoPanel(1);
    }

    // Setting 메인 탭 클릭 시 (기본으로 Game 서브 탭을 열도록 변경)
    public void OnClickSettingTab()
    {
        if (currentTab == TabType.Setting) return;

        HideAllTabs();
        currentTab = TabType.Setting;

        // ----- [추가됨] -----
        SetInventoryBackground(5);

        SettingTab.gameObject.SetActive(true);
        if (SettingGameButton != null) SettingGameButton.gameObject.SetActive(true);
        if (SettingKeyButton != null) SettingKeyButton.gameObject.SetActive(true);
        if (SettingCreditButton != null) SettingCreditButton.gameObject.SetActive(true);

        SettingButton.GetComponentInChildren<TextMeshProUGUI>().color = new Color(1, 1, 1, 1);

        OnClickSettingGameSubTab();
    }

    // 추가됨: Game 서브 탭 클릭 로직
    public void OnClickSettingGameSubTab()
    {
        if (currentTab == TabType.SettingGameSub) return;

        HideAllTabs();
        currentTab = TabType.SettingGameSub;

        // ----- [추가됨] -----
        SetInventoryBackground(5);

        SettingTab.gameObject.SetActive(true);
        if (SettingGameButton != null) SettingGameButton.gameObject.SetActive(true);
        if (SettingKeyButton != null) SettingKeyButton.gameObject.SetActive(true);
        if (SettingCreditButton != null) SettingCreditButton.gameObject.SetActive(true);

        if (SettingGamePanel != null) SettingGamePanel.SetActive(true);
        if (SettingKeyPanel != null) SettingKeyPanel.SetActive(false);
        if (SettingCreditPanel != null) SettingCreditPanel.gameObject.SetActive(false);

        SettingButton.GetComponentInChildren<TextMeshProUGUI>().color = new Color(1, 1, 1, 1);
        if (SettingGameButton != null) SettingGameButton.GetComponentInChildren<TextMeshProUGUI>().color = new Color(0, 0, 0, 1);
    }

    // 추가됨: Key 서브 탭 클릭 로직
    public void OnClickSettingKeySubTab()
    {
        if (currentTab == TabType.SettingKeySub) return;

        HideAllTabs();
        currentTab = TabType.SettingKeySub;

        // ----- [추가됨] -----
        SetInventoryBackground(5);

        SettingTab.gameObject.SetActive(true);
        if (SettingGameButton != null) SettingGameButton.gameObject.SetActive(true);
        if (SettingKeyButton != null) SettingKeyButton.gameObject.SetActive(true);
        if (SettingCreditButton != null) SettingCreditButton.gameObject.SetActive(true);

        if (SettingGamePanel != null) SettingGamePanel.SetActive(false);
        if (SettingKeyPanel != null) SettingKeyPanel.SetActive(true);
        if (SettingCreditPanel != null) SettingCreditPanel.gameObject.SetActive(false);

        SettingButton.GetComponentInChildren<TextMeshProUGUI>().color = new Color(1, 1, 1, 1);
        if (SettingKeyButton != null) SettingKeyButton.GetComponentInChildren<TextMeshProUGUI>().color = new Color(0, 0, 0, 1);
    }

    public void OnClickSettingCreditSubTab()
    {
        if (currentTab == TabType.SettingCreditSub) return;

        HideAllTabs();
        currentTab = TabType.SettingCreditSub;

        // ----- [추가됨] -----
        SetInventoryBackground(5);

        SettingTab.gameObject.SetActive(true);
        if (SettingGameButton != null) SettingGameButton.gameObject.SetActive(true);
        if (SettingKeyButton != null) SettingKeyButton.gameObject.SetActive(true);
        if (SettingCreditButton != null) SettingCreditButton.gameObject.SetActive(true);

        if (SettingGamePanel != null) SettingGamePanel.SetActive(false);
        if (SettingKeyPanel != null) SettingKeyPanel.SetActive(false);
        if (SettingCreditPanel != null) SettingCreditPanel.gameObject.SetActive(true);

        SettingButton.GetComponentInChildren<TextMeshProUGUI>().color = new Color(1, 1, 1, 1);
        if (SettingCreditButton != null) SettingCreditButton.GetComponentInChildren<TextMeshProUGUI>().color = new Color(0, 0, 0, 1);
    }

    public void ResetTabState()
    {
        currentTab = TabType.None;
    }

    public int GetItemCount(string itemName)
    {
        int count = 0;

        foreach (InventorySlot slot in ItemSlots)
        {
            if (slot != null && slot.item != null && slot.item.consumable != null)
            {
                if (slot.item.consumable.name == itemName)
                    count += slot.item.Count;
            }
            if (slot != null && slot.item != null && slot.item.nonConsumable != null)
            {
                if (slot.item.nonConsumable.name == itemName)
                    count += slot.item.Count;
            }
        }

        if (UIManager.Instance != null && UIManager.Instance.GameQuickSlots != null)
        {
            foreach (QuickSlot slot in UIManager.Instance.GameQuickSlots)
            {
                if (slot != null && slot.item != null)
                {
                    if (slot.item.consumable != null && slot.item.consumable.name == itemName)
                        count += slot.item.Count;
                    else if (slot.item.nonConsumable != null && slot.item.nonConsumable.name == itemName)
                        count += slot.item.Count;
                }
            }
        }

        foreach (InventorySlot slot in WeaponSlots)
        {
            if (slot != null && slot.item != null && slot.item.weapon != null && slot.item.weapon.name == itemName)
                count++;
        }

        if (EquippedWeaponSlot != null && EquippedWeaponSlot.item != null && EquippedWeaponSlot.item.weapon != null)
        {
            if (EquippedWeaponSlot.item.weapon.name == itemName)
                count++;
        }

        foreach (InventorySlot slot in ArmorSlots)
        {
            if (slot != null && slot.item != null && slot.item.armor != null && slot.item.armor.name == itemName)
                count++;
        }

        if (EquippedArmorSlot != null && EquippedArmorSlot.item != null && EquippedArmorSlot.item.armor != null)
        {
            if (EquippedArmorSlot.item.armor.name == itemName)
                count++;
        }

        foreach (InventorySlot slot in SkillBookSlots)
        {
            if (slot != null && slot.item != null && slot.item.skillBook != null && slot.item.skillBook.name == itemName)
                count++;
        }

        return count;
    }

    public void CheckAllSkillBooksAutoUnlocksFromSlots()
    {
        if (SkillBookSlots == null) return;

        foreach (InventorySlot slot in SkillBookSlots)
        {
            if (slot != null && slot.item != null && slot.item.skillBook != null)
            {
                slot.item.skillBook.CheckAndProcessAutoUnlocks();
            }
        }
    }
}