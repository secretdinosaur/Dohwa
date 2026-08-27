using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine.Localization;
public class KeybindRow : MonoBehaviour
{
    public InputActionReference primaryAction;
    public int primaryIndex = 0;

    public InputActionReference secondaryAction;
    public int secondaryIndex = -1;

    public TMP_Text buttonText;

    private InputActionRebindingExtensions.RebindingOperation rebindOperation;
    public LocalizedString PressKey;
    private void OnEnable()
    {
        LoadBinding();
        UpdateUI();
    }

    public void StartRebinding()
    {
        StartCoroutine(RebindRoutine());
    }

    private IEnumerator RebindRoutine()
    {
        primaryAction.action.Disable();
        if (secondaryAction != null)
            secondaryAction.action.Disable();

        buttonText.text = PressKey.GetLocalizedString();

        // 게임 시간이 멈춘(Time.timeScale = 0) 상태에서도 대기할 수 있도록 변경
        yield return new WaitForSecondsRealtime(0.1f);

        rebindOperation = primaryAction.action.PerformInteractiveRebinding(primaryIndex)
            .WithExpectedControlType("Button")
            .WithControlsExcluding("<Mouse>")
            .WithControlsExcluding("<Gamepad>")
            .WithCancelingThrough("<Keyboard>/escape")
            .OnComplete(operation => CompleteRebind())
            .OnCancel(operation => CompleteRebind())
            .Start();
    }

    private void CompleteRebind()
    {
        rebindOperation.Dispose();

        string newPath = primaryAction.action.bindings[primaryIndex].effectivePath;

        // 1. Secondary 액션(예: WASD 그룹 내부 방향키)이 있다면 새 키를 덮어씌웁니다.
        if (secondaryAction != null && secondaryIndex >= 0)
        {
            secondaryAction.action.ApplyBindingOverride(secondaryIndex, newPath);
        }

        // 2. 중복 검사 실행 (자신들은 지우지 않도록 예외 처리된 함수 호출)
        RemoveDuplicateBindings(newPath);

        primaryAction.action.Enable();
        if (secondaryAction != null)
            secondaryAction.action.Enable();

        SaveBinding();

        // 3. 씬 전체 UI 갱신
        KeybindRow[] allRows = FindObjectsByType<KeybindRow>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var row in allRows)
        {
            row.UpdateUI();
        }
    }

    // 매개변수를 줄이고, 내부에서 Primary와 Secondary를 모두 판별하도록 수정한 버전
    private void RemoveDuplicateBindings(string newPath)
    {
        if (string.IsNullOrEmpty(newPath)) return;

        InputActionMap map = primaryAction.action.actionMap;

        foreach (var action in map.actions)
        {
            for (int i = 0; i < action.bindings.Count; i++)
            {
                // 핵심: 방금 변경한 Primary 액션은 지우지 않고 패스
                if (action == primaryAction.action && i == primaryIndex) continue;

                // 핵심: 방금 변경한 Secondary 연동 액션도 지우지 않고 패스
                if (secondaryAction != null && action == secondaryAction.action && i == secondaryIndex) continue;

                // 그 외에 이 키를 도둑맞은 다른 액션들만 빈칸으로 초기화
                if (action.bindings[i].effectivePath == newPath)
                {
                    action.ApplyBindingOverride(i, "");
                    PlayerPrefs.SetString("Rebind_" + action.id, action.SaveBindingOverridesAsJson());
                }
            }
        }
    }

    public void UpdateUI()
    {
        buttonText.text = primaryAction.action.GetBindingDisplayString(primaryIndex);
    }

    private void SaveBinding()
    {
        string rebinds1 = primaryAction.action.SaveBindingOverridesAsJson();
        PlayerPrefs.SetString("Rebind_" + primaryAction.action.id, rebinds1);

        if (secondaryAction != null)
        {
            string rebinds2 = secondaryAction.action.SaveBindingOverridesAsJson();
            PlayerPrefs.SetString("Rebind_" + secondaryAction.action.id, rebinds2);
        }
    }

    private void LoadBinding()
    {
        string rebinds1 = PlayerPrefs.GetString("Rebind_" + primaryAction.action.id, string.Empty);
        if (!string.IsNullOrEmpty(rebinds1))
        {
            primaryAction.action.LoadBindingOverridesFromJson(rebinds1);
        }

        if (secondaryAction != null)
        {
            string rebinds2 = PlayerPrefs.GetString("Rebind_" + secondaryAction.action.id, string.Empty);
            if (!string.IsNullOrEmpty(rebinds2))
            {
                secondaryAction.action.LoadBindingOverridesFromJson(rebinds2);
            }
        }
    }
}