using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InventorySlot : MonoBehaviour, IScrollHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public InventoryItem item;

    [Header("Slot Settings")]
    public InventoryManager.InventoryType allowedType;
    public InventoryManager.SlotType slotMode;

    public CanvasGroup canvasGroup;
    Image image;
    RectTransform rectTransform;
    ScrollRect scrollRect;
    public Vector3 TooltipOffset
    {
        get
        {
            if (rectTransform == null) return Vector3.zero;

            float slotWidth = rectTransform.rect.width;
            float slotHeight = rectTransform.rect.height;

            float offsetX = slotWidth * (1f - rectTransform.pivot.x);

            float targetPivotY = CalculatedPivotY;

            float offsetY = slotHeight * (targetPivotY - rectTransform.pivot.y);

            return new Vector3(offsetX, offsetY, 0f);
        }
    }
    public Vector3 ContextMenuOffset
    {
        get
        {
            float slotWidth = rectTransform.rect.width;
            float slotHeight = rectTransform.rect.height;

            float pivotX = rectTransform.pivot.x;
            float pivotY = rectTransform.pivot.y;

            float screenCenterX = Screen.width * 0.5f;
            float slotPosX = rectTransform.position.x;
            float effectivePivotX = (slotPosX > screenCenterX) ? (1f - pivotX) : pivotX;

            float offsetX = slotWidth * (1f - effectivePivotX);
            float offsetY = slotHeight * (1f - pivotY);

            return new Vector3(offsetX, offsetY, 0f);
        }
    }

    public float CalculatedPivotY
    {
        get
        {
            float customCenterY = Screen.height * 0.4f;

            // ⭐ 2. 최대치/최소치가 "이미 완료될" 화면 상하단 한계선 정의
            // 예: 화면 아래에서 20% 지점(하단 패딩), 화면 위에서 20% 지점(상단 패딩)
            float minValidY = Screen.height * 0.13f;         // 하단 한계선 (최소 피벗 0.1이 되는 곳)
            float maxValidY = Screen.height * (1f - 0.33f);  // 상단 한계선 (최대 피벗 0.8이 되는 곳)

            // 3. 현재 슬롯의 위치가 한계선 사이에 어디쯤 있는지 비율(t: 0~1)로 계산
            // 한계선을 벗어나면 각각 0과 1로 고정(InverseLerp + Clamp01)시킵니다.
            float currentY = transform.position.y;
            float t = Mathf.InverseLerp(minValidY, maxValidY, currentY);

            // 4. 계산된 비율에 따라 피벗을 0.1 ~ 0.8 사이로 매핑
            // 이제 슬롯이 maxValidY(화면 상단 80% 지점)보다 위에 있으면 무조건 0.8로 고정되고,
            // minValidY(화면 하단 20% 지점)보다 아래에 있으면 무조건 0.1로 고정됩니다.
            return Mathf.Lerp(0.1f, 0.8f, t);
        }
    }

    private void Awake()
    {
        image = GetComponent<Image>();
        rectTransform = GetComponent<RectTransform>();
        item = GetComponentInChildren<InventoryItem>();
        canvasGroup = GetComponent<CanvasGroup>();
        scrollRect = GetComponentInParent<ScrollRect>();
    }

    public void OnClick()
    {
        InventoryManager.Instance.selectedItem = this;
        if (!InventoryManager.Instance.ItemContextMenu.gameObject.activeSelf && item.isSettedItem)
            InventoryManager.Instance.ShowItmeContextMenu();
        else
            InventoryManager.Instance.HideItemContextMenu();
    }

    public void OnPointerEnter()
    {
        if (item.weapon == null && item.armor == null && item.skillBook == null && item.consumable == null && item.nonConsumable == null) return;
        if (slotMode == InventoryManager.SlotType.Equipment)
            return;

        InventoryManager.Instance.ShowItemTooltip(item);
    }

    public void OnPointerExit()
    {
        InventoryManager.Instance.HideItemTooltip();
    }

    public void OnScroll(PointerEventData eventData)
    {
        if (scrollRect != null)
            scrollRect.OnScroll(eventData);
    }
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (scrollRect != null)
            scrollRect.OnBeginDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if(scrollRect != null)
            scrollRect.OnDrag(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (scrollRect != null)
            scrollRect.OnEndDrag(eventData);
    }
}