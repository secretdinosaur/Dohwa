using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;

public class SkillNodeUI : MonoBehaviour, IPointerClickHandler
{
    public SkillBookBase.SkillTreeNode nodeData;
    public SkillBookBase ParentSkillBook;
    public int nodeIndex;

    public Image bgSlot;
    public Image bgIconImage;
    public Image fillSlot;
    public Image fillIconImage;
    public TextMeshProUGUI nameText;
    public static bool IsAnimating { get; private set; }
    private bool initialized = false;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (IsAnimating) return;

        if (SkillTreeTooltip.Instance != null && nodeData != null)
        {
            SoundManager.Instance.PlayUISound(4);
            SkillTreeTooltip.Instance.gameObject.SetActive(true);
            InventoryManager.Instance.BookInfo.SetActive(false);
            SkillTreeTooltip.Instance.ShowTooltip(nodeData, ParentSkillBook, nodeIndex);
        }
    }

    public void Init(SkillBookBase.SkillTreeNode data, SkillBookBase parent, int index, Sprite Staticon = null)
    {
        nodeData = data;
        ParentSkillBook = parent;
        nodeIndex = index;

        Sprite iconSp = nodeData.activeSkill ? nodeData.activeSkill.icon : (nodeData.passiveSkill ? nodeData.passiveSkill.icon : Staticon);
        if (bgIconImage != null) bgIconImage.sprite = iconSp;
        if (fillIconImage != null) fillIconImage.sprite = iconSp;
        RefreshUI(false);

        if (ParentSkillBook != null)
            ParentSkillBook.OnNodeUnlocked += HandleNodeUnlocked;

        initialized = true;

        if (Application.isPlaying) InitializeLineState();
    }

    private void InitializeLineState()
    {
        if (nodeData == null || nodeData.prerequisiteIndices == null || nodeData.prerequisiteIndices.Length == 0) return;

        if (nodeData.unlocked)
        {
            Transform linesLayer = FindLinesLayer();
            if (linesLayer != null)
            {
                foreach (int reqIndex in nodeData.prerequisiteIndices)
                {
                    Transform common = linesLayer.Find($"Line_{reqIndex}_Common");
                    if (common != null)
                    {
                        var line = common.GetComponent<SkillTreeLine>();
                        if (line != null) line.SetFull();
                    }

                    Transform horiz = linesLayer.Find($"Line_{reqIndex}_{nodeIndex}_Horiz");
                    if (horiz != null)
                    {
                        var line = horiz.GetComponent<SkillTreeLine>();
                        if (line != null) line.SetFull();
                    }
                }

                Transform drop = linesLayer.Find($"Line_{nodeIndex}_Drop");
                if (drop != null)
                {
                    var line = drop.GetComponent<SkillTreeLine>();
                    if (line != null) line.SetFull();
                }
            }
        }
    }

    private void HandleNodeUnlocked(int idx)
    {
        if (idx == nodeIndex) StartCoroutine(AnimateUnlockSequence());
    }

    private IEnumerator AnimateUnlockSequence()
    {
        IsAnimating = true;
        Transform linesLayer = FindLinesLayer();
        List<Coroutine> lineCoroutines = new List<Coroutine>();

        if (linesLayer != null && nodeData.prerequisiteIndices != null)
        {
            foreach (int reqIndex in nodeData.prerequisiteIndices)
            {
                Transform common = linesLayer.Find($"Line_{reqIndex}_Common");
                if (common != null)
                {
                    var line = common.GetComponent<SkillTreeLine>();
                    if (line != null && line.gameObject.activeInHierarchy)
                        lineCoroutines.Add(StartCoroutine(line.AnimateLineFill()));
                }

                Transform horiz = linesLayer.Find($"Line_{reqIndex}_{nodeIndex}_Horiz");
                if (horiz != null)
                {
                    var line = horiz.GetComponent<SkillTreeLine>();
                    if (line != null && line.gameObject.activeInHierarchy)
                        lineCoroutines.Add(StartCoroutine(line.AnimateLineFill()));
                }
            }

            Transform drop = linesLayer.Find($"Line_{nodeIndex}_Drop");
            if (drop != null)
            {
                var line = drop.GetComponent<SkillTreeLine>();
                if (line != null && line.gameObject.activeInHierarchy)
                    lineCoroutines.Add(StartCoroutine(line.AnimateLineFill()));
            }
        }

        foreach (Coroutine c in lineCoroutines)
        {
            if (c != null) yield return c;
        }

        float duration = 0.2f;
        float time = 0;
        while (time < duration)
        {
            time += Time.unscaledDeltaTime;
            float rawT = time / duration;
            float easedT = 1f - Mathf.Pow(1f - rawT, 2f);

            if (fillSlot) fillSlot.fillAmount = easedT;
            if (fillIconImage) fillIconImage.fillAmount = easedT;

            yield return null;
        }

        if (fillSlot) fillSlot.fillAmount = 1f;
        if (fillIconImage) fillIconImage.fillAmount = 1f;

        RefreshUI(false);

        yield return new WaitForSecondsRealtime(0.2f);

        try
        {
            if (SkillTreeTooltip.Instance != null && SkillTreeTooltip.Instance.currentNodeIndex == nodeIndex)
            {
                if (SkillTreeTooltip.Instance.gameObject.activeInHierarchy)
                    SkillTreeTooltip.Instance.AnimateTooltipUnlock();
            }
        }
        catch (System.Exception e)
        {
        }

        IsAnimating = false;
    }

    public void RefreshUI(bool isAnimating)
    {
        if (isAnimating) return;
        bool isUnlocked = nodeData.unlocked;
        float alpha = isUnlocked ? 1f : 0.3f;

        if (nameText != null)
        {
            nameText.text = nodeData.GetNodeName();
            Color c = nameText.color; c.a = alpha; nameText.color = c;
        }

        if (bgSlot) { Color c = bgSlot.color; c.a = alpha; bgSlot.color = c; }
        if (bgIconImage) { Color c = bgIconImage.color; c.a = alpha; bgIconImage.color = c; }
        if (fillSlot) fillSlot.fillAmount = isUnlocked ? 1f : 0f;
        if (fillIconImage) fillIconImage.fillAmount = isUnlocked ? 1f : 0f;
    }

    private Transform FindLinesLayer()
    {
        Transform curr = transform;
        while (curr.parent != null)
        {
            Transform lines = curr.parent.Find("Lines");
            if (lines != null) return lines;
            curr = curr.parent;
        }
        return null;
    }

    private void OnDisable()
    {
        IsAnimating = false;
    }

    private void OnDestroy()
    {
        if (ParentSkillBook != null)
            ParentSkillBook.OnNodeUnlocked -= HandleNodeUnlocked;
    }
}