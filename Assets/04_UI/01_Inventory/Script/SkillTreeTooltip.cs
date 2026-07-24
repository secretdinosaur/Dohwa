using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

public class SkillTreeTooltip : MonoBehaviour
{
    public static SkillTreeTooltip Instance { get; private set; }

    [Header("UI References")]
    public GameObject tooltipPanel;
    public TextMeshProUGUI nameText;
    public Image icon;
    public TextMeshProUGUI descText;
    public TextMeshProUGUI effectDescText;
    public TextMeshProUGUI Command;
    public TextMeshProUGUI Cost;
    public GameObject costButtonObj;

    [Tooltip("더 이상 텍스트를 표시하는 용도로 사용되지 않으나, 기존 연결 유지 및 비활성화를 위해 남겨둡니다.")]
    public TextMeshProUGUI conditionText;

    [Header("Localization Settings")]
    private string statStringTableName = "UI";
    public LocalizedString defaultStatNodeDesc;

    [Header("Localized Strings for Hardcoded Texts")]
    public LocalizedString statIncreaseFormat; // 예: "{0} 증가"
    public LocalizedString conditionTitle; // 예: "[해금 조건]"
    public LocalizedString conditionReachGradeFormat; // 예: "- {0} 달성"
    public LocalizedString conditionEventCleared; // 예: "- 특수 조건 필요"
    public LocalizedString conditionNeedTeatherFormat; // 예: "- 필요 조각: {0}개"

    [HideInInspector] public SkillBookBase currentSkillBook;
    [HideInInspector] public int currentNodeIndex;
    [HideInInspector] public SkillBookBase.SkillTreeNode data;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void ShowTooltip(SkillBookBase.SkillTreeNode data, SkillBookBase skillBook, int index)
    {
        StopAllCoroutines();

        currentSkillBook = skillBook;
        currentNodeIndex = index;
        this.data = data;

        tooltipPanel.SetActive(true);

        nameText.text = data.GetNodeName();
        icon.sprite = data.activeSkill != null ? data.activeSkill.icon : (data.passiveSkill != null ? data.passiveSkill.icon : InventoryManager.Instance.StatIcon);
        Cost.text = data.cost.ToString();

        Command.text = data.activeSkill != null ? CommandsToString(data.activeSkill.commands) : " ";
        bool hasCommand = data.activeSkill != null;
        Vector2 currentSize = effectDescText.rectTransform.sizeDelta;
        effectDescText.rectTransform.sizeDelta = new Vector2(currentSize.x, hasCommand ? 420f : 480f);
        string GetStatEffectString()
        {
            if (data.statModifiers == null || data.statModifiers.Length == 0) return "";

            List<string> statStrings = new List<string>();
            string increaseFormat = statIncreaseFormat != null && !statIncreaseFormat.IsEmpty ? statIncreaseFormat.GetLocalizedString() : "{0} 증가";

            foreach (var mod in data.statModifiers)
            {
                string localizedStatName = LocalizationSettings.StringDatabase.GetLocalizedString(statStringTableName, mod.statType.ToString());
                if (string.IsNullOrEmpty(localizedStatName)) localizedStatName = mod.statType.ToString();

                string statValueStr = $"{localizedStatName} {mod.statValue}";
                statStrings.Add(increaseFormat.Contains("{0}") ? string.Format(increaseFormat, statValueStr) : $"{statValueStr} 증가");
            }
            return string.Join("\n", statStrings);
        }

        // 1. Effect Desc 깔끔하게 가져오기
        string currentEffectDesc = data.GetNodeEffectDesc();

        if (data.nodeType == SkillBookBase.NodeType.StatSkill)
        {
            currentEffectDesc = GetStatEffectString();
        }

        // 해금 조건을 effectDesc 텍스트 뒤에 병합
        if (data.useCondition && data.conditions != null && data.conditions.Length > 0)
        {
            List<string> conditionStrings = new List<string>();
            foreach (var condition in data.conditions)
            {
                switch (condition.conditionType)
                {
                    case SkillBookBase.ConditionType.ReachGrade:
                        int value = int.Parse(condition.targetValue);
                        string gradeName = GradeManager.Instance.LocalizedGrades[value].GetLocalizedString();
                        string reachFormat = conditionReachGradeFormat != null && !conditionReachGradeFormat.IsEmpty ? conditionReachGradeFormat.GetLocalizedString() : "- {0} 달성";
                        conditionStrings.Add(reachFormat.Contains("{0}") ? string.Format(reachFormat, gradeName) : $"- {gradeName} 달성");
                        break;
                    case SkillBookBase.ConditionType.EventCleared:
                        string eventClearedStr = conditionEventCleared != null && !conditionEventCleared.IsEmpty ? conditionEventCleared.GetLocalizedString() : "- 특수 조건 필요";
                        conditionStrings.Add(eventClearedStr);
                        break;
                    case SkillBookBase.ConditionType.hasTeatherItem:
                        string teatherFormat = conditionNeedTeatherFormat != null && !conditionNeedTeatherFormat.IsEmpty ? conditionNeedTeatherFormat.GetLocalizedString() : "- 필요 조각: {0}개";
                        conditionStrings.Add(teatherFormat.Contains("{0}") ? string.Format(teatherFormat, condition.targetValue) : $"- 필요 조각: {condition.targetValue}개");
                        break;
                }
            }

            string titleStr = conditionTitle != null && !conditionTitle.IsEmpty ? conditionTitle.GetLocalizedString() : "[해금 조건]";
            string conditionStr = string.Join("\n", conditionStrings);

            if (!string.IsNullOrEmpty(currentEffectDesc))
                currentEffectDesc += $"\n\n{titleStr}\n{conditionStr}";
            else
                currentEffectDesc = $"{titleStr}\n{conditionStr}";
        }

        // 2. Base Desc(Item Desc) 깔끔하게 가져오기
        string baseDesc = data.GetNodeBaseDesc();

        // 사용자가 로컬라이즈를 할당하지 않은 스탯 스킬을 위해 기본값 fallback 처리
        if (data.nodeType == SkillBookBase.NodeType.StatSkill && (data.localizeStatNodeDesc == null || data.localizeStatNodeDesc.IsEmpty))
        {
            baseDesc = defaultStatNodeDesc != null && !defaultStatNodeDesc.IsEmpty ? defaultStatNodeDesc.GetLocalizedString() : "능력치 상승.";
        }

        // 💡 3. 노드 타입이 StatSkill일 경우에만 UI 출력 위치를 서로 스왑
        if (data.nodeType == SkillBookBase.NodeType.StatSkill)
        {
            effectDescText.text = baseDesc;
            descText.text = currentEffectDesc;
        }
        else
        {
            effectDescText.text = currentEffectDesc;
            descText.text = baseDesc;
        }

        if (conditionText != null) conditionText.gameObject.SetActive(false);

        bool isUnlocked = data.unlocked;
        float targetAlpha = isUnlocked ? 1f : 0.5f;
        if (costButtonObj != null) costButtonObj.SetActive(!isUnlocked && !(data.useCondition && data.autoUnlockOnCondition));
        if (Cost != null) Cost.gameObject.SetActive(!isUnlocked);

        SetAlpha(nameText, targetAlpha);
        SetAlpha(effectDescText, targetAlpha);
        SetAlpha(icon.transform.parent.GetComponent<Image>(), targetAlpha);
        SetAlpha(descText, targetAlpha);
        SetAlpha(icon, targetAlpha);
    }

    public void AnimateTooltipUnlock()
    {
        StopAllCoroutines();

        if (costButtonObj != null) costButtonObj.SetActive(false);
        if (Cost != null) Cost.gameObject.SetActive(false);

        float fadeDuration = 0.5f;

        SetAlpha(nameText, 0.5f);
        SetAlpha(effectDescText, 0.5f);
        Image iconBg = icon.transform.parent.GetComponent<Image>();
        if (iconBg != null) SetAlpha(iconBg, 0.5f);
        SetAlpha(descText, 0.5f);
        SetAlpha(icon, 0.5f);

        StartCoroutine(FadeElement(nameText, fadeDuration));
        if (iconBg != null) StartCoroutine(FadeElement(iconBg, fadeDuration));
        StartCoroutine(FadeElement(icon, fadeDuration));
        StartCoroutine(FadeElement(descText, fadeDuration));
        StartCoroutine(FadeElement(effectDescText, fadeDuration));
    }

    private IEnumerator FadeElement(Graphic graphic, float duration)
    {
        if (graphic == null) yield break;
        float time = 0;
        float startAlpha = graphic.color.a;

        while (time < duration)
        {
            time += Time.unscaledDeltaTime;
            float t = time / duration;
            float easedT = 1f - Mathf.Pow(1f - t, 1f);

            Color c = graphic.color;
            c.a = Mathf.Lerp(startAlpha, 1f, easedT);
            graphic.color = c;
            yield return null;
        }

        Color finalColor = graphic.color;
        finalColor.a = 1f;
        graphic.color = finalColor;
    }

    private void SetAlpha(Graphic graphic, float alpha)
    {
        if (graphic == null) return;
        Color color = graphic.color;
        color.a = alpha;
        graphic.color = color;
    }

    public void OnClickUnlockButton()
    {
        if (currentSkillBook != null)
        {
            currentSkillBook.UnlockNode(currentNodeIndex);
        }
    }

    private string CommandsToString(IEnumerable<SkillBase.Command> cmds, bool forText = true)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        foreach (var c in cmds) sb.Append(forText ? CommandToChar(c) : CharToCommand(c));
        return sb.ToString();
    }

    private string CommandToChar(SkillBase.Command c)
    {
        switch (c)
        {
            case SkillBase.Command.W: return "<sprite=0>";
            case SkillBase.Command.A: return "<sprite=3>";
            case SkillBase.Command.S: return "<sprite=2>";
            case SkillBase.Command.D: return "<sprite=1>";
            case SkillBase.Command.J: return "<sprite=4>";
            default: return "?";
        }
    }

    private string CharToCommand(SkillBase.Command c)
    {
        switch (c)
        {
            case SkillBase.Command.W: return "W";
            case SkillBase.Command.A: return "A";
            case SkillBase.Command.S: return "S";
            case SkillBase.Command.D: return "D";
            case SkillBase.Command.J: return "J";
            default: return "?";
        }
    }
}