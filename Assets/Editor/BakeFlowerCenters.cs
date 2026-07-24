#if UNITY_EDITOR

using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class BakeFlowerCenters : EditorWindow
{
    // 0.01f = 1cm, 0.001f = 1mm 범위 내의 정점은 같은 위치로 간주
    private float mergeThreshold = 0.001f;

    [MenuItem("Tools/Bake Flower Centers (Robust Hybrid)")]
    public static void ShowWindow() => GetWindow<BakeFlowerCenters>("Bake Pivot");

    private void OnGUI()
    {
        mergeThreshold = EditorGUILayout.FloatField("Merge Threshold", mergeThreshold);
        if (GUILayout.Button("Bake Centers (Robust) to UV2")) BakeUV2();
    }

    private void BakeUV2()
    {
        GameObject obj = Selection.activeGameObject;
        if (obj == null) return;

        MeshFilter mf = obj.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return;

        Mesh originalMesh = mf.sharedMesh;
        Mesh newMesh = Instantiate(originalMesh);
        newMesh.name = originalMesh.name + "_Baked_Robust";

        Vector3[] vertices = newMesh.vertices;
        int[] triangles = newMesh.triangles;
        List<Vector3> uv2 = new List<Vector3>(new Vector3[vertices.Length]);

        // 인접 리스트 (중복 체크를 빠르게 하기 위해 HashSet 사용)
        Dictionary<int, HashSet<int>> adjacency = new Dictionary<int, HashSet<int>>();

        // 1. 토폴로지 연결
        for (int i = 0; i < triangles.Length; i += 3)
        {
            AddEdge(adjacency, triangles[i], triangles[i + 1]);
            AddEdge(adjacency, triangles[i + 1], triangles[i + 2]);
            AddEdge(adjacency, triangles[i + 2], triangles[i]);
        }

        // 2. 좌표 기반 정점 병합 (더 넓은 그리드 사용)
        // mergeThreshold 단위로 그리드를 나눠서 같은 칸에 있으면 병합
        float invThreshold = 1f / mergeThreshold;
        Dictionary<Vector3Int, List<int>> posMap = new Dictionary<Vector3Int, List<int>>();

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3Int key = new Vector3Int(
                Mathf.RoundToInt(vertices[i].x * invThreshold),
                Mathf.RoundToInt(vertices[i].y * invThreshold),
                Mathf.RoundToInt(vertices[i].z * invThreshold)
            );

            if (!posMap.ContainsKey(key)) posMap[key] = new List<int>();
            posMap[key].Add(i);
        }

        foreach (var indices in posMap.Values)
        {
            if (indices.Count > 1)
            {
                for (int j = 1; j < indices.Count; j++)
                {
                    AddEdge(adjacency, indices[0], indices[j]);
                }
            }
        }

        // 3. 탐색 및 피벗 계산
        bool[] visited = new bool[vertices.Length];
        int flowerCount = 0;

        for (int i = 0; i < vertices.Length; i++)
        {
            if (!visited[i] && adjacency.ContainsKey(i))
            {
                List<int> island = new List<int>();
                Queue<int> q = new Queue<int>();
                q.Enqueue(i);
                visited[i] = true;

                while (q.Count > 0)
                {
                    int v = q.Dequeue();
                    island.Add(v);
                    foreach (int n in adjacency[v])
                    {
                        if (!visited[n]) { visited[n] = true; q.Enqueue(n); }
                    }
                }

                Vector3 pivotSum = Vector3.zero;
                int maxDegree = -1;
                List<int> candidates = new List<int>();

                foreach (int idx in island)
                {
                    int degree = adjacency[idx].Count;
                    if (degree > maxDegree)
                    {
                        maxDegree = degree;
                        candidates.Clear();
                        candidates.Add(idx);
                    }
                    else if (degree == maxDegree) candidates.Add(idx);
                }

                foreach (int idx in candidates) pivotSum += vertices[idx];
                Vector3 finalPivot = pivotSum / candidates.Count;

                foreach (int idx in island) uv2[idx] = finalPivot;
                flowerCount++;
            }
        }

        newMesh.SetUVs(1, uv2);
        mf.sharedMesh = newMesh;
        Debug.Log($"Bake Complete: {flowerCount} clusters found with threshold {mergeThreshold}");
    }

    private void AddEdge(Dictionary<int, HashSet<int>> adj, int v1, int v2)
    {
        if (!adj.ContainsKey(v1)) adj[v1] = new HashSet<int>();
        if (!adj.ContainsKey(v2)) adj[v2] = new HashSet<int>();
        adj[v1].Add(v2);
        adj[v2].Add(v1);
    }
}
#endif