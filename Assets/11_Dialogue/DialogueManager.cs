using DG.Tweening;
using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Localization;
using UnityEngine.UI;

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance;

    public int CurrentStage => DungeonManager.Instance.CurrentStage;
    public int DialogueIndex = 0;

    private string[] TableNames =
    {
        "Tutorial",
        "Ch1",
        "Ch2",
        "Ch3",
        "Ch4",
        "Ch5",
        "Ch6",
    };

    public GameObject DialoguePanel;
    public DialogueSet CurrentDialogueSet;
    public TextMeshProUGUI DIalogueArea;
    public GameObject Buttons;
    public GameObject EndImage;

    private bool _isTyping = false;
    private bool _isChoiceSelected = false;
    public bool IsDialogueActive = false;

    public AudioClip typingSound;
    public float RandomSoundValue;

    // ★ DOTween 겹침 방지용 시퀀스 제어 변수
    private Sequence fadeSequence;

    // ★ 입력 연타 방지 쿨다운 변수
    private float lastInputTime = 0f;
    private float inputCooldown = 0.1f;

    private void Awake()
    {
        Instance = this;
    }

    // 안전한 입력을 보장하는 프로퍼티
    private bool IsInputPressedThisFrame
    {
        get
        {
            if (Time.unscaledTime - lastInputTime < inputCooldown) return false;

            bool pressed = (Keyboard.current?.fKey.wasPressedThisFrame == true ||
                            Mouse.current?.leftButton.wasPressedThisFrame == true);

            if (pressed)
            {
                lastInputTime = Time.unscaledTime;
                inputCooldown = 0.1f; // 이후 넘기기 연타는 0.1초 쿨다운
                return true;
            }
            return false;
        }
    }

    public void ShowMainDialogue()
    {
        DialogueSet DialogueSet = LoadNextMainDialogueSet();
        if (DialogueSet != null)
        {
            StartShowDialogue(DialogueSet);
        }
    }

    //None Npc(for item)
    public void StartShowDialogue(DialogueSet DialogueSet)
    {
        if (IsDialogueActive || _currentDialogueCoroutine != null) return;

        // ★ 대화 시작 시 0.2초간 무조건 입력 무시 (상호작용 연타 방지)
        lastInputTime = Time.unscaledTime;
        inputCooldown = 0.2f;

        _currentDialogueCoroutine = StartCoroutine(ShowDialogue(DialogueSet));
    }


    public void StartShowDialogue(DialogueSet DialogueSet, NPC npc = null)
    {
        if (IsDialogueActive || _currentDialogueCoroutine != null) return;

        // ★ 대화 시작 시 0.2초간 무조건 입력 무시 (상호작용 연타 방지)
        lastInputTime = Time.unscaledTime;
        inputCooldown = 0.2f;

        _currentDialogueCoroutine = StartCoroutine(ShowDialogue(DialogueSet, npc));
    }

    public void StopShowDialogue()
    {
        IsDialogueActive = false;
        Buttons.SetActive(false);

        // ★ DOTween 시퀀스 확실한 종료
        if (fadeSequence != null) fadeSequence.Kill();

        fadeSequence = DOTween.Sequence();
        fadeSequence.Append(DialoguePanel.GetComponent<Image>().DOFade(0f, 0.25f));
        fadeSequence.Join(DIalogueArea.DOFade(0f, 0.25f));
        fadeSequence.OnComplete(() => {
            DialoguePanel.SetActive(false);
        });

        if (_currentDialogueCoroutine != null)
        {
            StopCoroutine(_currentDialogueCoroutine);
            _currentDialogueCoroutine = null;
        }

        CurrentDialogueSet = null;
        EndImage.SetActive(false);
        UIManager.Instance.PreventCameraMoveAndClick(false);

        Player.Instance.isInteracting = false;
    }

    private int captureIndex = -1;

    public IEnumerator ShowDialogue(DialogueSet DialogueSet, NPC npc = null)
    {
        Player.Instance.isInteracting = true;
        UIManager.Instance.PreventCameraMoveAndClick(true);
        CurrentDialogueSet = DialogueSet;
        IsDialogueActive = true;

        DialoguePanel.SetActive(true);
        if (CurrentDialogueSet.name.Contains("Teacher")) SoundManager.Instance.PlayTeacherSound();

        // ★ 열릴 때 기존 닫기 애니메이션 파괴
        if (fadeSequence != null) fadeSequence.Kill();
        fadeSequence = DOTween.Sequence();
        fadeSequence.Append(DialoguePanel.GetComponent<Image>().DOFade(1f, 0.25f));
        fadeSequence.Join(DIalogueArea.DOFade(1f, 0.25f));

        if (CurrentDialogueSet == null)
        {
            if (fadeSequence != null) fadeSequence.Kill();
            fadeSequence = DOTween.Sequence();
            fadeSequence.Append(DialoguePanel.GetComponent<Image>().DOFade(0f, 0.25f));
            fadeSequence.Join(DIalogueArea.DOFade(0f, 0.25f));
            fadeSequence.OnComplete(() => {
                DialoguePanel.SetActive(false);
                IsDialogueActive = false;
            });

            yield return null;
            EndImage.SetActive(false);
            UIManager.Instance.PreventCameraMoveAndClick(false);
            Player.Instance.isInteracting = false;
            yield break;
        }

        bool needExecution = DialogueSet.needExecutionFunction;
        foreach (LocalizedString str in DialogueSet.Script)
        {
            string fullText = str.GetLocalizedString();

            _isTyping = true;
            SetFontSize(DialogueSet);
            Coroutine typingCoroutine = StartCoroutine(TypeText(fullText, 0.05f));

            while (_isTyping)
            {
                bool tutorialActive = TutorialManager.instance != null && TutorialManager.instance.TutorialPanel.activeSelf;

                if (!tutorialActive && IsInputPressedThisFrame)
                {
                    StopCoroutine(typingCoroutine);
                    DIalogueArea.text = fullText;
                    _isTyping = false;
                    break;
                }
                yield return null;
            }

            yield return null;
            EndImage.SetActive(true);

            while (true)
            {
                bool tutorialActive = TutorialManager.instance != null && TutorialManager.instance.TutorialPanel.activeSelf;

                if (!tutorialActive && IsInputPressedThisFrame)
                {
                    break;
                }
                yield return null;
            }

            EndImage.SetActive(false);
        }

        if (DialogueSet.isChoiceNode)
        {
            _isChoiceSelected = false;
            Button[] buttons = Buttons.GetComponentsInChildren<Button>(true);
            Buttons.SetActive(true);
            foreach (Button i in buttons) i.gameObject.SetActive(false);

            for (int i = 0; i < DialogueSet.choiceOptions.Length; i++)
            {
                int ClickIndex = i;
                if (DialogueSet.choiceOptions[ClickIndex].needCheckCondition)
                    if (!DialogueSet.choiceOptions[ClickIndex].CheckConditions()) continue;

                buttons[i].gameObject.SetActive(true);
                buttons[i].GetComponentInChildren<TextMeshProUGUI>().text = DialogueSet.choiceOptions[i].Text.GetLocalizedString();
                buttons[i].onClick.RemoveAllListeners();

                if (DialogueSet.choiceOptions[ClickIndex].isQuest)
                    if (!DialogueSet.choiceOptions[ClickIndex].IsQuestClear())
                        buttons[i].GetComponentInChildren<TextMeshProUGUI>().color = new Color32(133, 133, 133, 255);

                buttons[i].onClick.AddListener(() => {
                    if (DialogueSet.choiceOptions[ClickIndex].isQuest)
                        if (!DialogueSet.choiceOptions[ClickIndex].IsQuestClear()) return;

                    OnChoice(ClickIndex, npc);
                    captureIndex = ClickIndex;
                });
            }

            RectTransform rectTransform = Buttons.GetComponent<RectTransform>();
            if (DialogueSet.choiceOptions.Length == 1) rectTransform.anchoredPosition = new Vector2(-100, 300);
            else if (DialogueSet.choiceOptions.Length == 2) rectTransform.anchoredPosition = new Vector2(-20, 330);
            else if (DialogueSet.choiceOptions.Length == 3) rectTransform.anchoredPosition = new Vector2(50, 400);
            else if (DialogueSet.choiceOptions.Length == 4) rectTransform.anchoredPosition = new Vector2(165, 500);

            rectTransform.sizeDelta = new Vector2(200 * DialogueSet.choiceOptions.Length, 100);

            yield return new WaitUntil(() => _isChoiceSelected);
            Buttons.SetActive(false);
        }

        CurrentDialogueSet.GetImmadiately();
        if (needExecution)
        {
            npc.ExecutionFunctionSet[CurrentDialogueSet.ExecutionFunctionIndex].Invoke();
        }
        StopShowDialogue();
    }

    public void ShowAutoShowDialogue(DialogueSet DialogueSet)
    {
        if (IsDialogueActive || _currentDialogueCoroutine != null) return;

        lastInputTime = Time.unscaledTime;
        inputCooldown = 0.2f;
        _currentDialogueCoroutine = StartCoroutine(AutoShowDialogue(DialogueSet));
    }

    public IEnumerator AutoShowDialogue(DialogueSet DialogueSet)
    {
        Player.Instance.isInteracting = true;
        CurrentDialogueSet = DialogueSet;
        IsDialogueActive = true;
        DialoguePanel.SetActive(true);

        if (fadeSequence != null) fadeSequence.Kill();
        fadeSequence = DOTween.Sequence();
        fadeSequence.Append(DialoguePanel.GetComponent<Image>().DOFade(1f, 0.25f));
        fadeSequence.Join(DIalogueArea.DOFade(1f, 0.25f));

        foreach (LocalizedString str in DialogueSet.Script)
        {
            string fullText = str.GetLocalizedString();
            SetFontSize(DialogueSet);
            _isTyping = true;
            Coroutine typingCoroutine = StartCoroutine(TypeText(fullText, 0.05f));

            while (_isTyping)
            {
                bool tutorialActive = TutorialManager.instance != null && TutorialManager.instance.TutorialPanel.activeSelf;

                if (!tutorialActive && IsInputPressedThisFrame)
                {
                    StopCoroutine(typingCoroutine);
                    DIalogueArea.text = fullText;
                    _isTyping = false;
                    break;
                }
                yield return null;
            }
            EndImage.SetActive(true);
            yield return null;

            float dialogueTimer = 0f;
            while (dialogueTimer < 3f)
            {
                bool tutorialActive = TutorialManager.instance != null && TutorialManager.instance.TutorialPanel.activeSelf;

                if (!tutorialActive)
                {
                    dialogueTimer += Time.deltaTime;
                    if (IsInputPressedThisFrame) break;
                }
                yield return null;
            }

            yield return null;
            EndImage.SetActive(false);
        }

        if (!DialogueSet.isChoiceNode || CurrentDialogueSet.choiceOptions == null || CurrentDialogueSet.choiceOptions.Length == 0)
        {
            StopShowDialogue();
            yield return null;
        }
    }

    public float typePitch;
    private IEnumerator TypeText(string text, float delay)
    {
        DIalogueArea.text = "";
        foreach (char c in text)
        {
            DIalogueArea.text += c;
            yield return new WaitForSecondsRealtime(delay);

            if (c != ' ' && typingSound != null && UnityEngine.Random.value < RandomSoundValue)
            {
                SoundManager.Instance.PlaySound(typingSound, UnityEngine.Random.Range(typePitch - 0.1f, typePitch));
            }
        }
        _isTyping = false;
    }

    public Coroutine _currentDialogueCoroutine;

    public void OnChoice(int index, NPC npc = null)
    {
        if (CurrentDialogueSet.isChoiceNode)
        {
            CurrentDialogueSet.GetImmadiately();
            if (CurrentDialogueSet.needExecutionFunction && npc != null)
            {
                if (npc.ExecutionFunctionSet != null && npc.ExecutionFunctionSet.Count > index)
                {
                    npc.ExecutionFunctionSet[index]?.Invoke();
                }
            }

            DialogueSet nextDialogue;
            if (!CurrentDialogueSet.choiceOptions[index].isQuest)
            {
                if (CurrentDialogueSet.choiceOptions[index].needCheckConditionForChooseNextDialogue)
                {
                    if (CurrentDialogueSet.choiceOptions[index].NextDialogueSet.Count > 1)
                        nextDialogue = CurrentDialogueSet.choiceOptions[index].GetValidNextDialogue();
                    else
                        nextDialogue = CurrentDialogueSet.choiceOptions[index].NextDialogueSet[0].dialogueSet;
                }
                else
                {
                    if (CurrentDialogueSet.choiceOptions[index].NextDialogueSet.Count > 1)
                    {
                        nextDialogue = CurrentDialogueSet.choiceOptions[index].NextDialogueSet[UnityEngine.Random.Range(0, CurrentDialogueSet.choiceOptions[index].NextDialogueSet.Count)].dialogueSet;
                    }
                    else
                        nextDialogue = CurrentDialogueSet.choiceOptions[index].NextDialogueSet[0].dialogueSet;
                }
            }
            else
                nextDialogue = CurrentDialogueSet.choiceOptions[index].questOption.NextDialogueSetIfQuestClear;

            if (_currentDialogueCoroutine != null) StopCoroutine(_currentDialogueCoroutine);

            Buttons.SetActive(false);

            if (CurrentDialogueSet.choiceOptions[index].isQuest)
            {
                AfterQuestClear(index);
                if (CurrentDialogueSet.choiceOptions[index].questOption.QuestReward)
                    CurrentDialogueSet.choiceOptions[index].GiveQuestReward();
                DataManager.Instance.AllSave();
            }

            if (nextDialogue != null)
            {
                lastInputTime = Time.unscaledTime;
                inputCooldown = 0.1f;
                _currentDialogueCoroutine = StartCoroutine(ShowDialogue(nextDialogue, npc));
            }
            else
                StopShowDialogue();
        }
    }

    public void AfterQuestClear(int index)
    {
        switch (CurrentDialogueSet.choiceOptions[index].questOption.questType)
        {
            case QuestType.None: break;
            case QuestType.HasItem:
                {
                    var questOpt = CurrentDialogueSet.choiceOptions[index].questOption;
                    if (questOpt == null || InventoryManager.Instance == null || questOpt.requireItem == null) break;

                    foreach (var req in questOpt.requireItem)
                    {
                        if (req == null || req.requireItem == null || !req.isDeleteItem) continue;

                        InventoryManager.InventoryType invType = default;
                        string id = null;

                        if (req.requireItem.Weapon != null && !string.IsNullOrEmpty(req.requireItem.Weapon.Id))
                        {
                            invType = InventoryManager.InventoryType.Weapon; id = req.requireItem.Weapon.Id;
                        }
                        else if (req.requireItem.Armor != null && !string.IsNullOrEmpty(req.requireItem.Armor.Id))
                        {
                            invType = InventoryManager.InventoryType.Armor; id = req.requireItem.Armor.Id;
                        }
                        else if (req.requireItem.SkillBook != null && !string.IsNullOrEmpty(req.requireItem.SkillBook.Id))
                        {
                            invType = InventoryManager.InventoryType.SkillBook; id = req.requireItem.SkillBook.Id;
                        }
                        else if (req.requireItem.Consumable != null && !string.IsNullOrEmpty(req.requireItem.Consumable.Id))
                        {
                            invType = InventoryManager.InventoryType.Item; id = req.requireItem.Consumable.Id;
                        }
                        else if (req.requireItem.NonConsumable != null && !string.IsNullOrEmpty(req.requireItem.NonConsumable.Id))
                        {
                            invType = InventoryManager.InventoryType.Item; id = req.requireItem.NonConsumable.Id;
                        }
                        else continue;

                        var slot = InventoryManager.Instance.FindItem(invType, id);
                        if (slot != null)
                        {
                            InventoryManager.Instance.RemoveItem(slot, req.requireItemCount);
                            InventoryManager.Instance.RemoveNullSlots(invType);
                            InventoryManager.Instance.UpdateSlots(invType);
                        }
                    }
                }
                InventoryManager.Instance.HideItemContextMenu();
                UIManager.Instance.SyncGameQuickSlots();
                break;
        }
    }

    public DialogueSet LoadNextMainDialogueSet()
    {
        if (CurrentDialogueSet != null && CurrentDialogueSet.NextMainDialogue != null)
        {
            if (CurrentDialogueSet.NextMainDialogue.name.Contains("Main"))
            {
                DialogueIndex = int.Parse(CurrentDialogueSet.NextMainDialogue.name.Split("_")[1]);
            }
            return CurrentDialogueSet.NextMainDialogue;
        }
        string resourcePath = $"Dialogues/{TableNames[CurrentStage]}/Main_{DialogueIndex}";
        DialogueSet nextSet = Resources.Load<DialogueSet>(resourcePath);

        if (nextSet != null)
        {
            DialogueIndex++;
            return nextSet;
        }
        else
        {
            Debug.LogWarning($"대화 뭉치를 찾을 수 없습니다: {resourcePath}");
            return null;
        }
    }

    public void SetFontSize(DialogueSet dialogueSet)
    {
        if (dialogueSet.Whisper) DIalogueArea.fontSize = 30;
        else if (dialogueSet.Shout) DIalogueArea.fontSize = 50;
        else DIalogueArea.fontSize = 40;
    }

    public void ResetDialogueIndex()
    {
        DialogueIndex = 0;
    }
}