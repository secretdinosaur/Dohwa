using UnityEngine;

public class Food_Recovery : Food
{
    public float HpRecoveryAmount = 0f;

    public override void Use()
    {
        Player.Instance.HP += HpRecoveryAmount * StatManager.Instance.HpRecoveryAmount;
    }
}
