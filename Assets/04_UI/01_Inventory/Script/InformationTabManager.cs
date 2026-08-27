using JetBrains.Annotations;
using System.Linq;
using UnityEngine;

public class InformationTabManager : MonoBehaviour
{
    public GameObject AcquiredSkillContent;
    public GameObject SkillBookInfoPrefab;

    private void OnEnable()
    {
        if (InventoryManager.Instance == null) return;
        if (InventoryManager.Instance.MySkillBooks == null) return;

        for (int i = AcquiredSkillContent.transform.childCount - 1; i >= 0; i--)
        {
            Destroy(AcquiredSkillContent.transform.GetChild(i).gameObject);
        }

        float contentTotalHeight = 0f;
        float spacing = 25f; // 항목 간 간격

        foreach (var skillBook in InventoryManager.Instance.MySkillBooks)
        {
            GameObject infoObj = Instantiate(SkillBookInfoPrefab, AcquiredSkillContent.transform);
            AcquiredSkillUI infoPanel = infoObj.GetComponent<AcquiredSkillUI>();

            infoPanel.Initialize(skillBook);

            RectTransform rect = infoObj.GetComponent<RectTransform>();

            Canvas.ForceUpdateCanvases();

            contentTotalHeight += infoPanel.panelHeight + spacing;
        }

        AcquiredSkillContent.GetComponent<RectTransform>().sizeDelta = new Vector2(650, contentTotalHeight);
    }
}