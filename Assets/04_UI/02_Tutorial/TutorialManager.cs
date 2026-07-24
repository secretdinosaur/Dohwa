using DG.Tweening;
using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class TutorialManager : MonoBehaviour
{
    public enum TutorialType
    {
        Inventory,
        SkillBook,
        Item,
        Fight,
        GameGoal,
        Move,
    }

    public static TutorialManager instance;

    [Header("TutorialSet")]
    public Tutorial[] Tutorials;

    [Header("TutorialUI")]
    public GameObject TutorialPanel;
    public Image TutorialImage;
    public TMP_Text TutorialText;
    public TMP_Text PageText;
    public TMP_Text TitleText;
    public bool isOngoingTutorial => TutorialPanel.gameObject.activeSelf;
    private Coroutine TutorialRoutine;
    private Tutorial currentTutorial;
    private int currentPage;

    private void Awake()
    {
        instance = this;
    }

    public void StartTutorial(string type)
    {
        if (Enum.TryParse<TutorialType>(type, out var tutorialType))
        {
            CheckTutorial(tutorialType);
        }
    }

    public void ForceStartTutorial(string type)
    {
        if (Enum.TryParse<TutorialType>(type, out var tutorialType))
        {
            TutorialRoutine = StartCoroutine(ShowTutorial(tutorialType.ToString()));
        }
    }

    public void SetTutorialSet(string tutorialName)
    {
        foreach (var tutorial in Tutorials)
        {
            if (tutorial.TutorialName == tutorialName)
                currentTutorial = tutorial;
        }
        TitleText.text = currentTutorial.tutorialTitle;
    }

    public int GetPageCount(string tutorialName)
    {
        foreach (var tutorial in Tutorials)
        {
            if (tutorial.TutorialName == tutorialName)
                return tutorial.Pages.Length;
        }
        return 0;
    }

    private void RefreshPage(int PageCount)
    {
        var page = currentTutorial.Pages[currentPage];

        if (page.Image != null)
        {
            TutorialImage.sprite = page.Image;
            TutorialImage.preserveAspect = true;
        }
        if (page.Text != null)
            TutorialText.text = TextTagParser.Parse(page.Text);
        PageText.text = (currentPage + 1) + " / " + PageCount;
    }

    public void CheckTutorial(TutorialType type)
    {
        // DataManager를 통해 튜토리얼 완료 여부 확인 및 저장
        if (!DataManager.Instance.IsTutorialCompleted(type))
        {
            TutorialRoutine = StartCoroutine(ShowTutorial(type.ToString()));
            DataManager.Instance.CompleteTutorial(type);
        }
    }

    public IEnumerator ShowTutorial(string TutorialName)
    {
        Player.Instance.isInteracting = true;
        UIManager.Instance.PreventCameraMoveAndClick(true);
        Time.timeScale = 0;
        TutorialPanel.SetActive(true);
        SetTutorialSet(TutorialName);
        int PageCount = GetPageCount(TutorialName);
        currentPage = 0;

        TutorialPanel.GetComponent<CanvasGroup>().alpha = 0f;
        TutorialPanel.GetComponent<CanvasGroup>().DOKill();
        Sequence OpenfadeSequence = DOTween.Sequence();
        OpenfadeSequence.SetUpdate(true);
        OpenfadeSequence.Append(TutorialPanel.GetComponent<CanvasGroup>().DOFade(1f, 0.15f));

        while (true)
        {
            RefreshPage(PageCount);
            string inputType = "";
            yield return null;
            yield return new WaitUntil(() =>
            {
                if (Keyboard.current?.fKey.wasPressedThisFrame == true)
                {
                    inputType = "Next";
                    return true;
                }
                if (Mouse.current?.leftButton.wasPressedThisFrame == true)
                {
                    inputType = "Next";
                    return true;
                }
                if (Keyboard.current?.rightArrowKey.wasPressedThisFrame == true && currentPage < PageCount - 1)
                {
                    inputType = "Next";
                    return true;
                }
                if (Keyboard.current?.dKey.wasPressedThisFrame == true && currentPage < PageCount - 1)
                {
                    inputType = "Next";
                    return true;
                }
                if (Keyboard.current?.aKey.wasPressedThisFrame == true)
                {
                    inputType = "Prev";
                    return true;
                }
                if (Keyboard.current?.leftArrowKey.wasPressedThisFrame == true)
                {
                    inputType = "Prev";
                    return true;
                }
                if (Keyboard.current?.escapeKey.wasPressedThisFrame == true)
                {
                    inputType = "Esc";
                    return true;
                }
                return false;
            });
            if (inputType == "Next")
            {
                currentPage++;
                if (currentPage >= PageCount)
                {
                    break;
                }
            }
            else if (inputType == "Prev")
            {
                currentPage--;
                if (currentPage < 0)
                    currentPage = 0;
            }
            else
            {
                break;
            }
        }
        Player.Instance.isInteracting = false;
        TutorialPanel.GetComponent<CanvasGroup>().DOKill();

        Sequence ClosefadeSequence = DOTween.Sequence();
        ClosefadeSequence.SetUpdate(true);
        ClosefadeSequence.Append(TutorialPanel.GetComponent<CanvasGroup>().DOFade(0f, 0.15f));
        ClosefadeSequence.OnComplete(() => {
            TutorialPanel.SetActive(false);
            TutorialPanel.GetComponent<CanvasGroup>().alpha = 1f;
        });

        if ((InventoryManager.Instance != null && InventoryManager.Instance.isInventoryOpen) || 
            (UIManager.Instance != null && UIManager.Instance.SignPanel.activeSelf))
        {
            UIManager.Instance.PreventCameraMoveAndClick(true);
            Time.timeScale = 0;
        }
        else if (UIManager.Instance != null && UIManager.Instance.GainPanel.activeSelf)
        {
            UIManager.Instance.PreventCameraMoveAndClick(true);
            Time.timeScale = 1;
        }
        else
        {
            UIManager.Instance.PreventCameraMoveAndClick(false);
            Time.timeScale = 1;
        }

        yield return null;
    }
}