using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class NPC : InteractableObj
{
    public override int Type => 0;

    protected int TalkCount = 0;

    public DialogueSet[] dialogueSet;
    public Coroutine Talk;
    private Coroutine LookCoroutine;
    public AudioClip TalkSound;

    [Header("Dialogue")]
    public List<UnityEvent> ExecutionFunctionSet = new List<UnityEvent>();

    public override void Interact(bool ShowPanelImmediately = true, bool LookPlayer = true)
    {
        // DialogueManager가 이미 실행 중이면 중복 상호작용 완전 차단
        if (DialogueManager.Instance.IsDialogueActive) return;

        TutorialManager.instance.CheckTutorial(TutorialManager.TutorialType.Connections);

        base.Interact();

        if (LookPlayer)
        {
            if (LookCoroutine != null) StopCoroutine(LookCoroutine);
            LookCoroutine = StartCoroutine(LookRoutine());
        }

        int indexToPlay = TalkCount < dialogueSet.Length - 1 ? TalkCount : dialogueSet.Length - 1;

        // ★ NPC에서 코루틴을 돌리지 않고 DialogueManager에 완전히 위임합니다.
        DialogueManager.Instance.StartShowDialogue(dialogueSet[indexToPlay], this);

        TalkCount++;

        if (TalkSound != null)
        {
            SoundManager.Instance.PlaySound(TalkSound);
        }
    }

    private IEnumerator LookRoutine()
    {
        yield return null;

        while (DialogueManager.Instance.IsDialogueActive)
        {
            Vector3 direction = Player.Instance.transform.position - transform.position;
            direction.y = 0f;
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 5f);

            yield return null;
        }

        LookCoroutine = null;
    }
}