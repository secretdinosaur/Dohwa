using UnityEngine;

public class EnvironmentController : MonoBehaviour
{
    public BoxCollider innerBox;
    public BoxCollider outerBox;

    public Material Fog;
    public Material Sobel;
    public Material SmokeFilter;
    private Vector3 innerCenter;
    private Vector3 outerCenter;
    private Vector3 innerExtents;
    private Vector3 outerExtents;
    private Vector3 innerScale;
    private Vector3 outerScale;

    public float weight = 1;

    private void Awake()
    {
        innerCenter = innerBox.center;
        outerCenter = outerBox.center;
        innerExtents = innerBox.size * 0.5f;
        outerExtents = outerBox.size * 0.5f;
        innerScale = innerBox.transform.lossyScale;
        outerScale = outerBox.transform.lossyScale;
    }

    public float GetBoundaryWeight(Vector3 targetPosition)
    {
        if (InventoryManager.Instance != null && InventoryManager.Instance.isInventoryOpen)
        {
            return 0f;
        }

        if (innerBox == null || outerBox == null) return 0f;

        Vector3 localPos = innerBox.transform.InverseTransformPoint(targetPosition);

        Vector3 innerExtents = innerBox.size * 0.5f;
        Vector3 outerExtents = outerBox.size * 0.5f;

        Vector3 innerCenter = innerBox.center;
        Vector3 outerCenter = outerBox.center;

        float weightX = GetAxisWeight(localPos.x, innerCenter.x, innerExtents.x, outerCenter.x, outerExtents.x);
        float weightY = GetAxisWeight(localPos.y, innerCenter.y, innerExtents.y, outerCenter.y, outerExtents.y);
        float weightZ = GetAxisWeight(localPos.z, innerCenter.z, innerExtents.z, outerCenter.z, outerExtents.z);

        float finalWeight = Mathf.Min(weightX, Mathf.Min(weightY, weightZ));

        return Mathf.SmoothStep(0f, 1f, finalWeight);
    }

    private float GetAxisWeight(float pos, float centerIn, float extentIn, float centerOut, float extentOut)
    {
        float innerMin = centerIn - extentIn;
        float innerMax = centerIn + extentIn;
        float outerMin = centerOut - extentOut;
        float outerMax = centerOut + extentOut;

        if (pos >= innerMin && pos <= innerMax) return 1f;
        if (pos <= outerMin || pos >= outerMax) return 0f;

        if (pos > innerMax)
        {
            return 1f - ((pos - innerMax) / (outerMax - innerMax));
        }
        else
        {
            return (pos - outerMin) / (innerMin - outerMin);
        }
    }

    private void OnDisable()
    {
        Fog.color = new Color(Fog.color.r, Fog.color.g, Fog.color.b, 0f);
        Shader.SetGlobalColor("_SkyColor", Color.white);
        Shader.SetGlobalFloat("_FogAlpha", 0f);
    }
}