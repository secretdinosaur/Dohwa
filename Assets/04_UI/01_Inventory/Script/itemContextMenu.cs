using DG.Tweening;
using JetBrains.Annotations;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;


public class itemContextMenu : MonoBehaviour
{
    public InventorySlot slot;
    [Header("Equipment")]
    public GameObject EquipmentContextMenu;

    [Header("Consumable")]
    public GameObject ConsumableContextMenu;

    [Header("Nonconsumable")]
    public GameObject NonConsumableContextMenu;

    [Header("QuickSlot")]
    public GameObject QuickSlotContextMenu;

    public void OnEquipmentContextMenu()
    {
        if (EquipmentContextMenu != null) EquipmentContextMenu.SetActive(true);
        if (ConsumableContextMenu != null) ConsumableContextMenu.SetActive(false);
        if (NonConsumableContextMenu != null) NonConsumableContextMenu.SetActive(false);
        if (QuickSlotContextMenu != null) QuickSlotContextMenu.SetActive(false);
    }
    public void OnConsumableContextMenu()
    {
        if (EquipmentContextMenu != null) EquipmentContextMenu.SetActive(false);
        if (ConsumableContextMenu != null) ConsumableContextMenu.SetActive(true);
        if (NonConsumableContextMenu != null) NonConsumableContextMenu.SetActive(false);
        if (QuickSlotContextMenu != null) QuickSlotContextMenu.SetActive(false);
    }
    public void OnNonConsumableContextMenu()
    {
        if (EquipmentContextMenu != null) EquipmentContextMenu.SetActive(false);
        if (ConsumableContextMenu != null) ConsumableContextMenu.SetActive(false);
        if (NonConsumableContextMenu != null) NonConsumableContextMenu.SetActive(true);
        if (QuickSlotContextMenu != null) QuickSlotContextMenu.SetActive(false);
    }
    public void OnQuickSlotContextMenu()
    {
        if (EquipmentContextMenu != null) EquipmentContextMenu.SetActive(false);
        if (ConsumableContextMenu != null) ConsumableContextMenu.SetActive(false);
        if (NonConsumableContextMenu != null) NonConsumableContextMenu.SetActive(false);
        if (QuickSlotContextMenu != null) QuickSlotContextMenu.SetActive(true);
    }

    public void OnEquip()
    {
        switch (slot.allowedType)
        {
            case InventoryManager.InventoryType.Weapon:
                if (slot.item.weapon != null)
                    InventoryManager.Instance.EquipWeapon(slot.item.weapon, slot);
                InventoryManager.Instance.RemoveNullSlots(InventoryManager.InventoryType.Weapon);
                InventoryManager.Instance.UpdateSlots(InventoryManager.InventoryType.Weapon);
                break;
            case InventoryManager.InventoryType.Armor:
                if (slot.item.armor != null)
                    InventoryManager.Instance.EquipArmor(slot.item.armor, slot);
                InventoryManager.Instance.RemoveNullSlots(InventoryManager.InventoryType.Armor);
                InventoryManager.Instance.UpdateSlots(InventoryManager.InventoryType.Armor);
                break;
        }
        InventoryManager.Instance.HideItemContextMenu();
        InventoryManager.Instance.HideItemTooltip();
    }

    public void OnSetQuickSlot()
    {
        QuickSlot slot = null;
        for(int i = 0; i < UIManager.Instance.GameQuickSlots.Length; i++)
        {
            if (UIManager.Instance.GameQuickSlots[i].item.consumable == null &&
                UIManager.Instance.GameQuickSlots[i].item.nonConsumable == null)
            {
                slot = UIManager.Instance.GameQuickSlots[i].GetComponent<QuickSlot>();
                break;
            }
        }
        if (slot != null)
        {
            var selectedItem = InventoryManager.Instance.selectedItem.item;
            if (selectedItem.consumable != null)
            {
                slot.item.SetItem(selectedItem.consumable);
                slot.item.Count = selectedItem.Count;
            }
            else if (selectedItem.nonConsumable != null)
            {
                slot.item.SetItem(selectedItem.nonConsumable);
                slot.item.Count = selectedItem.Count;
            }
            selectedItem.Count = 0;
            InventoryManager.Instance.RemoveNullSlots(InventoryManager.InventoryType.Item);
            InventoryManager.Instance.UpdateSlots(InventoryManager.InventoryType.Item);
        }
        InventoryManager.Instance.HideItemContextMenu();
        InventoryManager.Instance.HideItemTooltip();
        UIManager.Instance.SyncGameQuickSlots();
    }

    public void OnRemoveQuickSlot()
    {
        var selectedItem = InventoryManager.Instance.selectedItem.GetComponent<QuickSlot>().OriginalSlot.GetComponent<QuickSlot>();
        if (selectedItem.item.consumable != null)
        {
            Consumable item = selectedItem.item.consumable;
            int count = selectedItem.item.Count;
            selectedItem.item.Count = 0;
            selectedItem.SetSlot();

            for (int i = 0; i < count; i++)
            {
                InventoryManager.Instance.AddItem(item);
            }
        }
        else if (selectedItem.item.nonConsumable != null)
        {
            NonConsumable item = selectedItem.item.nonConsumable;
            int count = selectedItem.item.Count;
            selectedItem.item.Count = 0;
            selectedItem.SetSlot();

            for (int i = 0; i < count; i++)
            {
                InventoryManager.Instance.AddItem(item);
            }
        }
        InventoryManager.Instance.RemoveNullSlots(InventoryManager.InventoryType.Item);
        InventoryManager.Instance.UpdateSlots(InventoryManager.InventoryType.Item);
        InventoryManager.Instance.HideItemContextMenu();
        InventoryManager.Instance.HideItemTooltip();
        UIManager.Instance.SyncGameQuickSlots();
    }

    public void OnUse()
    {
        var item = InventoryManager.Instance.selectedItem.item;
        if ((item.consumable != null && item.consumable.isEatable) || (item.nonConsumable != null && item.nonConsumable.isEatable))
            Player.Instance.StartUseItemRoutine(InventoryManager.Instance.selectedItem);
        else if (slot.item.consumable != null)
        {
            if(slot.item.Count == 1)
            {
                InventoryManager.Instance.HideItemContextMenu();
                InventoryManager.Instance.HideItemTooltip();
            }
            slot.item.consumable.Use();
            slot.item.Count--;
            InventoryManager.Instance.RemoveNullSlots(InventoryManager.InventoryType.Item);
            InventoryManager.Instance.UpdateSlots(InventoryManager.InventoryType.Item);
        }
        else if (slot.item.nonConsumable != null)
        {
            slot.item.nonConsumable.Use();
        }
        UIManager.Instance.SyncGameQuickSlots();
    }

    public void OnDelete()
    {
        if ((slot.item.weapon != null && slot.item.weapon.isDeletable)
            || (slot.item.armor != null && slot.item.armor.isDeletable)
            || (slot.item.consumable != null && slot.item.consumable.isDeletable)
            || (slot.item.nonConsumable != null && slot.item.nonConsumable.isDeletable))
        {
            OpenCheckDeleteItemPenel();
        }
        else if((slot.item.weapon != null && !slot.item.weapon.isDeletable)
            || (slot.item.armor != null && !slot.item.armor.isDeletable)
            || (slot.item.consumable != null && !slot.item.consumable.isDeletable)
            || (slot.item.nonConsumable != null && !slot.item.nonConsumable.isDeletable))
        {
            ShowCantDeleteMessagePanel();
        }
    }

    private void ShowCantDeleteMessagePanel()
    {
        CanvasGroup canvasGroup = InventoryManager.Instance.CantDeleteMessagePanel.GetComponent<CanvasGroup>();
        canvasGroup.DOKill();

        canvasGroup.alpha = 0f;

        Sequence fadeSequence = DOTween.Sequence();

        fadeSequence
            .Append(canvasGroup.DOFade(1f, 0.2f))
            .AppendInterval(2f)
            .Append(canvasGroup.DOFade(0f, 0.2f));
    }

    Coroutine checkDeleteCoroutine;
    public void OpenCheckDeleteItemPenel()
    {
        checkDeleteCoroutine = StartCoroutine(ShowCheckDeleteItemPenel());
    }
    public IEnumerator ShowCheckDeleteItemPenel()
    {
        InventoryManager.Instance.CheckDeleteItemPenel.SetActive(true);
        Button[] buttons = InventoryManager.Instance.CheckDeleteItemPenel.GetComponentsInChildren<Button>();

        buttons[0].onClick.RemoveAllListeners();
        buttons[0].onClick.AddListener(() => {
            StopCoroutine(checkDeleteCoroutine);
            InventoryManager.Instance.CheckDeleteItemPenel.SetActive(false);
        });

        buttons[1].onClick.RemoveAllListeners();
        buttons[1].onClick.AddListener(() => {
            if (slot.item.weapon != null && slot.item.weapon.isDeletable)
            {
                slot.item.Count -= slot.item.Count;
                InventoryManager.Instance.RemoveNullSlots(InventoryManager.InventoryType.Weapon);
                InventoryManager.Instance.UpdateSlots(InventoryManager.InventoryType.Weapon);
            }
            else if (slot.item.armor != null && slot.item.armor.isDeletable)
            {
                slot.item.Count -= slot.item.Count;
                InventoryManager.Instance.RemoveNullSlots(InventoryManager.InventoryType.Armor);
                InventoryManager.Instance.UpdateSlots(InventoryManager.InventoryType.Armor);
            }
            else if (slot.item.consumable != null && slot.item.consumable.isDeletable)
            {
                slot.item.Count -= slot.item.Count;
                InventoryManager.Instance.RemoveNullSlots(InventoryManager.InventoryType.Item);
                InventoryManager.Instance.UpdateSlots(InventoryManager.InventoryType.Item);
            }
            else if (slot.item.nonConsumable != null && slot.item.nonConsumable.isDeletable)
            {
                slot.item.Count -= slot.item.Count;
                InventoryManager.Instance.RemoveNullSlots(InventoryManager.InventoryType.Item);
                InventoryManager.Instance.UpdateSlots(InventoryManager.InventoryType.Item);
            }
            InventoryManager.Instance.HideItemContextMenu();
            InventoryManager.Instance.HideItemTooltip();
            UIManager.Instance.SyncGameQuickSlots();
            InventoryManager.Instance.CheckDeleteItemPenel.SetActive(false);
            StopCoroutine(checkDeleteCoroutine);
        });

        SoundManager.Instance.PlayUISound(1);
        yield return new WaitUntil(() => ((Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)));
        InventoryManager.Instance.CheckDeleteItemPenel.SetActive(false);
    }
}
