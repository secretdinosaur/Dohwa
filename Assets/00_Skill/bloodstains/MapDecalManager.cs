using UnityEngine;
using UnityEngine.Rendering.Universal;
using System.Collections;

public class MapDecalManager : MonoBehaviour
{
    public static MapDecalManager Instance { get; private set; }

    public GameObject decalPrefab;
    public Material[] decalMaterials;

    public Vector2 widthRange = new Vector2(0.5f, 1.5f);
    public Vector2 heightRange = new Vector2(0.5f, 1.5f);
    public float depth = 1.0f;

    public float lifeTime = 30.0f;
    public float fadeDuration = 2.0f;

    public float SpawnChance = 0.9f;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void SpawnDecal(Vector3 hitPoint, Vector3 hitNormal, Vector3 velocity)
    {
        if (decalPrefab == null || decalMaterials == null || decalMaterials.Length == 0) return;
        if (UnityEngine.Random.value > SpawnChance) return;

        Quaternion rotation;

        // 충돌 표면 평면에 파티클의 속도 벡터를 투영하여 실질적인 흘러가는 방향을 구함
        Vector3 projectedVelocity = Vector3.ProjectOnPlane(velocity, hitNormal);

        // 속도가 거의 0이 아닌 경우에만 방향 적용 (정지 상태거나 수직 충돌 시 대비)
        if (projectedVelocity.sqrMagnitude > 0.001f)
        {
            // Decal Projector의 Z축(투사방향)은 -hitNormal, Y축(위쪽)은 projectedVelocity로 정렬
            rotation = Quaternion.LookRotation(-hitNormal, projectedVelocity);
        }
        else
        {
            // 방향을 구할 수 없을 때는 기존처럼 랜덤 회전
            rotation = Quaternion.LookRotation(-hitNormal) * Quaternion.Euler(0, 0, Random.Range(0f, 360f));
        }

        GameObject decal = Instantiate(decalPrefab, hitPoint, rotation, transform);

        DecalProjector projector = decal.GetComponent<DecalProjector>();
        if (projector != null)
        {
            float randomWidth = Random.Range(widthRange.x, widthRange.y);
            float randomHeight = Random.Range(heightRange.x, heightRange.y);
            projector.size = new Vector3(randomWidth, randomHeight, depth);

            Material selectedMat = decalMaterials[Random.Range(0, decalMaterials.Length)];
            projector.material = new Material(selectedMat);

            StartCoroutine(FadeAndDestroyRoutine(decal, projector.material));
        }
        else
        {
            Destroy(decal);
        }
    }

    private IEnumerator FadeAndDestroyRoutine(GameObject decal, Material mat)
    {
        yield return new WaitForSeconds(lifeTime);

        float time = 0f;
        float startAlpha = 1f;

        if (mat.HasProperty("_Alpha"))
        {
            startAlpha = mat.GetFloat("_Alpha");
        }

        while (time < fadeDuration)
        {
            time += Time.deltaTime;
            float alpha = Mathf.Lerp(startAlpha, 0f, time / fadeDuration);
            mat.SetFloat("_Alpha", alpha);
            yield return null;
        }

        Destroy(mat);
        Destroy(decal);
    }
}