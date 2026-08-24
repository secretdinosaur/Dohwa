using UnityEngine;
using UnityEngine.Localization;

[CreateAssetMenu(menuName = "Item/Consumable/Food")]
public class Food : Consumable
{
    public float HealPercentage;
    public float HealAmount;
    
    public override void Use()
    {
        Player.Instance.HP += (StatManager.Instance.MaxHp * HealPercentage / 100 + HealAmount) * StatManager.Instance.HpRecoveryAmount;
    }
}