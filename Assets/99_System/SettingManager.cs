using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;
using TMPro;
using UnityEngine.SceneManagement;

public class SettingManager : MonoBehaviour
{
    public Slider sfxSlider;
    public Slider bgmSlider;
    public Slider masterSlider;
    public Slider sensitivitySlider;

    [HideInInspector] public int currentLocaleIndex = 0;

    private int currentWidth = 1920;
    private int currentHeight = 1080;
    private FullScreenMode currentMode = FullScreenMode.FullScreenWindow;
    public CinemachineInputAxisController cinemachineInputAxisController;
    public GameObject ResetPanel;
    public GameObject TitlePanel;
    public GameObject QuitPanel;
    public TMP_Dropdown resoltionDropdown;
    public TMP_Dropdown screenDropdown;
    public TMP_Dropdown languageDropdown;
    private bool isChangingLanguage = false;

    private bool isUIReady = false;

    private float originalSensitivity = 0.5f;

    public static SettingManager Instance;
    public Texture2D cursorTexture;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        BindListeners();
        Cursor.SetCursor(cursorTexture, Vector2.zero, CursorMode.Auto);
    }

    public void OnOpenSettings()
    {
        StartCoroutine(InitUIRoutine());
    }

    private IEnumerator InitUIRoutine()
    {
        isUIReady = false;
        yield return new WaitForEndOfFrame();
        DataManager.Instance?.LoadSettingsData();
        isUIReady = true;
    }

    public void SyncUIs(float sfx, float bgm, float master, float sens, int localeIdx, int resIdx, int screenIdx)
    {
        if (sfxSlider != null) sfxSlider.SetValueWithoutNotify(sfx);
        if (bgmSlider != null) bgmSlider.SetValueWithoutNotify(bgm);
        if (masterSlider != null) masterSlider.SetValueWithoutNotify(master);
        if (sensitivitySlider != null) sensitivitySlider.SetValueWithoutNotify(sens);

        if (resoltionDropdown != null) resoltionDropdown.SetValueWithoutNotify(resIdx);
        if (screenDropdown != null) screenDropdown.SetValueWithoutNotify(screenIdx);
        if (languageDropdown != null) languageDropdown.SetValueWithoutNotify(localeIdx);

        UpdateResolutionValues(resIdx);
        UpdateWindowModeValues(screenIdx);
        ApplyResolution();

        ChangeSensitivity(sens);
        currentLocaleIndex = localeIdx;
        StartCoroutine(SetLocaleRoutine(localeIdx));
    }
    private void BindListeners()
    {
        if (sfxSlider != null)
        {
            sfxSlider.onValueChanged.AddListener((v) => { if (isUIReady) SoundManager.Instance?.SetSFXVolume(v); });
            AddPointerUpListener(sfxSlider);
        }

        if (bgmSlider != null)
        {
            bgmSlider.onValueChanged.AddListener((v) => { if (isUIReady) SoundManager.Instance?.SetBGMVolume(v); });
            AddPointerUpListener(bgmSlider);
        }

        if (masterSlider != null)
        {
            masterSlider.onValueChanged.AddListener((v) => { if (isUIReady) SoundManager.Instance?.SetMasterVolume(v); });
            AddPointerUpListener(masterSlider);
        }

        if (sensitivitySlider != null)
        {
            sensitivitySlider.onValueChanged.AddListener((v) => {
                if (!isUIReady) return;
                originalSensitivity = v;
                ChangeSensitivity(v);
            });
            AddPointerUpListener(sensitivitySlider);
        }

        if (resoltionDropdown != null)
            resoltionDropdown.onValueChanged.AddListener(ChangeResolution);

        if (screenDropdown != null)
            screenDropdown.onValueChanged.AddListener(ChangeWindowMode);

        if (languageDropdown != null)
            languageDropdown.onValueChanged.AddListener(ChangeLocale);
    }

    private void AddPointerUpListener(Slider slider)
    {
        EventTrigger trigger = slider.gameObject.GetComponent<EventTrigger>() ?? slider.gameObject.AddComponent<EventTrigger>();
        EventTrigger.Entry entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
        entry.callback.AddListener((data) => { DataManager.Instance?.SaveSettingsData(); });
        trigger.triggers.Add(entry);
    }

    public void ChangeResolution(int index)
    {
        if (!isUIReady) return;

        UpdateResolutionValues(index);
        ApplyResolution();
        DataManager.Instance?.SaveSettingsData();
    }

    private void UpdateResolutionValues(int index)
    {
        switch (index)
        {
            case 0: currentWidth = 1920; currentHeight = 1080; break;
            case 1: currentWidth = 2560; currentHeight = 1440; break;
            case 2: currentWidth = 1280; currentHeight = 720; break;
        }
    }

    public void ChangeWindowMode(int index)
    {
        if (!isUIReady) return;

        UpdateWindowModeValues(index);
        ApplyResolution();
        DataManager.Instance?.SaveSettingsData();
    }

    private void UpdateWindowModeValues(int index)
    {
        switch (index)
        {
            case 0: currentMode = FullScreenMode.FullScreenWindow; break;
            case 1: currentMode = FullScreenMode.Windowed; break;
        }
    }

    private void ApplyResolution()
    {
        Screen.SetResolution(currentWidth, currentHeight, currentMode);
    }

    public void ChangeSensitivity(float value)
    {
        if (cinemachineInputAxisController != null && cinemachineInputAxisController.Controllers.Count >= 2)
        {
            cinemachineInputAxisController.Controllers[0].Input.Gain = value * 10;
            cinemachineInputAxisController.Controllers[1].Input.Gain = -value * 10;
        }
    }

    public void SetSensitivityZero()
    {
        if (cinemachineInputAxisController != null && cinemachineInputAxisController.Controllers.Count >= 2)
        {
            cinemachineInputAxisController.Controllers[0].Input.Gain = 0f;
            cinemachineInputAxisController.Controllers[1].Input.Gain = 0f;
        }
    }

    public void RestoreSensitivity()
    {
        ChangeSensitivity(originalSensitivity);
    }

    public void ChangeLocale(int localeIndex)
    {
        if (!isUIReady || isChangingLanguage) return;

        currentLocaleIndex = localeIndex;
        StartCoroutine(SetLocaleRoutine(localeIndex));
        DataManager.Instance?.SaveSettingsData();
    }

    private IEnumerator SetLocaleRoutine(int localeIndex)
    {
        isChangingLanguage = true;

        if (!LocalizationSettings.InitializationOperation.IsDone)
        {
            yield return LocalizationSettings.InitializationOperation;
        }

        if (localeIndex >= 0 && localeIndex < LocalizationSettings.AvailableLocales.Locales.Count)
        {
            LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.Locales[localeIndex];
        }

        isChangingLanguage = false;
    }

    public void OnClickResetButton() => ResetPanel.SetActive(true);

    public void OnClickExitButton()
    {
        DataManager.Instance.AllSave();
        Application.Quit();
    }

    public void OnClickTitleButton()
    {
        DataManager.Instance.AllSave();
        Time.timeScale = 1f;
        SceneManager.LoadScene("Main");
    }
}