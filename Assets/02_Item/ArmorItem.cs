using UnityEngine;

public class ArmorItem : Item
{
    public Armor armor;
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (InventoryManager.Instance.IsInventoryAvailable(InventoryManager.InventoryType.Armor))
            {
                InventoryManager.Instance.AddItem(armor);
            }
        }
    }
}