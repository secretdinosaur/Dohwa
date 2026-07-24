using UnityEngine;
using UnityEngine.UI;

public class HpBarCanvas : MonoBehaviour
{
    private Transform cam;
    [SerializeField] private Image hpBarImage;
    [SerializeField] private Image mpBarImage;

    void Awake()
    {
        if (Camera.main != null)
            cam = Camera.main.transform;

        if (hpBarImage != null)
            hpBarImage.material = Instantiate(hpBarImage.material);

        if (mpBarImage != null)
            mpBarImage.material = Instantiate(mpBarImage.material);

        hpBarImage
            .material.SetFloat("_FillAmount", 1f);
        mpBarImage
            .material.SetFloat("_FillAmount", 1f);
    }

    void LateUpdate()
    {
        if (cam == null) return;
        transform.LookAt(transform.position - cam.forward);
    }

    public void UpdateHP(float current, float max)
    {
            hpBarImage.material.SetFloat("_FillAmount", current / max);
    }

    public void UpdateMP(float current, float max)
    {
            mpBarImage.material.SetFloat("_FillAmount", current / max);
    }

    void OnDestroy()
    {
        // 복제된 Material의 메모리 누수 방지
        /*if (hpBarImage != null && hpBarImage.material != null)
            Destroy(hpBarImage.material);

        if (mpBarImage != null && mpBarImage.material != null)
            Destroy(mpBarImage.material);*/
    }
}