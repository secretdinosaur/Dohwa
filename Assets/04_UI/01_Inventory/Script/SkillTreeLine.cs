using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SkillTreeLine : MonoBehaviour
{
    private Sprite lineSprite;
    private float lineThickness;

    public void Init(Sprite sprite, float thickness)
    {
        lineSprite = sprite;
        lineThickness = thickness;
    }

    public void DrawSpecificSegment(Vector2 from, Vector2 to, bool touchStart, bool touchEnd)
    {
        CreateSegment(from, to, touchStart, touchEnd);
    }

    void CreateSegment(Vector2 from, Vector2 to, bool touchStartNode, bool touchEndNode)
    {
        if (from == to) return;

        float thick = lineThickness > 0 ? lineThickness : 2f;
        Vector2 dir = to - from;
        float dist = dir.magnitude;

        float length = dist + thick - 5;
        Vector2 centerPos = (from + to) / 2f;

        if (touchStartNode)
        {
            length -= thick / 2f;
            centerPos += dir.normalized * (thick / 4f);
        }
        if (touchEndNode)
        {
            length -= thick / 2f;
            centerPos -= dir.normalized * (thick / 4f);
        }

        GameObject baseSegment = new GameObject("BaseSegment", typeof(Image));
        baseSegment.transform.SetParent(transform, false);
        RectTransform baseRt = baseSegment.GetComponent<RectTransform>();
        baseRt.pivot = new Vector2(0.5f, 0.5f);
        baseRt.sizeDelta = new Vector2(length, thick);
        baseRt.anchoredPosition = centerPos;
        baseRt.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);

        Image baseImg = baseSegment.GetComponent<Image>();
        baseImg.sprite = lineSprite;
        baseImg.color = new Color(0, 0, 0, 0.3f);

        GameObject fillSegment = new GameObject("FillSegment", typeof(Image));
        fillSegment.transform.SetParent(baseSegment.transform, false);
        RectTransform fillRt = fillSegment.GetComponent<RectTransform>();
        fillRt.pivot = new Vector2(0.5f, 0.5f);
        fillRt.sizeDelta = new Vector2(length, thick);
        fillRt.anchoredPosition = Vector2.zero;
        fillRt.localRotation = Quaternion.identity;

        Image fillImg = fillSegment.GetComponent<Image>();
        fillImg.sprite = lineSprite;
        fillImg.color = new Color(0, 0, 0, 1f);
        fillImg.type = Image.Type.Filled;
        fillImg.fillMethod = Image.FillMethod.Horizontal;
        fillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
        fillImg.fillAmount = 0f;
    }

    public IEnumerator AnimateLineFill()
    {
        float speed = 1200f;
        Image[] allImages = GetComponentsInChildren<Image>(true);
        List<Image> fills = new List<Image>();

        foreach (var img in allImages)
        {
            if (img.gameObject.name == "FillSegment" && img.fillAmount < 0.99f)
                fills.Add(img);
        }

        foreach (Image img in fills)
        {
            float length = img.rectTransform.sizeDelta.x;
            float duration = (length > 0f) ? (length / speed) : 0.001f;
            float time = 0;

            while (time < duration)
            {
                time += Time.unscaledDeltaTime;
                float rawT = time / duration;
                float easedT = 1f - Mathf.Pow(1f - rawT, 2f);

                img.fillAmount = Mathf.Clamp01(easedT);
                yield return null;
            }
            img.fillAmount = 1f;
        }
    }

    public void SetFull()
    {
        foreach (Transform child in transform)
        {
            Transform fill = child.Find("FillSegment");
            if (fill) fill.GetComponent<Image>().fillAmount = 1f;
        }
    }
}
