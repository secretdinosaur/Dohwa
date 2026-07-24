using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AcquiredSkillUI : MonoBehaviour
{
    public TextMeshProUGUI SkillBookName;
    public Image SkillBookIcon;
    public GameObject SkillPanelPrefab;
    public GameObject SkillPanelContainer;
    public float panelHeight = 75f;
    public void Initialize(SkillBookBase skillBook)
    {
        for (int i = SkillPanelContainer.transform.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(SkillPanelContainer.transform.GetChild(i).gameObject);
        }

        SkillBookName.text = skillBook.skillBookName;
        SkillBookIcon.sprite = skillBook.icon;

        if (skillBook.skillTree == null) return;

        var acquiredNodes = skillBook.skillTree
            .Where(node => node.unlocked &&
                  (node.nodeType == SkillBookBase.NodeType.ActiveSkill || node.nodeType == SkillBookBase.NodeType.PassiveSkill))
            .ToList();

        foreach (var node in acquiredNodes)
        {
            var panel = Instantiate(SkillPanelPrefab, SkillPanelContainer.transform).GetComponent<AcquiredSkillPanel>();

            if (node.nodeType == SkillBookBase.NodeType.ActiveSkill && node.activeSkill != null)
            {
                panel.skillName.text = node.activeSkill.skillName;
                panel.icon.sprite = node.activeSkill.icon;
            }
            else if (node.nodeType == SkillBookBase.NodeType.PassiveSkill && node.passiveSkill != null)
            {
                panel.skillName.text = node.passiveSkill.passiveName;
                panel.icon.sprite = node.passiveSkill.icon;
            }
        }


        // 3. 만약 Content Size Fitter를 쓰지 않는다면, 계산된 preferredHeight를 적용합니다.
        float finalHeight = LayoutUtility.GetPreferredHeight(SkillPanelContainer.GetComponent<RectTransform>());
        GetComponent<RectTransform>().sizeDelta = new Vector2(800, acquiredNodes.Count * 75 + 75);
        panelHeight = acquiredNodes.Count * 75 + 125;
    }
}