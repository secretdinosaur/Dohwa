using UnityEngine;

public class CheckPointPanel : MonoBehaviour
{
    private void OnEnable()
    {
        UIManager.Instance.PreventCameraMoveAndClick(true);

    }

    private void OnDisable()
    {
        UIManager.Instance.PreventCameraMoveAndClick(false);

    }
}
