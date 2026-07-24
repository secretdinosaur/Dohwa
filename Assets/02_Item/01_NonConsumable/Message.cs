using UnityEngine;
using UnityEngine.Localization;

[CreateAssetMenu(menuName = "Item/NonConsumable/Message")]
public class Message : NonConsumable
{
    public LocalizedString Localize_Message;
    public string MessageText => Localize_Message.GetLocalizedString();
    public override void Use()
    {
        if(UIManager.Instance != null)
        {
            UIManager.Instance.StartMessageRoutine(MessageText);
        }
    }
}