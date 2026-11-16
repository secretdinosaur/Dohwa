using UnityEngine;
using System.Collections;
using TMPro;
using UnityEngine.Localization.Components;

public class DialogueCommandSet : MonoBehaviour
{
    private DialogueSet pendingDialogueSet;

    public void PlayerDeadForPiece(DialogueSet dialogueSet)
    {
        DialogueManager.Instance.StopShowDialogue();
        Player.Instance.CancelCurrentAction();
        // 즉시 실행할 동작들
        GetDamage(999999999);
        if (Player.Instance.PlayerAttack != null) Player.Instance.PlayerAttack.DisableLockOn();
        UIManager.Instance.DeathPanel.GetComponentInChildren<TextMeshProUGUI>(true).gameObject.SetActive(false);
        Player.Instance.Animator_SetTrigger("isDead");
        InkBrushEffectManager.Instance.PlayDeathEffect();
        SettingManager.Instance.SetSensitivityZero();
        SoundManager.Instance.PlaySound(Player.Instance.DeathSound);

        // 지연 실행을 위해 데이터 보관 및 Invoke로 예약
        pendingDialogueSet = dialogueSet;
        // 5초 후 죽음 패널 표시
        Invoke(nameof(ShowDeathPanelForPiece), 5f);
        // 10초 후 리스폰 처리 (즉, 처음 호출 시점으로부터 10초)
        Invoke(nameof(RespawnForPieceInvoker), 10f);
    }

    // Invoke로 호출되는 헬퍼: 죽음 패널 표시
    void ShowDeathPanelForPiece() { UIManager.Instance.DeathPanel.SetActive(true); }

    // Invoke로 호출되는 헬퍼: RespawnForPiece 호출
    void RespawnForPieceInvoker() { RespawnForPiece(); }

    public void RespawnForPiece()
    {
        Player.Instance.Respawn(false);
        Invoke(nameof(StartShowDialogue), 0.25f);
    }
    public void StartShowDialogue()
    {
        DialogueManager.Instance.StartShowDialogue(pendingDialogueSet);
        UIManager.Instance.DeathPanel.GetComponentInChildren<TextMeshProUGUI>(true).gameObject.SetActive(true);
        pendingDialogueSet = null;
    }

    public void GetDamage(int damage)
    {
        if(Player.Instance != null)
            Player.Instance.TakeArbitraryDamage(damage);
    }

    public void ForceStartTutorial(string type)
    {
        if (TutorialManager.instance != null)
        {
            TutorialManager.instance.ForceStartTutorial(type);
        }
    }

    public void GetItem(string Id)
    {
        if(InventoryManager.Instance != null && DataManager.Instance != null)
        {
            SkillBookBase skillBook = DataManager.Instance.GetSkillBook(Id);
            if(skillBook != null)
            {
                InventoryManager.Instance.AddItem(skillBook);
                UIManager.Instance.StartGainRoutine(skillBook.skillBookName, skillBook.icon, null);
                return;
            }
            
            Weapon weapon = DataManager.Instance.GetWeapon(Id);
            if (weapon != null)
            {
                InventoryManager.Instance.AddItem(weapon);
                UIManager.Instance.StartGainRoutine(weapon.weaponName, weapon.icon, null);
            }

            Armor armor = DataManager.Instance.GetArmor(Id);
            if (armor != null)
            {
                InventoryManager.Instance.AddItem(armor);
                UIManager.Instance.StartGainRoutine(armor.armorName, armor.icon, null);
                return;
            }

            Consumable consumable = DataManager.Instance.GetConsumable(Id);
            if (consumable != null)
            {
                InventoryManager.Instance.AddItem(consumable);
                UIManager.Instance.StartGainRoutine(consumable.ConsumableName, consumable.icon, null);
                return;
            }

            NonConsumable nonConsumable = DataManager.Instance.GetNonConsumable(Id);
            if (nonConsumable != null)
            {
                InventoryManager.Instance.AddItem(nonConsumable);
                UIManager.Instance.StartGainRoutine(nonConsumable.NonConsumableName, nonConsumable.icon, null);
                return;
            }
        }
    }

    public void SetEventClear(string EventKey)
    {
        if (DataManager.Instance == null) return;
        DataManager.Instance.SetEventCleared(EventKey);
    }
}
