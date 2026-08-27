using UnityEngine;

public class TerrainGridLODManager : MonoBehaviour
{
    public Transform playerTransform;
    public float tileSize = 100f;

    private Terrain[] terrainTiles;
    // int.MinValue 대신 0으로 초기화해도 문제 없습니다.
    private Vector2Int currentPlayerGridPos = Vector2Int.zero;

    void Start()
    {
        playerTransform = Player.Instance.transform;
        terrainTiles = Terrain.activeTerrains;

        // Start 단계에서 플레이어의 위치를 먼저 한 번 계산해줍니다.
        if (playerTransform != null)
        {
            currentPlayerGridPos = new Vector2Int(
                Mathf.FloorToInt(playerTransform.position.x / tileSize),
                Mathf.FloorToInt(playerTransform.position.z / tileSize)
            );
        }

        UpdateTerrainLODs();
    }

    void Update()
    {
        if (playerTransform == null || terrainTiles == null || terrainTiles.Length == 0) return;

        Vector2Int currentGridPos = new Vector2Int(
            Mathf.FloorToInt(playerTransform.position.x / tileSize),
            Mathf.FloorToInt(playerTransform.position.z / tileSize)
        );

        // 그리드 위치가 달라졌을 때만(타일을 넘어갔을 때만) LOD를 업데이트하여 최적화
        if (currentGridPos != currentPlayerGridPos)
        {
            currentPlayerGridPos = currentGridPos;
            UpdateTerrainLODs();
        }
    }

    private void UpdateTerrainLODs()
    {
        foreach (Terrain terrain in terrainTiles)
        {
            if (terrain == null) continue;

            Vector3 pos = terrain.transform.position;

            Vector2Int terrainGridPos = new Vector2Int(
                Mathf.FloorToInt(pos.x / tileSize),
                Mathf.FloorToInt(pos.z / tileSize)
            );

            int distanceX = Mathf.Abs(terrainGridPos.x - currentPlayerGridPos.x);
            int distanceZ = Mathf.Abs(terrainGridPos.y - currentPlayerGridPos.y);
            int gridDistance = Mathf.Max(distanceX, distanceZ);

            // 유니티의 기본 카메라 거리 기반 LOD 오차율을 0으로 만들어 무력화

            if (gridDistance <= 1)
            {
                terrain.heightmapMaximumLOD = 0;
                terrain.heightmapPixelError = 0;
            }
            else if (gridDistance <= 2)
            {
                terrain.heightmapMaximumLOD = 1;
                terrain.heightmapPixelError = 10;
            }
            else if (gridDistance <= 3)
            {
                terrain.heightmapMaximumLOD = 4;
                terrain.heightmapPixelError = 50;
            }
            else if (gridDistance <= 4)
            {
                terrain.heightmapMaximumLOD = 8;
                terrain.heightmapPixelError = 150;
            }
            else if (gridDistance <= 5)
            {
                terrain.heightmapMaximumLOD = 16;
                terrain.heightmapPixelError = 500;
            }
            else
            {
                terrain.heightmapMaximumLOD = 32;
                terrain.heightmapPixelError = 2500;
            }
        }
    }
}