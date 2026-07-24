using UnityEngine;

public class WeaponItem : Item
{
    public Weapon weapon;
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (InventoryManager.Instance.IsInventoryAvailable(InventoryManager.InventoryType.Weapon))
            {
                InventoryManager.Instance.AddItem(weapon);
            }
        }
    }
}