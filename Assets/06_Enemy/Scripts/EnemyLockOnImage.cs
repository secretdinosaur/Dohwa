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

    void Awake()
    {
        Instance = this;
        if (Camera.main != null)
            mainCamera = Camera.main;
        imageRectTransform = LockOnImage.GetComponent<RectTransform>();
        imageRectTransform.gameObject.SetActive(false);
    }

    public void SetTarget(Transform newTarget)
    {
        if(newTarget == null)
        {
            target = null;
            imageRectTransform.gameObject.SetActive(false);
            StopCoroutine(LockOnCoroutine);
        }
        else
        {
            target = newTarget.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "DEF-spine.003");
            imageRectTransform.gameObject.SetActive(true);
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
}
