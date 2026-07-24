using UnityEngine;
using UnityEngine.Localization;

[CreateAssetMenu(menuName = "Item/NonConsumable/TeacherPiece")]
public class TeacherPiece : NonConsumable
{
    public DialogueSet[] dialogueSets;
    public override void Use()
    {
        if (DialogueManager.Instance != null)
        {
            if(UIManager.Instance == null) return;
            if(UIManager.Instance.Inventory.activeSelf)
                UIManager.Instance.ToggleInventory();
            InventoryManager.Instance.HideItemContextMenu();
            InventoryManager.Instance.HideItemTooltip();
            if (dialogueSets.Length > 1)
            {
                DialogueManager.Instance.StartShowDialogue(dialogueSets[UnityEngine.Random.Range(0, dialogueSets.Length)]);
            }
            else
            {
                DialogueManager.Instance.StartShowDialogue(dialogueSets[0]);
            }
        }
    }
}