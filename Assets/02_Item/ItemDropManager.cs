using UnityEngine;

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

    public void CheckAllItems()
    {
        Item[] pickups = FindObjectsByType<Item>(FindObjectsSortMode.None);

        foreach (var pickup in pickups)
        {
            // 💡 고유 GUID를 통해 검증
            if (DataManager.Instance.IsItemObtained(pickup.UniqueID))
            {
                Destroy(pickup.gameObject);
            }
        }
    }

    public void RegisterItemObtained(Item item)
    {
        // 💡 고유 GUID를 세이브 데이터에 등록
        DataManager.Instance.MarkItemAsObtained(item.UniqueID);
    }
}