using UnityEngine;

public class Food_GetSP : Food
{
    public int SpAmount = 0;
    public override void Use()
    {
        StatManager.Instance.GetStatPoint(SpAmount);

    }
}
