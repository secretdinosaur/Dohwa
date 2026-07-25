using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;

public class EnemyLockOnImage : MonoBehaviour
{
    public Transform target;
    private Transform cam;
    private Camera mainCamera;
    [SerializeField] private Image LockOnImage;
    private RectTransform imageRectTransform;

    void Awake()
    {
        if (Camera.main != null)
            cam = Camera.main.transform;
        imageRectTransform = LockOnImage.GetComponent<RectTransform>();
        imageRectTransform.gameObject.SetActive(false);
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "DEF-spine.003");
        imageRectTransform.gameObject.SetActive(target != null);
    }

    void LateUpdate()
    {
        if (target == null)
            return;

        Vector3 screenPos = mainCamera.WorldToScreenPoint(target.position);

        if (screenPos.z < 0)
        {
            imageRectTransform.gameObject.SetActive(false);
            return;
        }

        imageRectTransform.gameObject.SetActive(true);
        imageRectTransform.position = screenPos;
    }
}
