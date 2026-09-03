using System.Collections;
using UnityEngine;
[CreateAssetMenu(menuName = "Item/NonConsumable/Potion")]
public class Potion : NonConsumable
{
    public int MaxAmount;
    public int LeftAmount;

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
