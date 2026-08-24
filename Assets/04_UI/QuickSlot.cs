using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class QuickSlot : InventorySlot
{
    public InventorySlot OriginalSlot;
    public Image Cooldown;

    public static float globalQuickSlotDelay = 5f;
    public float nextAllowedUseTime = 0f;

    public void SetSlot()
    {
        OriginalSlot.item.Clear();
        if (item.consumable != null)
        {
            OriginalSlot.item.SetItem(item.consumable);
        }
        else if (item.nonConsumable)
        {
            OriginalSlot.item.SetItem(item.nonConsumable);
        }
        OriginalSlot.item.Count = item.Count;
    }
    public void OnClickQuickSlot()
    {
        Player.Instance.StartUseItemRoutine(this);
    }
}