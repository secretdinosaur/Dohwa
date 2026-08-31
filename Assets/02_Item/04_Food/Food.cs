using System.Collections;
using UnityEngine;
using UnityEngine.Localization;

[CreateAssetMenu(menuName = "Item/Consumable/Food")]
public class Food : Consumable
{
    public float HealPercentage;
    public float HealAmount;

    public override void Use()
    {
        float totalHeal = (StatManager.Instance.MaxHp * HealPercentage / 100 + HealAmount) * StatManager.Instance.HpRecoveryAmount;

        // ScriptableObject는 코루틴을 직접 실행할 수 없어 Player.Instance 위에서 실행
        Player.Instance.StartCoroutine(HealOverTime(totalHeal, 3f));
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