using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Localization;

public class StatToolTip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private LocalizedString toolTip;
    private RectTransform rectTransform;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }
    public void OnPointerEnter(PointerEventData eventData)
    {
        InventoryManager.Instance.StartShowStatToolTip(toolTip.GetLocalizedString(), rectTransform);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        InventoryManager.Instance.HideStatToolTip();
    }
}
