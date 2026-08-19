using UnityEngine;

public class DialogueTrigger : MonoBehaviour
{
    public DialogueSet dialogueSet;
    private string eventSaveKey = "Teacher_Moutain_Dialogue_Watched";

    public void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (DataManager.Instance == null) return;
            if (!DataManager.Instance.IsEventCleared(eventSaveKey))
            {
                DialogueManager.Instance.ShowAutoShowDialogue(dialogueSet);
                DataManager.Instance.SetEventCleared(eventSaveKey);
            }
        }
    }
}
