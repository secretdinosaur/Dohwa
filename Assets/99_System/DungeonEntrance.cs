using UnityEngine;

public class DungeonEntrance : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if(other.CompareTag("Player"))
        {
            DungeonManager.Instance.TryEnterNextStage();
        }
    }
}
