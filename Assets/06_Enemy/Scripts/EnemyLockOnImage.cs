using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using System.Collections;

public class EnemyLockOnImage : MonoBehaviour
{
    public static EnemyLockOnImage Instance;
    public Transform target;
    private Camera mainCamera;

    [SerializeField] private Image LockOnImage;
    private RectTransform imageRectTransform;

    private Coroutine LockOnCoroutine;
    private Sequence attackSeq;

    [Header("Strong Attack Effect")]
    public Material strongAttackMaterial;
    public float expandDuration = 0.15f;  // 처음 커지고, 마지막에 작아지는 데 걸리는 시간
    public float holdDuration = 1.0f;     // 유지(두근거림) 시간

    [Header("Pulse Settings (유지 중 효과)")]
    public float pulseScale = 1.65f;      // 유지 중 최대 팽창 크기 (기본 1.5 -> 1.65 왔다갔다)
    public float pulseSpeed = 0.05f;      // 팽창/수축 1회 반복 속도

    void Awake()
    {
        Instance = this;
        if (Camera.main != null)
            mainCamera = Camera.main;

        imageRectTransform = LockOnImage.GetComponent<RectTransform>();
        imageRectTransform.gameObject.SetActive(false);

        if (strongAttackMaterial != null)
        {
            strongAttackMaterial.SetFloat("_Edge", 0f);
        }
    }

    public void SetTarget(Transform newTarget)
    {
        if (newTarget == null)
        {
            target = null;
            imageRectTransform.gameObject.SetActive(false);

            if (LockOnCoroutine != null) StopCoroutine(LockOnCoroutine);

            if (attackSeq != null) attackSeq.Kill();

            imageRectTransform.localScale = Vector3.one;
            if (strongAttackMaterial != null) strongAttackMaterial.SetFloat("_Edge", 0f);
        }
        else
        {
            target = newTarget.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "DEF-spine.003");
            imageRectTransform.gameObject.SetActive(true);

            if (LockOnCoroutine != null) StopCoroutine(LockOnCoroutine);
            LockOnCoroutine = StartCoroutine(FollowTarget());
        }
    }

    IEnumerator FollowTarget()
    {
        while (true)
        {
            yield return new WaitForEndOfFrame();

            if (target == null)
                continue;

            imageRectTransform.position = mainCamera.WorldToScreenPoint(target.position);
        }
    }

    public void TriggerStrongAttackEffect()
    {
        if (target == null || !imageRectTransform.gameObject.activeInHierarchy) return;

        if (attackSeq != null) attackSeq.Kill();

        // SetUpdate(true)를 설정하여 타임스케일(역경직 등)을 무시하고 강제 재생시킴
        attackSeq = DOTween.Sequence().SetUpdate(true);

        // 1. 시작 시간(expandDuration)만큼 1.5배로 커짐
        attackSeq.Append(imageRectTransform.DOScale(2f, expandDuration).SetEase(Ease.OutQuad));
        if (strongAttackMaterial != null)
        {
            attackSeq.Join(strongAttackMaterial.DOFloat(0.2f, "_Edge", expandDuration).SetEase(Ease.OutQuad));
        }

        // 2. 유지 시간(holdDuration) 동안 빠르게 요요(Yoyo) 반복 (두근거림 효과)
        // 횟수가 홀수면 크기가 커진 채로 끝나버리므로, 항상 짝수가 되도록 보정하여 1.5배 크기로 돌아오게 맞춤
        int loops = Mathf.Max(2, Mathf.RoundToInt(holdDuration / pulseSpeed));
        if (loops % 2 != 0) loops++;

        attackSeq.Append(imageRectTransform.DOScale(pulseScale, pulseSpeed)
            .SetLoops(loops, LoopType.Yoyo)
            .SetEase(Ease.InOutSine));

        // 3. 시작 시간(expandDuration)만큼 다시 1배로 작아짐
        attackSeq.Append(imageRectTransform.DOScale(1f, expandDuration).SetEase(Ease.OutQuad));
        if (strongAttackMaterial != null)
        {
            attackSeq.Join(strongAttackMaterial.DOFloat(0f, "_Edge", expandDuration).SetEase(Ease.OutQuad));
        }
    }

    public void CancelStrongAttackEffect()
    {
        if (target == null || !imageRectTransform.gameObject.activeInHierarchy) return;

        if (attackSeq != null) attackSeq.Kill();

        attackSeq = DOTween.Sequence().SetUpdate(true);

        // 취소 시 현재 크기에서 시작 시간(expandDuration)만큼 빠르게 1배로 원상복구
        attackSeq.Append(imageRectTransform.DOScale(1f, expandDuration).SetEase(Ease.OutQuad));
        if (strongAttackMaterial != null)
        {
            attackSeq.Join(strongAttackMaterial.DOFloat(0f, "_Edge", expandDuration).SetEase(Ease.OutQuad));
        }
    }
}