using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class B0_Blind : MonoBehaviour
{
    public Image Blind;

    public void SetBlind(int idx)
    {
        Blind.rectTransform.sizeDelta = new Vector2(500, 800 - idx);
    }
}
