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

    public float fadeInDuration = 0.5f;
    public float lifeTime = 30.0f;
    public float fadeOutDuration = 2.0f;

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
        Vector3 projectedVelocity = Vector3.ProjectOnPlane(velocity, hitNormal);

        if (projectedVelocity.sqrMagnitude > 0.001f)
        {
            rotation = Quaternion.LookRotation(-hitNormal, projectedVelocity);
        }
        else
        {
            rotation = Quaternion.LookRotation(-hitNormal) * Quaternion.Euler(0, 0, Random.Range(0f, 360f));
        }

        GameObject decal = Instantiate(decalPrefab, hitPoint, rotation, transform);

        DecalProjector projector = decal.GetComponent<DecalProjector>();
        if (projector != null)
        {
            float dot = Mathf.Abs(Vector3.Dot(velocity.normalized, hitNormal));
            float calculatedHeight = Mathf.Lerp(heightRange.y, heightRange.x, dot);
            float randomWidth = Random.Range(widthRange.x, widthRange.y);

            projector.size = new Vector3(randomWidth, calculatedHeight, depth);

            Material selectedMat = decalMaterials[Random.Range(0, decalMaterials.Length)];
            projector.material = new Material(selectedMat);

            StartCoroutine(FadeRoutine(decal, projector.material));
        }
        else
        {
            Destroy(decal);
        }
    }

    public void ClearAllDecals()
    {
        StopAllCoroutines();

        foreach (Transform child in transform)
        {
            DecalProjector projector = child.GetComponent<DecalProjector>();
            if (projector != null && projector.material != null)
            {
                Destroy(projector.material);
            }
            Destroy(child.gameObject);
        }
    }

    private IEnumerator FadeRoutine(GameObject decal, Material mat)
    {
        float time = 0f;
        float targetAlpha = 1f;

        if (mat.HasProperty("_Alpha"))
        {
            targetAlpha = mat.GetFloat("_Alpha");
            mat.SetFloat("_Alpha", 0f);
        }

        while (time < fadeInDuration)
        {
            time += Time.deltaTime;
            float alpha = Mathf.Lerp(0f, targetAlpha, time / fadeInDuration);
            mat.SetFloat("_Alpha", alpha);
            yield return null;
        }

        mat.SetFloat("_Alpha", targetAlpha);

        yield return new WaitForSeconds(lifeTime);

        time = 0f;

        while (time < fadeOutDuration)
        {
            time += Time.deltaTime;
            float alpha = Mathf.Lerp(targetAlpha, 0f, time / fadeOutDuration);
            mat.SetFloat("_Alpha", alpha);
            yield return null;
        }

        Destroy(mat);
        if (decal != null)
        {
            Destroy(decal);
        }
    }
}