using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using TMPro;

[RequireComponent(typeof(TMP_Dropdown))]
public class LocalizeTMPDropdown : MonoBehaviour
{
    [Header("로컬라이제이션 키 매핑 (Dropdown 옵션 순서대로 넣어주세요)")]
    [SerializeField] private List<LocalizedString> localizedOptions;

    private TMP_Dropdown _tmpDropdown;

    private void Awake()
    {
        _tmpDropdown = GetComponent<TMP_Dropdown>();
    }

    private void OnEnable()
    {
        // 언어 변경 이벤트 구독
        LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
        // 최초 실행 시 UI 갱신
        UpdateDropdownOptions();
    }

    private void OnDisable()
    {
        // 이벤트 구독 해제
        LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
    }

    private void OnLocaleChanged(Locale newLocale)
    {
        UpdateDropdownOptions();
    }

    private void UpdateDropdownOptions()
    {
        if (_tmpDropdown == null || localizedOptions == null || localizedOptions.Count == 0)
            return;

        // 현재 유저가 선택하고 있던 인덱스 저장
        int currentIndex = _tmpDropdown.value;

        // 기존 옵션 클리어
        _tmpDropdown.ClearOptions();

        // 선택지 새로 빌드
        List<TMP_Dropdown.OptionData> newOptions = new List<TMP_Dropdown.OptionData>();
        foreach (var localizedString in localizedOptions)
        {
            // GetLocalizedString()은 테이블이 로드되어 있다면 동기식으로 스트링을 가져옵니다.
            newOptions.Add(new TMP_Dropdown.OptionData(localizedString.GetLocalizedString()));
        }

        _tmpDropdown.options = newOptions;

        // 선택 인덱스 복구 및 UI 갱신
        _tmpDropdown.value = currentIndex;
        _tmpDropdown.RefreshShownValue();
    }
}