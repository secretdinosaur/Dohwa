using UnityEngine;

public class Kid : NPC
{
    private string questSaveKey = "Kid_Quest_Accepted";
    private string eventSaveKey = "Kid_Quest_Cleared";

    public override void Interact(bool ShowPanelImmediately = true, bool LookPlayer = true)
    {
        if (!isInteractable || DataManager.Instance == null) return;

        if (!DataManager.Instance.IsQuestAccepted(questSaveKey))
        {
            TalkCount = 0;
            base.Interact(true, false);
            return;
        }
        else
        {
            if (!DataManager.Instance.IsEventCleared(eventSaveKey))
            {
                TalkCount = 1;
                base.Interact(true, false);
                return;
            }
            else
            {
                TalkCount = 2;
                base.Interact(true, false);
                return;
            }
        }
    }
}
