using UnityEngine;

public enum ScreenPosition
{
    UpperLeft, UpperCenter, UpperRight,
    MiddleLeft, Center, MiddleRight,
    LowerLeft, LowerCenter, LowerRight,
    // 추가된 라인 옵션
    Top, Middle, Bottom
}

[CreateAssetMenu(fileName = "NewHitEffect", menuName = "HitInkEffectData")]
public class HitEffectData : ScriptableObject
{
    public ScreenPosition startPosition;
    public ScreenPosition endPosition;

    [Header("Spawn Settings")]
    public int minCount = 5;
    public int maxCount = 10;
    public float minScale = 1.0f;
    public float maxScale = 2.0f;
    public float minSpeed = 0.1f;
    public float maxSpeed = 0.3f;
    public int lineSteps = 3;
    [Header("Timing Settings")]
    public float growthDuration = 0.2f;
    public float stayDuration = 0.3f;
    public float fadeDuration = 0.3f;

    public Vector2 GetRandomPosInZone(ScreenPosition pos, RectTransform canvasRect)
    {
        float w = canvasRect.rect.width / 2f;
        float h = canvasRect.rect.height / 2f;
        float xRange = canvasRect.rect.width * 0.2f;
        float yRange = canvasRect.rect.height * 0.2f;

        switch (pos)
        {
            case ScreenPosition.UpperLeft: return new Vector2(-w, h);
            case ScreenPosition.UpperCenter: return new Vector2(Random.Range(-xRange, xRange), h);
            case ScreenPosition.UpperRight: return new Vector2(w, h);
            case ScreenPosition.MiddleLeft: return new Vector2(-w, Random.Range(-yRange, yRange));
            case ScreenPosition.Center: return new Vector2(Random.Range(-xRange, xRange), Random.Range(-yRange, yRange));
            case ScreenPosition.MiddleRight: return new Vector2(w, Random.Range(-yRange, yRange));
            case ScreenPosition.LowerLeft: return new Vector2(-w, -h);
            case ScreenPosition.LowerCenter: return new Vector2(Random.Range(-xRange, xRange), -h);
            case ScreenPosition.LowerRight: return new Vector2(w, -h);

            // 라인 옵션: 가로축 전체(-w ~ w)를 사용
            case ScreenPosition.Top: return new Vector2(Random.Range(-w, w), h);
            case ScreenPosition.Middle: return new Vector2(Random.Range(-w, w), Random.Range(-yRange, yRange));
            case ScreenPosition.Bottom: return new Vector2(Random.Range(-w, w), -h);

            default: return Vector2.zero;
        }
    }
}