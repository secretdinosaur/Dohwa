using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class MainManager : MonoBehaviour
{
    public GameObject SettingTab;

    public GameObject StartPanel;

    public Button SettingGameButton;
    public Button SettingKeyButton;
    public Button SettingCreditButton;

    public GameObject SettingGamePanel;
    public GameObject SettingKeyPanel;
    public GameObject SettingCreditPanel;

    public GameObject ResetPanel;

    public AudioClip mainBGM;
    public void Start()
    {
        StartCoroutine(InitializeLocalization());
        Application.runInBackground = false;
        StartCoroutine(FadePanel());
        SoundManager.Instance.PlaySpecialBGM(mainBGM);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
    private IEnumerator InitializeLocalization()
    {
        yield return UnityEngine.Localization.Settings.LocalizationSettings.InitializationOperation;
    }

    // 시작 시 화면이 밝아지는 코루틴
    private IEnumerator FadePanel()
    {
        Image panelImage = StartPanel.GetComponent<Image>();
        StartPanel.SetActive(true);

        Color color = panelImage.color;
        color.a = 1f;
        panelImage.color = color;

        float elapsed = 0f;

        while (elapsed < 5f)
        {
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame ||
                Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame ||
                Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                color.a = 0f;
                panelImage.color = color;
                StartPanel.SetActive(false);
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;

            color.a = Mathf.Lerp(1f, 0f, elapsed / 5f);
            panelImage.color = color;

            yield return null;
        }

        color.a = 0f;
        panelImage.color = color;
        StartPanel.SetActive(false);
    }

    public void OnClickSettingGameSubTab()
    {
        SettingTab.gameObject.SetActive(true);
        if (SettingGameButton != null) SettingGameButton.gameObject.SetActive(true);
        if (SettingKeyButton != null) SettingKeyButton.gameObject.SetActive(true);
        if (SettingCreditButton != null) SettingCreditButton.gameObject.SetActive(true);

        if (SettingGamePanel != null) SettingGamePanel.SetActive(true);
        if (SettingKeyPanel != null) SettingKeyPanel.SetActive(false);
        if (SettingCreditPanel != null) SettingCreditPanel.gameObject.SetActive(false);

        if (SettingGameButton != null) SettingGameButton.GetComponentInChildren<TextMeshProUGUI>().color = new Color(0, 0, 0, 1);
    }

    public void OnClickSettingKeySubTab()
    {
        SettingTab.gameObject.SetActive(true);
        if (SettingGameButton != null) SettingGameButton.gameObject.SetActive(true);
        if (SettingKeyButton != null) SettingKeyButton.gameObject.SetActive(true);
        if (SettingCreditButton != null) SettingCreditButton.gameObject.SetActive(true);

        if (SettingGamePanel != null) SettingGamePanel.SetActive(false);
        if (SettingKeyPanel != null) SettingKeyPanel.SetActive(true);
        if (SettingCreditPanel != null) SettingCreditPanel.gameObject.SetActive(false);

        if (SettingKeyButton != null) SettingKeyButton.GetComponentInChildren<TextMeshProUGUI>().color = new Color(0, 0, 0, 1);
    }

    public void OnClickSettingCreditSubTab()
    {
        SettingTab.gameObject.SetActive(true);
        if (SettingGameButton != null) SettingGameButton.gameObject.SetActive(true);
        if (SettingKeyButton != null) SettingKeyButton.gameObject.SetActive(true);
        if (SettingCreditButton != null) SettingCreditButton.gameObject.SetActive(true);

        if (SettingGamePanel != null) SettingGamePanel.SetActive(false);
        if (SettingKeyPanel != null) SettingKeyPanel.SetActive(false);
        if (SettingCreditPanel != null) SettingCreditPanel.gameObject.SetActive(true);

        if (SettingCreditButton != null) SettingCreditButton.GetComponentInChildren<TextMeshProUGUI>().color = new Color(0, 0, 0, 1);
    }

    public void OpenHideSettingTab()
    {
        SettingTab.SetActive(!SettingTab.activeSelf);
    }

    public void OnClickResetButton() => ResetPanel.SetActive(true);

    public void OnClickExitButton() => Application.Quit();
    public GameObject NewGamePanel;
    public void OnClickNewGameButton()
    {
        if (DataManager.Instance != null && DataManager.Instance.HasSaveData())
        {
            NewGamePanel.SetActive(true);
        }
        else
        {
            CreateNewGame();
        }
    }

    public void CreateNewGame()
    {
        StartCoroutine(FadeOutAndLoadScene("Game", () =>
        {
            DataManager.Instance.ResetData();
        }));
    }

    public void OnContinuewGameButton()
    {
        StartCoroutine(FadeOutAndLoadScene("Game"));
    }

    // 씬 이동 시 화면이 암전되는 코루틴
    private IEnumerator FadeOutAndLoadScene(string sceneName, Action beforeLoadAction = null)
    {
        Image panelImage = StartPanel.GetComponent<Image>();
        StartPanel.SetActive(true);

        Color color = panelImage.color;
        color.a = 0f;
        panelImage.color = color;

        float duration = 1.5f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            color.a = Mathf.Lerp(0f, 1f, elapsed / duration);
            panelImage.color = color;
            yield return null;
        }

        color.a = 1f;
        panelImage.color = color;

        beforeLoadAction?.Invoke();
        SceneManager.LoadScene(sceneName);
    }
}