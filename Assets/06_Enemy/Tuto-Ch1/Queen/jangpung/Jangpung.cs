using System.Collections;
using UnityEngine;

public class Jangpung : Projectile
{
    #region Settings
    [Header("Scale Settings")]
    [SerializeField] private AnimationCurve zScaleCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    [SerializeField] private float maxZScale = 5f;
    [SerializeField] private float duration = 1f;

    [Header("Timing Settings")]
    [SerializeField] private float hitDestroyDelay = 0.1f;
    #endregion

    #region Internal Variables
    private Coroutine scaleCoroutine;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        PlayScaleAnimation();
    }
    #endregion

    #region Scale Animation
    public void PlayScaleAnimation()
    {
        if (scaleCoroutine != null)
        {
            StopCoroutine(scaleCoroutine);
        }
        scaleCoroutine = StartCoroutine(ScaleZRoutine());
    }

    private IEnumerator ScaleZRoutine()
    {
        Vector3 startScale = transform.localScale;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / duration);
            float curveMultiplier = zScaleCurve.Evaluate(t);
            float newZ = curveMultiplier * maxZScale;

            transform.localScale = new Vector3(startScale.x, startScale.y, newZ);
            yield return null;
        }

        float finalCurveValue = zScaleCurve.Evaluate(1f);
        transform.localScale = new Vector3(startScale.x, startScale.y, finalCurveValue * maxZScale);

        scaleCoroutine = null;
    }
    #endregion

    #region Projectile Overrides
    public override void DestroyThis()
    {
        StartCoroutine(DelayedDestroyRoutine());
    }

    private IEnumerator DelayedDestroyRoutine()
    {
        yield return new WaitForSeconds(hitDestroyDelay);
        base.DestroyThis();
    }
    #endregion
}