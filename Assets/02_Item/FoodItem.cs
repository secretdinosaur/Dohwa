using UnityEngine;

public class FoodItem : Item
{
    public Food food;
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (InventoryManager.Instance.IsInventoryAvailable(InventoryManager.InventoryType.Item))
            {
                InventoryManager.Instance.AddItem(food);
            }
        }
    }
}