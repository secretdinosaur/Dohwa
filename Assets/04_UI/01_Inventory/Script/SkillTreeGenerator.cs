using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
public class SkillTreeGenerator : MonoBehaviour
{
    public SkillBookBase skillBook;
    public GameObject nodePrefab;
    public RectTransform container;

    [Header("Tree Spacing Settings")]
    public float horizontalSpacing = 120f;
    public float verticalSpacing = 150f;
    public float parentGroupSpacing = 50f;

    [Header("Line Settings")]
    public Sprite inkLineSprite;
    public float lineThickness = 8f;
    public float nodeYOffset = 45f;

    private Dictionary<int, SkillNodeRuntime> runtimeNodes = new Dictionary<int, SkillNodeRuntime>();
    public SkillBookBase currentSkillBook;

    public class SkillNodeRuntime
    {
        public int index;
        public SkillBookBase.SkillTreeNode data;
        public List<SkillNodeRuntime> layoutChildren = new List<SkillNodeRuntime>();
        public List<SkillNodeRuntime> connectionChildren = new List<SkillNodeRuntime>();
        public RectTransform uiRect;
        public float subtreeWidth;
    }

    [ContextMenu("Generate Tree In Editor")]
    public void GenerateTreeEditor() { if (skillBook != null) GenerateTree(skillBook); }

    public void GenerateTree(SkillBookBase book)
    {
        currentSkillBook = book;
        for (int i = container.childCount - 1; i >= 0; i--)
            DestroyImmediate(container.GetChild(i).gameObject);

        runtimeNodes.Clear();
        GameObject treeRootObj = new GameObject(currentSkillBook.name, typeof(RectTransform));
        treeRootObj.transform.SetParent(container, false);
        RectTransform treeRootRT = treeRootObj.GetComponent<RectTransform>();
        treeRootRT.anchoredPosition = Vector2.zero;
        treeRootRT.sizeDelta = Vector2.zero;

        for (int i = 0; i < book.skillTree.Length; i++)
            runtimeNodes[i] = new SkillNodeRuntime { index = i, data = book.skillTree[i] };

        List<SkillNodeRuntime> roots = new List<SkillNodeRuntime>();
        foreach (var node in runtimeNodes.Values)
        {
            if (node.data.prerequisiteIndices == null || node.data.prerequisiteIndices.Length == 0)
            {
                roots.Add(node);
            }
            else
            {
                int primaryParent = node.data.prerequisiteIndices[0];
                if (runtimeNodes.ContainsKey(primaryParent))
                    runtimeNodes[primaryParent].layoutChildren.Add(node);

                foreach (int req in node.data.prerequisiteIndices)
                {
                    if (runtimeNodes.ContainsKey(req))
                        runtimeNodes[req].connectionChildren.Add(node);
                }
            }
        }

        foreach (var root in roots) CalculateSubtreeWidth(root);

        float totalTreeWidth = 0;
        foreach (var root in roots) totalTreeWidth += root.subtreeWidth;
        totalTreeWidth += (roots.Count - 1) * (horizontalSpacing + parentGroupSpacing);

        int maxTreeDepth = 0;
        foreach (var root in roots)
        {
            int depth = GetMaxDepth(root);
            if (depth > maxTreeDepth) maxTreeDepth = depth;
        }

        float currentX = -totalTreeWidth / 2f;
        float startY = ((maxTreeDepth - 1) * verticalSpacing) / 2f;

        foreach (var root in roots)
        {
            float rootWidth = root.subtreeWidth;
            ArrangeNodes(root, new Vector2(currentX + rootWidth / 2f, startY), treeRootRT);
            currentX += rootWidth + horizontalSpacing + parentGroupSpacing;
        }

        AdjustMultiParentChildren();

        CreateConnections(treeRootRT);
    }

    private void AdjustMultiParentChildren()
    {
        foreach (var node in runtimeNodes.Values)
        {
            if (node.data.prerequisiteIndices != null && node.data.prerequisiteIndices.Length > 1)
            {
                float sumX = 0;
                int validParents = 0;

                foreach (int pIdx in node.data.prerequisiteIndices)
                {
                    if (runtimeNodes.TryGetValue(pIdx, out var pNode))
                    {
                        sumX += pNode.uiRect.anchoredPosition.x;
                        validParents++;
                    }
                }

                if (validParents > 0)
                {
                    float targetX = sumX / validParents;
                    float deltaX = targetX - node.uiRect.anchoredPosition.x;
                    ShiftNodeSubtree(node, deltaX);
                }
            }
        }
    }

    private void ShiftNodeSubtree(SkillNodeRuntime node, float deltaX)
    {
        Vector2 pos = node.uiRect.anchoredPosition;
        pos.x += deltaX;
        node.uiRect.anchoredPosition = pos;

        foreach (var child in node.layoutChildren)
        {
            ShiftNodeSubtree(child, deltaX);
        }
    }

    int GetMaxDepth(SkillNodeRuntime node)
    {
        if (node.layoutChildren.Count == 0) return 1;
        int maxChildDepth = 0;
        foreach (var child in node.layoutChildren)
        {
            int childDepth = GetMaxDepth(child);
            if (childDepth > maxChildDepth) maxChildDepth = childDepth;
        }
        return maxChildDepth + 1;
    }

    float CalculateSubtreeWidth(SkillNodeRuntime node)
    {
        if (node.layoutChildren.Count == 0) return node.subtreeWidth = 100f;
        float totalWidth = 0;
        foreach (var child in node.layoutChildren) totalWidth += CalculateSubtreeWidth(child);
        return node.subtreeWidth = Mathf.Max(100f, totalWidth + (node.layoutChildren.Count - 1) * horizontalSpacing);
    }

    void ArrangeNodes(SkillNodeRuntime node, Vector2 pos, Transform parent)
    {
        GameObject obj = Instantiate(nodePrefab, parent, false);
        node.uiRect = obj.GetComponent<RectTransform>();
        node.uiRect.anchoredPosition = pos;

        var ui = obj.GetComponentInChildren<SkillNodeUI>();
        if (ui != null) ui.Init(node.data, currentSkillBook, node.index);

        float nextX = pos.x - (node.subtreeWidth / 2f);
        foreach (var child in node.layoutChildren)
        {
            float childWidth = child.subtreeWidth;
            ArrangeNodes(child, new Vector2(nextX + childWidth / 2f, pos.y - verticalSpacing), parent);
            nextX += childWidth + horizontalSpacing;
        }
    }

    void CreateConnections(Transform parentTransform)
    {
        GameObject lineLayer = new GameObject("Lines", typeof(RectTransform));
        lineLayer.transform.SetParent(parentTransform, false);
        lineLayer.transform.SetAsFirstSibling();

        foreach (var parent in runtimeNodes.Values)
        {
            if (parent.connectionChildren.Count == 0) continue;

            Vector2 pBot = parent.uiRect.anchoredPosition; pBot.y -= nodeYOffset;
            float midY = Mathf.Lerp(pBot.y, parent.connectionChildren[0].uiRect.anchoredPosition.y + nodeYOffset, 0.5f);
            Vector2 midP = new Vector2(pBot.x, midY);

            GameObject cLineObj = new GameObject($"Line_{parent.index}_Common", typeof(SkillTreeLine));
            cLineObj.transform.SetParent(lineLayer.transform, false);
            SkillTreeLine cLine = cLineObj.GetComponent<SkillTreeLine>();
            cLine.Init(inkLineSprite, lineThickness);
            cLine.DrawSpecificSegment(pBot, midP, true, false);

            foreach (var child in parent.connectionChildren)
            {
                GameObject hLineObj = new GameObject($"Line_{parent.index}_{child.index}_Horiz", typeof(SkillTreeLine));
                hLineObj.transform.SetParent(lineLayer.transform, false);
                SkillTreeLine hLine = hLineObj.GetComponent<SkillTreeLine>();
                hLine.Init(inkLineSprite, lineThickness);

                Vector2 cTop = child.uiRect.anchoredPosition; cTop.y += nodeYOffset;
                Vector2 cMid = new Vector2(cTop.x, midY);

                hLine.DrawSpecificSegment(midP, cMid, false, false);
                // 수정됨: 프리팹 구울 때는 해금 상태와 무관하게 무조건 빈 선으로 둡니다.
            }
        }

        foreach (var child in runtimeNodes.Values)
        {
            if (child.data.prerequisiteIndices == null || child.data.prerequisiteIndices.Length == 0) continue;

            int firstParentIdx = child.data.prerequisiteIndices[0];
            if (!runtimeNodes.TryGetValue(firstParentIdx, out var firstParent)) continue;

            Vector2 pBot = firstParent.uiRect.anchoredPosition; pBot.y -= nodeYOffset;
            Vector2 cTop = child.uiRect.anchoredPosition; cTop.y += nodeYOffset;
            float midY = Mathf.Lerp(pBot.y, cTop.y, 0.5f);
            Vector2 cMid = new Vector2(cTop.x, midY);

            GameObject dLineObj = new GameObject($"Line_{child.index}_Drop", typeof(SkillTreeLine));
            dLineObj.transform.SetParent(lineLayer.transform, false);
            SkillTreeLine dLine = dLineObj.GetComponent<SkillTreeLine>();
            dLine.Init(inkLineSprite, lineThickness);

            dLine.DrawSpecificSegment(cMid, cTop, false, true);
            // 수정됨: 프리팹 구울 때는 해금 상태와 무관하게 무조건 빈 선으로 둡니다.
        }
    }
}