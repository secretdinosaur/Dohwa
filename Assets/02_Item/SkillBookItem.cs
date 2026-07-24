using UnityEngine;

public class SkillBookItem : Item
{
    public SkillBookBase skillBook;
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {

            if (InventoryManager.Instance.IsInventoryAvailable(InventoryManager.InventoryType.SkillBook))
            {
                InventoryManager.Instance.AddItem(skillBook);
            }
        }
    }
}
