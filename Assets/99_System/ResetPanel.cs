using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ResetPanel : MonoBehaviour
{
    public TMP_InputField reset;
    public GameObject IncorrectAlert;

    public void OnClickResetButton()
    {
        if (reset.text.Equals("RESET"))
        {
            DataManager.Instance.ResetData();
        }
        else
        {
            IncorrectAlert.SetActive(true);
        }
    }

    public void OnClickCancelButton()
    {
        reset.text = "";
        IncorrectAlert.SetActive(false);
        gameObject.SetActive(false);
    }

    
}