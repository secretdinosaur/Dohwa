using UnityEngine;
using System.Collections.Generic;

public class ItemDropManager : MonoBehaviour
{
    public static ItemDropManager Instance;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        CheckAllItems();
    }
    public Item[] OneTimeItems;

    public void CheckAllItems()
    {
        Item[] pickups = FindObjectsByType<Item>(FindObjectsSortMode.None);

        foreach (var pickup in pickups)
        {
            int index = System.Array.IndexOf(OneTimeItems, pickup);
            if (index >= 0 && DataManager.Instance.IsItemObtained(index))
            {
                Destroy(pickup.gameObject);
            }
        }
    }

    // 아이템이 획득되었을 때 호출되는 메서드
    public void RegisterItemObtained(Item item)
    {
        int index = System.Array.IndexOf(OneTimeItems, item);
        if (index >= 0)
        {
            DataManager.Instance.MarkItemAsObtained(index);
        }
    }
}