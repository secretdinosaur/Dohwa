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
    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
    }

    public void ShowMainDialogue()
    {
        DialogueSet DialogueSet = LoadNextMainDialogueSet();
        if (DialogueSet != null)
        {
            if (_currentDialogueCoroutine != null) StopCoroutine(_currentDialogueCoroutine);
            _currentDialogueCoroutine = StartCoroutine(ShowDialogue(DialogueSet));
        }
    }

    public void StopShowDialogue()
    {
        IsDialogueActive = false;
        Buttons.SetActive(false);
        DialoguePanel.GetComponent<Image>().DOKill();
        DIalogueArea.DOKill();

        Sequence ClosefadeSequence = DOTween.Sequence();

        ClosefadeSequence.Append(DialoguePanel.GetComponent<Image>().DOFade(0f, 0.25f));
        ClosefadeSequence.Join(DIalogueArea.DOFade(0f, 0.25f));
        ClosefadeSequence.OnComplete(() => {
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

    public void StartShowDialogue(DialogueSet DialogueSet)
    {
        _currentDialogueCoroutine = StartCoroutine(ShowDialogue(DialogueSet));
    }
    private int captureIndex = -1;
    public IEnumerator ShowDialogue(DialogueSet DialogueSet, NPC npc = null)
    {
        Player.Instance.isInteracting = true;
        UIManager.Instance.PreventCameraMoveAndClick(true);
        CurrentDialogueSet = DialogueSet;
        Debug.Log($"ShowDialogue called with DialogueSet: {CurrentDialogueSet.name}");
        IsDialogueActive = true;
        DialoguePanel.SetActive(true);
        if (CurrentDialogueSet.name.Contains("Teacher")) SoundManager.Instance.PlayTeacherSound();
        DialoguePanel.GetComponent<Image>().DOKill();
        DIalogueArea.DOKill();
        Sequence OpenfadeSequence = DOTween.Sequence();
        OpenfadeSequence.Append(DialoguePanel.GetComponent<Image>().DOFade(1f, 0.25f));
        OpenfadeSequence.Join(DIalogueArea.DOFade(1f, 0.25f));

        if(CurrentDialogueSet == null)
        {
            DialoguePanel.GetComponent<Image>().DOKill();
            DIalogueArea.DOKill();

            Sequence ClosefadeSequence = DOTween.Sequence();

            ClosefadeSequence.Append(DialoguePanel.GetComponent<Image>().DOFade(0f, 0.25f));
            ClosefadeSequence.Join(DIalogueArea.DOFade(0f, 0.25f));
            ClosefadeSequence.OnComplete(() => {
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
                bool isSkipPressed = !TutorialManager.instance.TutorialPanel.activeSelf &&
                             (Keyboard.current?.fKey.wasPressedThisFrame == true ||
                              Mouse.current?.leftButton.wasPressedThisFrame == true);

                if (isSkipPressed)
                {
                    StopCoroutine(typingCoroutine);
                    DIalogueArea.text = fullText;
                    _isTyping = false;

                    yield return null;
                    break;
                }
                yield return null;
            }
            EndImage.SetActive(true);
            yield return null;
            yield return new WaitUntil(() => !TutorialManager.instance.TutorialPanel.activeSelf &&
            (
                (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame) ||
                (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            ));
            yield return null;
            EndImage.SetActive(false);
        }

        if (DialogueSet.isChoiceNode)
        {
            _isChoiceSelected = false;
            Button[] buttons = Buttons.GetComponentsInChildren<Button>(true);
            Buttons.SetActive(true);
            foreach (Button i in buttons)
            {
                i.gameObject.SetActive(false);
            }

            for (int i = 0; i < DialogueSet.choiceOptions.Length; i++)
            {
                int ClickIndex = i;
                if (DialogueSet.choiceOptions[ClickIndex].needCheckCondition)
                    if (!DialogueSet.choiceOptions[ClickIndex].CheckConditions())
                        continue;

                buttons[i].gameObject.SetActive(true);
                buttons[i].GetComponentInChildren<TextMeshProUGUI>().text = DialogueSet.choiceOptions[i].Text.GetLocalizedString();

                buttons[i].onClick.RemoveAllListeners();
                if (DialogueSet.choiceOptions[ClickIndex].isQuest)
                    if (!DialogueSet.choiceOptions[ClickIndex].IsQuestClear())
                        buttons[i].GetComponentInChildren<TextMeshProUGUI>().color = new Color32(133, 133, 133, 255);
                buttons[i].onClick.AddListener(() => {
                    bool isQuest = DialogueSet.choiceOptions[ClickIndex].isQuest;
                    bool isClear = DialogueSet.choiceOptions[ClickIndex].IsQuestClear();

                    Debug.Log($"버튼 {ClickIndex} 클릭됨 | 퀘스트 여부: {isQuest} | 클리어 여부: {isClear}");
                    if (DialogueSet.choiceOptions[ClickIndex].isQuest)
                        if (!DialogueSet.choiceOptions[ClickIndex].IsQuestClear())
                            return;
                    OnChoice(ClickIndex, npc);
                    captureIndex = ClickIndex;
                });
            }
            RectTransform rectTransform = Buttons.GetComponent<RectTransform>();
            if (DialogueSet.choiceOptions.Length == 1)
            {
                rectTransform.anchoredPosition = new Vector2(-100, 300);
            }
            else if(DialogueSet.choiceOptions.Length == 2)
            {
                rectTransform.anchoredPosition = new Vector2(-20, 330);
            }
            else if (DialogueSet.choiceOptions.Length == 3)
            {
                rectTransform.anchoredPosition = new Vector2(50, 400);
            }
            else if (DialogueSet.choiceOptions.Length == 4)
            {
                rectTransform.anchoredPosition = new Vector2(165, 500);
            }

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
        _currentDialogueCoroutine = StartCoroutine(AutoShowDialogue(DialogueSet));
    }

    public IEnumerator AutoShowDialogue(DialogueSet DialogueSet)
    {
        Player.Instance.isInteracting = true;
        CurrentDialogueSet = DialogueSet;
        IsDialogueActive = true;
        DialoguePanel.SetActive(true);

        DialoguePanel.GetComponent<Image>().DOKill();
        DIalogueArea.DOKill();
        Sequence OpenfadeSequence = DOTween.Sequence();
        OpenfadeSequence.Append(DialoguePanel.GetComponent<Image>().DOFade(1f, 0.25f));
        OpenfadeSequence.Join(DIalogueArea.DOFade(1f, 0.25f));

        foreach (LocalizedString str in DialogueSet.Script)
        {
            string fullText = str.GetLocalizedString();
            SetFontSize(DialogueSet);
            _isTyping = true;
            Coroutine typingCoroutine = StartCoroutine(TypeText(fullText, 0.05f));

            while (_isTyping)
            {
                bool isSkipPressed = !TutorialManager.instance.TutorialPanel.activeSelf &&
                             (Keyboard.current?.fKey.wasPressedThisFrame == true ||
                              Mouse.current?.leftButton.wasPressedThisFrame == true);

                if (isSkipPressed)
                {
                    StopCoroutine(typingCoroutine);
                    DIalogueArea.text = fullText;
                    _isTyping = false;

                    yield return null;
                    break;
                }
                yield return null;
            }
            EndImage.SetActive(true);
            yield return null;

            // ✨ 2. 타이핑 종료 후 다음 대화 대기 (튜토리얼 창이 꺼져있을 때만 흐르는 타이머)
            float dialogueTimer = 0f; // 누적 시간을 담을 변수

            while (dialogueTimer < 3f) // 3초가 채워질 때까지 반복
            {
                // 튜토리얼 창이 꺼져있을 때만 작동합니다.
                if (!TutorialManager.instance.TutorialPanel.activeSelf)
                {
                    // 시간이 실시간로 누적됩니다. (튜토리얼 창이 열리면 누적이 멈춤)
                    dialogueTimer += Time.deltaTime;

                    // 타이머가 흐르는 도중 플레이어가 넘기기 입력을 한 경우 즉시 대기 종료
                    if (Keyboard.current?.fKey.wasPressedThisFrame == true ||
                        Mouse.current?.leftButton.wasPressedThisFrame == true)
                    {
                        break;
                    }
                }

                yield return null; // 다음 프레임까지 대기
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
    [SerializeField]
    public Coroutine _currentDialogueCoroutine;

    public void OnChoice(int index, NPC npc = null)
    {
        if (CurrentDialogueSet.isChoiceNode)
        {
            CurrentDialogueSet.GetImmadiately();
            if (CurrentDialogueSet.needExecutionFunction && npc != null)
            {
                if (npc.ExecutionFunctionSet != null && npc.ExecutionFunctionSet.Count > CurrentDialogueSet.ExecutionFunctionIndex)
                {
                    npc.ExecutionFunctionSet[CurrentDialogueSet.ExecutionFunctionIndex]?.Invoke();
                }
            }

            DialogueSet nextDialogue;
            if (!CurrentDialogueSet.choiceOptions[index].isQuest)
            {
                if (CurrentDialogueSet.choiceOptions[index].needCheckConditionForChooseNextDialogue)
                {
                    if (CurrentDialogueSet.choiceOptions[index].NextDialogueSet.Count > 1)
                    {
                        nextDialogue = CurrentDialogueSet.choiceOptions[index].GetValidNextDialogue();
                    }
                    else
                    {
                        nextDialogue = CurrentDialogueSet.choiceOptions[index].NextDialogueSet[0].dialogueSet;
                    }
                }
                else
                {
                    if (CurrentDialogueSet.choiceOptions[index].NextDialogueSet.Count > 1)
                    {
                        nextDialogue =
                            CurrentDialogueSet.
                            choiceOptions[index].
                            NextDialogueSet[UnityEngine.Random.Range(0, CurrentDialogueSet.choiceOptions[index].NextDialogueSet.Count)].
                            dialogueSet;
                    }
                    else
                    {
                        nextDialogue = CurrentDialogueSet.choiceOptions[index].NextDialogueSet[0].dialogueSet;
                    }
                }
            }
            else
                nextDialogue = CurrentDialogueSet.choiceOptions[index].questOption.NextDialogueSetIfQuestClear;

            if (_currentDialogueCoroutine != null)
            {
                StopCoroutine(_currentDialogueCoroutine);
            }

            Buttons.SetActive(false);

            if (CurrentDialogueSet.choiceOptions[index].isQuest)
            {
                AfterQuestClear(index);
                if (CurrentDialogueSet.choiceOptions[index].questOption.QuestReward)
                    CurrentDialogueSet.choiceOptions[index].GiveQuestReward();
                DataManager.Instance.AllSave();
            }

            if (nextDialogue != null)
                _currentDialogueCoroutine = StartCoroutine(ShowDialogue(nextDialogue, npc));
            else
                StopShowDialogue();
        }
    }

    public void AfterQuestClear(int index)
    {
        switch (CurrentDialogueSet.choiceOptions[index].questOption.questType)
        {
            case QuestType.None:
                break;

            case QuestType.HasItem:
                {
                    var questOpt = CurrentDialogueSet.choiceOptions[index].questOption;
                    if (questOpt == null) break;
                    if (InventoryManager.Instance == null) break;
                    if (questOpt.requireItem == null || questOpt.requireItem.Length == 0) break;

                    foreach (var req in questOpt.requireItem)
                    {
                        if (req == null || req.requireItem == null) continue;
                        if (!req.isDeleteItem) continue;

                        InventoryManager.InventoryType invType = default;
                        string id = null;

                        if (req.requireItem.Weapon != null)
                        {
                            if (string.IsNullOrEmpty(req.requireItem.Weapon.Id)) continue;
                            invType = InventoryManager.InventoryType.Weapon;
                            id = req.requireItem.Weapon.Id;
                        }
                        else if (req.requireItem.Armor != null)
                        {
                            if (string.IsNullOrEmpty(req.requireItem.Armor.Id)) continue;
                            invType = InventoryManager.InventoryType.Armor;
                            id = req.requireItem.Armor.Id;
                        }
                        else if (req.requireItem.SkillBook != null)
                        {
                            if (string.IsNullOrEmpty(req.requireItem.SkillBook.Id)) continue;
                            invType = InventoryManager.InventoryType.SkillBook;
                            id = req.requireItem.SkillBook.Id;
                        }
                        else if (req.requireItem.Consumable != null)
                        {
                            if (string.IsNullOrEmpty(req.requireItem.Consumable.Id)) continue;
                            invType = InventoryManager.InventoryType.Item;
                            id = req.requireItem.Consumable.Id;
                        }
                        else if (req.requireItem.NonConsumable != null)
                        {
                            if (string.IsNullOrEmpty(req.requireItem.NonConsumable.Id)) continue;
                            invType = InventoryManager.InventoryType.Item;
                            id = req.requireItem.NonConsumable.Id;
                        }
                        else
                        {
                            continue;
                        }

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

            case QuestType.KillCount:
                break;

            case QuestType.MinimumGrade:
                break;

            case QuestType.MinimumGold:
                break;

            default:
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
        if (dialogueSet.Whisper)
        {
            DIalogueArea.fontSize = 30;
        }
        else if (dialogueSet.Shout)
        {
            DIalogueArea.fontSize = 50;
        }
        else
        {
            DIalogueArea.fontSize = 40;
        }
    }

    public void ResetDialogueIndex()
    {
        DialogueIndex = 0;
    }
}

