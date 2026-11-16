using System.Collections;
using UnityEngine;
[CreateAssetMenu(menuName = "Item/NonConsumable/Potion")]
public class Potion : NonConsumable
{
    public Sprite icon2; // 반절이상
    public Sprite icon3; // 반절이하
    public Sprite icon4; // 0

    public float HealPercentage;
    public float HealAmount;

    public override void Use()
    {
        if(LeftAmount > 0)
        {
            float totalHeal = (StatManager.Instance.MaxHp * HealPercentage / 100 + HealAmount) * StatManager.Instance.HpRecoveryAmount;

            // ScriptableObject는 코루틴을 직접 실행할 수 없어 Player.Instance 위에서 실행
            Player.Instance.StartCoroutine(HealOverTime(totalHeal, 3f));
            LeftAmount--;
            if(InventoryManager.Instance != null)
            {
                InventorySlot slot = InventoryManager.Instance.FindItem(InventoryManager.InventoryType.Item, "3abef340-ac0e-4538-9727-e386d2ef4e53");
                if(slot != null)
                {
                    float ratio = (float)LeftAmount / MaxAmount;

                    if (LeftAmount <= 0)
                    {
                        slot.item.icon.sprite = icon4;
                    }
                    else if (ratio <= 0.5f)
                    {
                        slot.item.icon.sprite = icon3;
                    }
                    else if (ratio > 0.5f && LeftAmount != MaxAmount)
                    {
                        slot.item.icon.sprite = icon2;
                    }
                    else
                    {
                        slot.item.icon.sprite = icon;
                    }
                }
            }
        }
    }

    private IEnumerator HealOverTime(float totalHeal, float duration)
    {
        float elapsed = 0f;
        float healPerSecond = totalHeal / duration;

        while (elapsed < duration)
        {
            Player.Instance.HP += healPerSecond * Time.deltaTime;
            elapsed += Time.deltaTime;
            yield return null;
        }
    }
}
