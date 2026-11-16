using DG.Tweening;
using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.Localization;
using UnityEngine.UI;
using static TutorialManager;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    [Header("System")]
    public GameObject InteractPanel;
    public LocalizedString[] InteractType;
    public GameObject SignPanel;

    [Header("System - Stat")]
    public GameObject StatPanel;

    [Header("System - Item")]
    public GameObject GainPanel;
    public Image itemImage;
    public GameObject EndImage;
    public Coroutine GainRoutine;

    [Header("System - Message")]
    public GameObject MessagePanel;
    public Coroutine MessageRoutine;

    [Header("Inventory")]
    public GameObject Inventory;
    public Image ExpBarFill;
    public TextMeshProUGUI CurrentGradeText;
    public TextMeshProUGUI NextGradeText;
    public TextMeshProUGUI ExpText;
    public TextMeshProUGUI GradeAbilityText;
    public InventorySlot[] GameQuickSlots;
    public GameObject GameQuickSlot;
    public InventorySlot CurrentSlot;

    private Vector2[] indexPos;
    private bool canMoveQuickSlot = true;

    [Header("Player UI")]
    public GameObject BossStatus;
    public TextMeshProUGUI BossNameText;
    public LocalizedString BossName;

    public Image BossHpFill;

    public Image BossMpFill;

    public TextMeshProUGUI HpText;
    public TextMeshProUGUI MpText;
    public TextMeshProUGUI AttackText;
    public TextMeshProUGUI ArmorText;
    public TextMeshProUGUI ExternalArtText;
    public TextMeshProUGUI InnerArtText;
    public TextMeshProUGUI VitalStrikeRateText;
    public TextMeshProUGUI VitalStrikeDamageText;
    public TextMeshProUGUI HpRecoveryAmountText;
    public TextMeshProUGUI MpRecoveryAmountText;
    public TextMeshProUGUI MoveSpeedText;
    public TextMeshProUGUI PoisonResistanceText;
    public TextMeshProUGUI BleedResistanceText;
    public TextMeshProUGUI FireResistanceText;
    public TextMeshProUGUI LightningResistanceText;
    public TextMeshProUGUI OrthodoxText;
    public TextMeshProUGUI UnorthodoxText;
    public TextMeshProUGUI DemonicText;
    public TextMeshProUGUI MpConsumptionReductionText;
    public Image MpTipImage;

    public TextMeshProUGUI InsightPointText;

    [Header("Command UI")]
    public GameObject CommandPanel;
    public TextMeshProUGUI CurrentCommandsText;
    public TextMeshProUGUI AvailableCommandsText;

    [Header("Death UI")]
    public Sprite[] deathEffectSprites;
    public GameObject DeathPanel;

    public LocalizedString localizeStatNodeName;

    private void Awake()
    {
        Instance = this;
        if (CommandPanel != null) CommandPanel.SetActive(false);

        if (MpTipImage != null)
            MpTipImage.material = Instantiate(MpTipImage.material);

        if (ExpBarFill != null && ExpBarFill.material != null)
        {
            ExpBarFill.material = Instantiate(ExpBarFill.material);
        }
    }

    private void Start()
    {
        PreventCameraMoveAndClick(false);
        indexPos = new Vector2[GameQuickSlots.Length];
        CanvasGroup[] groups = GameQuickSlot.GetComponentsInChildren<CanvasGroup>();

        indexPos = groups
            .Select(x => x.GetComponent<RectTransform>().anchoredPosition)
            .ToArray();
        CurrentSlot = GameQuickSlots[0];
    }

    public void UpdateMpUI(float value)
    {
        if (MpTipImage != null)
            MpTipImage.material.SetFloat("_FillAmount", value);
    }

    public void UpdateBossHpUI(float value)
    {
        BossHpFill.material.SetFloat("_FillAmount", value);
    }

    public void UpdateBossMpUI(float value)
    {
        BossMpFill.material.SetFloat("_FillAmount", value);
    }

    public void UpdateBossNameUI()
    {
        if (BossNameText == null)
            return;

        if (BossName == null)
        {
            BossNameText.text = "Null boss name";
            return;
        }

        if (BossName.IsEmpty)
        {
            BossNameText.text = "Empty boss name";
            return;
        }

        BossNameText.text = BossName.GetLocalizedString();
    }

    public void PreventCameraMoveAndClick(bool status)
    {
        if (status)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Player.Instance.PreventAttack = true;
            Player.Instance.PreventMove = true;
            Camera.main.GetComponent<CameraController>().SetCameraInput(false);
        }
        else
        {
            Player.Instance.PreventAttack = false;
            Player.Instance.PreventMove = false;
            Camera.main.GetComponent<CameraController>().SetCameraInput(true);

            StartCoroutine(DelayCursorLock());
        }
    }

    private IEnumerator DelayCursorLock()
    {
        yield return null;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    float HPtemp = 1f;

    // =======================================================
    // UI 토글 (인벤토리)
    // =======================================================
    public void OpenInventory(InputAction.CallbackContext context)
    {
        if (!context.started) return;
        ToggleInventory();
    }

    public void ToggleInventory()
    {
        if (FadePanel.isFadePanelOn || TutorialManager.instance.isOngoingTutorial || GradeManager.Instance.IsPlayingUpgradeAnim) return;

        if (InventoryManager.Instance.isInventoryOpen)
        {
            CloseUI(); // 이미 열려있다면 닫기
        }
        else if (Player.Instance.CanAttack())
        {
            OpenUI(true); // 닫혀있다면 인벤토리 탭으로 열기
        }
    }

    // =======================================================
    // UI 토글 (세팅)
    // =======================================================
    public void OpenSetting(InputAction.CallbackContext context)
    {
        if (!context.started) return;
        ToggleSetting();
    }

    public void ToggleSetting()
    {
        if (FadePanel.isFadePanelOn || TutorialManager.instance.isOngoingTutorial || GradeManager.Instance.IsPlayingUpgradeAnim) return;

        if (InventoryManager.Instance.isInventoryOpen)
        {
            CloseUI(); // 이미 열려있다면 닫기
        }
        else if (Player.Instance.CanAttack())
        {
            OpenUI(false); // 닫혀있다면 세팅 탭으로 열기
        }
    }

    // =======================================================
    // UI 상태 통합 제어 메서드
    // =======================================================
    private void OpenUI(bool isInventoryTab)
    {
        InventoryManager.Instance.isInventoryOpen = true;
        Time.timeScale = 0f;

        InventoryManager.Instance.uiMap.SetActive(true);
        PreventCameraMoveAndClick(true); // 마우스 커서 표시 및 시점 고정
        GameQuickSlot.SetActive(false);

        InkBrushEffectManager.Instance.ClearHitEffects();

        // HP 이펙트 임시 저장 후 가리기
        HPtemp = Player.Instance.LowHPMat.GetFloat("_HP");
        Player.Instance.LowHPMat.SetFloat("_HP", 1f);

        if (isInventoryTab)
        {
            InventoryManager.Instance.OnClickInfoTab();
            InventoryManager.Instance.CheckAllSkillBooksAutoUnlocksFromSlots();
            TutorialManager.instance.CheckTutorial(TutorialType.Inventory);
        }
        else
        {
            InventoryManager.Instance.ResetTabState();
            InventoryManager.Instance.OnClickSettingTab();
        }

        Inventory.SetActive(true);
        SoundManager.Instance.PlayUISound(2); // 열기 사운드
    }

    private void CloseUI()
    {
        InventoryManager.Instance.isInventoryOpen = false;
        Time.timeScale = 1f;

        InventoryManager.Instance.uiMap.SetActive(false);
        Inventory.SetActive(false);
        GameQuickSlot.SetActive(true);

        PreventCameraMoveAndClick(false); // 마우스 커서 숨김 및 시점 복구

        SoundManager.Instance.PlayUISound(3); // 닫기 사운드

        // HP 이펙트 복구 및 스킬북 처리
        Player.Instance.LowHPMat.SetFloat("_HP", HPtemp);
        InventoryManager.Instance.CheckAllSkillBooksAutoUnlocksFromSlots();
    }

    public void UpdateCommandUI(string currentCommands, string[] availableCommands)
    {
        if (CommandPanel != null) CommandPanel.SetActive(true);
        if (CurrentCommandsText != null)
            CurrentCommandsText.text = currentCommands;
        if (AvailableCommandsText != null)
        {
            if (availableCommands != null && availableCommands.Length > 0)
                AvailableCommandsText.text = string.Join(", ", availableCommands);
            else
                AvailableCommandsText.text = " ";
        }
    }

    public void ShowInteractUI(int Type)
    {
        if (InteractPanel != null)
        {
            InteractPanel.SetActive(true);
            TextMeshProUGUI text = InteractPanel.GetComponentInChildren<TextMeshProUGUI>();
            string interactKey = TextTagParser.Parse("{Action:Interact}");
            string finalizedText;
            if (text != null)
            {
                switch (Type)
                {
                    case 0:
                        finalizedText = TextTagParser.Parse(InteractType[1].GetLocalizedString());
                        break;
                    case 1:
                        finalizedText = TextTagParser.Parse(InteractType[2].GetLocalizedString());
                        break;
                    case 2:
                        finalizedText = TextTagParser.Parse(InteractType[3].GetLocalizedString());
                        break;
                    case 3:
                        finalizedText = TextTagParser.Parse(InteractType[4].GetLocalizedString());
                        break;
                    default:
                        finalizedText = TextTagParser.Parse(InteractType[0].GetLocalizedString());
                        break;
                }
                text.text = interactKey + " : " + finalizedText;
            }
        }
    }

    public IEnumerator ShowSign(string txt, UnityEvent ExecutionFunctionSet)
    {
        Player.Instance.isInteracting = true;
        PreventCameraMoveAndClick(true);
        Time.timeScale = 0;
        SignPanel.SetActive(true);

        SignPanel.GetComponent<CanvasGroup>().alpha = 0f;
        SignPanel.GetComponent<CanvasGroup>().DOKill();
        Sequence OpenfadeSequence = DOTween.Sequence();
        OpenfadeSequence.SetUpdate(true);
        OpenfadeSequence.Append(SignPanel.GetComponent<CanvasGroup>().DOFade(1f, 0.15f));

        TextMeshProUGUI text = SignPanel.GetComponentInChildren<TextMeshProUGUI>();
        if (text != null && txt != null)
        {
            text.text = txt;
        }
        yield return null;
        yield return new WaitUntil(() =>
                (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame) ||
                (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
                (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame));
        Player.Instance.isInteracting = false;
        PreventCameraMoveAndClick(false);
        SignPanel.GetComponent<CanvasGroup>().DOKill();

        Sequence ClosefadeSequence = DOTween.Sequence();
        ClosefadeSequence.SetUpdate(true);
        ClosefadeSequence.Append(SignPanel.GetComponent<CanvasGroup>().DOFade(0f, 0.15f));
        ClosefadeSequence.OnComplete(() => {
            SignPanel.SetActive(false);
            SignPanel.GetComponent<CanvasGroup>().alpha = 1f;
        });
        Time.timeScale = 1;
        ExecutionFunctionSet.Invoke();
        yield return null;
    }

    public void SyncGameQuickSlots()
    {
        if (InventoryManager.Instance == null) return;

        for (int i = 0; i < GameQuickSlots.Length; i++)
        {
            QuickSlot gameQuickSlot = GameQuickSlots[i].GetComponent<QuickSlot>();
            gameQuickSlot.SetSlot();
        }
    }

    public void OnMoveQuickSlotNext(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            MoveQuickSlot(false);
        }
    }

    public void OnMoveQuickSlotPrev(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            MoveQuickSlot(true);
        }
    }

    public void MoveQuickSlot(bool next)
    {
        if (!canMoveQuickSlot) return;
        canMoveQuickSlot = false;
        CanvasGroup[] groups = GameQuickSlot.GetComponentsInChildren<CanvasGroup>();
        SoundManager.Instance.PlayUISound(0);
        RectTransform[] quickSlots = groups
            .Where(x => x.GetComponent<InventoryItem>() == null)
            .Select(x => x.GetComponent<RectTransform>())
            .ToArray();

        float duration = 0.2f;
        Sequence seq = DOTween.Sequence();

        if (next)
        {
            seq.Join(quickSlots[0].DOAnchorPos(indexPos[1], duration));
            seq.Join(quickSlots[0].GetComponent<CanvasGroup>().DOFade(0.6f, duration));

            seq.Join(quickSlots[1].DOAnchorPos(indexPos[2], duration));
            seq.Join(quickSlots[1].GetComponent<CanvasGroup>().DOFade(1f, duration));

            seq.Join(quickSlots[2].DOAnchorPos(indexPos[3], duration));
            seq.Join(quickSlots[2].GetComponent<CanvasGroup>().DOFade(0f, duration));

            seq.Join(quickSlots[3].DOAnchorPos(indexPos[0], duration));
            seq.Join(quickSlots[3].GetComponent<CanvasGroup>().DOFade(0f, duration));

            seq.OnComplete(() =>
            {
                quickSlots[quickSlots.Length - 1].SetSiblingIndex(0);
                CurrentSlot = quickSlots[1].GetComponent<InventorySlot>();
                canMoveQuickSlot = true;
            });
        }
        else
        {
            seq.Join(quickSlots[0].DOAnchorPos(indexPos[3], duration));
            seq.Join(quickSlots[0].GetComponent<CanvasGroup>().DOFade(0f, duration));

            seq.Join(quickSlots[1].DOAnchorPos(indexPos[0], duration));
            seq.Join(quickSlots[1].GetComponent<CanvasGroup>().DOFade(0f, duration));

            seq.Join(quickSlots[2].DOAnchorPos(indexPos[1], duration));
            seq.Join(quickSlots[2].GetComponent<CanvasGroup>().DOFade(0.6f, duration));

            seq.Join(quickSlots[3].DOAnchorPos(indexPos[2], duration));
            seq.Join(quickSlots[3].GetComponent<CanvasGroup>().DOFade(1f, duration));

            seq.OnComplete(() =>
            {
                quickSlots[0].SetSiblingIndex(quickSlots.Length - 1);
                CurrentSlot = quickSlots[3].GetComponent<InventorySlot>();
                canMoveQuickSlot = true;
            });
        }
    }

    public void StartGainRoutine(string[] texts, Sprite[] images, UnityEvent ExecutionFunctionSet)
    {
        GainRoutine = StartCoroutine(ShowGainPanel(texts, images, ExecutionFunctionSet));
    }

    public void StartGainRoutine(string text, Sprite image, UnityEvent ExecutionFunctionSet)
    {
        StartGainRoutine(
            new string[] { text },
            new Sprite[] { image },
            ExecutionFunctionSet
        );
    }

    public IEnumerator ShowGainPanel(string[] texts, Sprite[] images, UnityEvent ExecutionFunctionSet)
    {
        Player.Instance.isInteracting = true;
        PreventCameraMoveAndClick(true);
        GainPanel.SetActive(true);

        GainPanel.GetComponent<CanvasGroup>().alpha = 0f;
        GainPanel.GetComponent<CanvasGroup>().DOKill();
        Sequence OpenfadeSequence = DOTween.Sequence();
        OpenfadeSequence.SetUpdate(true);
        OpenfadeSequence.Append(GainPanel.GetComponent<CanvasGroup>().DOFade(1f, 0.15f));

        TextMeshProUGUI text = GainPanel.GetComponentInChildren<TextMeshProUGUI>();
        int count = 0;
        while (count < texts.Length)
        {
            SoundManager.Instance.PlayUISound(1);
            if (images[count] != null)
                itemImage.sprite = images[count];
            if (text != null && texts != null)
            {
                text.text = texts[count];
            }
            yield return null;
            yield return new WaitUntil(() => !TutorialManager.instance.TutorialPanel.activeSelf &&
            (
                (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame) ||
                (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
                (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            ));
            count++;
        }
        Player.Instance.isInteracting = false;
        PreventCameraMoveAndClick(false);

        GainPanel.GetComponent<CanvasGroup>().DOKill();

        Sequence ClosefadeSequence = DOTween.Sequence();
        ClosefadeSequence.SetUpdate(true);
        ClosefadeSequence.Append(GainPanel.GetComponent<CanvasGroup>().DOFade(0f, 0.15f));
        ClosefadeSequence.OnComplete(() => {
            GainPanel.SetActive(false);
            GainPanel.GetComponent<CanvasGroup>().alpha = 1f;
        });
        yield return null;
        ExecutionFunctionSet?.Invoke();
    }

    public IEnumerator ShowStatPanel(int statPoints)
    {
        StatPanel.SetActive(true);

        TextMeshProUGUI text = StatPanel.GetComponentInChildren<TextMeshProUGUI>();
        if (text != null)
        {
            text.text = "+ " + statPoints.ToString();
        }
        SoundManager.Instance.PlayUISound(1);
        StatPanel.GetComponent<CanvasGroup>().alpha = 0f;
        StatPanel.GetComponent<CanvasGroup>().DOKill();
        Sequence OpenfadeSequence = DOTween.Sequence();
        OpenfadeSequence.SetUpdate(true);
        OpenfadeSequence.Append(StatPanel.GetComponent<CanvasGroup>().DOFade(1f, 0.15f));

        yield return new WaitForSeconds(3f);

        StatPanel.GetComponent<CanvasGroup>().DOKill();

        Sequence ClosefadeSequence = DOTween.Sequence();
        ClosefadeSequence.SetUpdate(true);
        ClosefadeSequence.Append(StatPanel.GetComponent<CanvasGroup>().DOFade(0f, 0.15f));
        ClosefadeSequence.OnComplete(() => {
            StatPanel.SetActive(false);
            StatPanel.GetComponent<CanvasGroup>().alpha = 1f;
        });
        yield return null;
    }

    public void StartMessageRoutine(string text)
    {
        MessageRoutine = StartCoroutine(ShowMessage(text));
    }

    public IEnumerator ShowMessage(string txt)
    {
        MessagePanel.SetActive(true);

        MessagePanel.GetComponent<CanvasGroup>().alpha = 0f;
        MessagePanel.GetComponent<CanvasGroup>().DOKill();
        Sequence OpenfadeSequence = DOTween.Sequence();
        OpenfadeSequence.SetUpdate(true);
        OpenfadeSequence.Append(MessagePanel.GetComponent<CanvasGroup>().DOFade(1f, 0.15f));

        TextMeshProUGUI text = MessagePanel.GetComponentInChildren<TextMeshProUGUI>();
        if (text != null && txt != null)
        {
            text.text = txt;
        }
        yield return null;
        yield return new WaitUntil(() =>
                (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame) ||
                (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
                (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame));
        MessagePanel.GetComponent<CanvasGroup>().DOKill();

        Sequence ClosefadeSequence = DOTween.Sequence();
        ClosefadeSequence.SetUpdate(true);
        ClosefadeSequence.Append(MessagePanel.GetComponent<CanvasGroup>().DOFade(0f, 0.15f));
        ClosefadeSequence.OnComplete(() => {
            MessagePanel.SetActive(false);
            MessagePanel.GetComponent<CanvasGroup>().alpha = 1f;
        });
        yield return null;
    }

    public void HideCommandUI()
    {
        if (CommandPanel != null) CommandPanel.SetActive(false);
        if (CurrentCommandsText != null) CurrentCommandsText.text = "";
        if (AvailableCommandsText != null) AvailableCommandsText.text = "";
    }

    private void OnDestroy()
    {
        if (MpTipImage != null && MpTipImage.material != null)
            Destroy(MpTipImage.material);
    }
}