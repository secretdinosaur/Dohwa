using NUnit.Framework;
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

    [Header("Dialogue")]
    public List<UnityEvent> ExecutionFunctionSet = new List<UnityEvent>();

    public override void Interact(bool ShowPanelImmediately = true, bool LookPlayer = true)
    {
        base.Interact();
        print("Interacted with NPC: " + gameObject.name);

        // 바라보기 코루틴 시작
        if (LookPlayer)
        {
            if (LookCoroutine != null) StopCoroutine(LookCoroutine);
            LookCoroutine = StartCoroutine(LookRoutine());
        }

        if (TalkCount < dialogueSet.Length - 1)
        {
            Talk = StartCoroutine(DialogueManager.Instance.ShowDialogue(dialogueSet[TalkCount], this));
        }
        else
        {
            Talk = StartCoroutine(DialogueManager.Instance.ShowDialogue(dialogueSet[dialogueSet.Length - 1], this));
        }
        TalkCount++;
    }

    private IEnumerator LookRoutine()
    {
        // DialogueManager의 IsDialogueActive가 true로 바뀔 시간을 위해 한 프레임 대기
        yield return null;

        // 대화가 끝날 때까지 회전 업데이트 유지
        while (DialogueManager.Instance.IsDialogueActive)
        {
            Vector3 direction = Player.Instance.transform.position - transform.position;
            direction.y = 0f; // NPC가 위아래로 기울어지는 것 방지
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 5f);

            yield return null;
        }

        LookCoroutine = null;
    }
}