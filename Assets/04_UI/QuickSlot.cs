using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class QuickSlot : InventorySlot
{
    public InventorySlot OriginalSlot;
    public Image Cooldown;

    public static float globalQuickSlotDelay = 5f;
    public float nextAllowedUseTime = 0f;

    public void SetSlot()
    {
        OriginalSlot.item.Clear();
        if (item.consumable != null)
        {
            OriginalSlot.item.SetItem(item.consumable);
        }
        else if (item.nonConsumable)
        {
            OriginalSlot.item.SetItem(item.nonConsumable);
        }
        OriginalSlot.item.Count = item.Count;
    }
    Coroutine EatRoutine = null;
    public void OnClickQuickSlot()
    {
        if ((item == null || Time.time < nextAllowedUseTime || EatRoutine != null) ||
            (item.consumable == null && item.nonConsumable == null))
        {
            return;
        }
        EatRoutine = StartCoroutine(UseItemRoutine());
    }
    IEnumerator UseItemRoutine()
    {
        Player.Instance.Animator_SetTrigger("Eat");
        yield return new WaitForSeconds(1.16f);

        // 아이템 사용
        if (item.consumable != null)
            item.consumable.Use();
        else if (item.nonConsumable != null)
            item.nonConsumable.Use();
        item.Count--;
        OriginalSlot.item.Count--;

        nextAllowedUseTime = Time.time + globalQuickSlotDelay;
        if (Cooldown != null)
        {
            if (cooldownRoutine != null) StopCoroutine(cooldownRoutine);
            cooldownRoutine = StartCoroutine(ShowCooldown(nextAllowedUseTime));
        }

        EatRoutine = null;
        UIManager.Instance.SyncGameQuickSlots();
    }

    public IEnumerator ShowCooldown(float targetEndTime)
    {
        // Time.time이 targetEndTime에 도달할 때까지 루프
        while (Time.time < targetEndTime)
        {
            float remaining = targetEndTime - Time.time;

            // 전체 쿨타임(globalQuickSlotDelay) 대비 남은 시간 비율로 fillAmount 설정
            // 쿨타임이 5초인데 2.5초 남았다면 0.5가 됨
            if (Cooldown != null)
                Cooldown.fillAmount = Mathf.Clamp01(remaining / globalQuickSlotDelay);

            yield return null;
        }

        if (Cooldown != null) Cooldown.fillAmount = 0f;
        cooldownRoutine = null;
    }
    public void RestoreCooldown(float targetEndTime)
    {
        if (Time.time < targetEndTime && Cooldown != null)
        {
            StopAllCoroutines(); // 기존 쿨타임 루틴 정지
            StartCoroutine(ShowCooldown(targetEndTime));
        }
    }
    private Coroutine cooldownRoutine;
    private void OnDisable()
    {
        // 오브젝트가 비활성화될 때 실행 중인 쿨타임 코루틴 중단
        if (cooldownRoutine != null)
        {
            StopCoroutine(cooldownRoutine);
            cooldownRoutine = null;
        }
    }

    private void OnEnable()
    {
        // 다시 활성화될 때 쿨타임이 남아있다면 즉시 재시작
        if (Time.time < nextAllowedUseTime)
        {
            if (cooldownRoutine != null) StopCoroutine(cooldownRoutine);
            cooldownRoutine = StartCoroutine(ShowCooldown(nextAllowedUseTime));
        }
        else
        {
            if (Cooldown != null) Cooldown.fillAmount = 0f;
        }
    }
}