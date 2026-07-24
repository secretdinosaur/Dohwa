using UnityEngine;

public class PeakEnvironment : EnvironmentController
{
    private float lastValue = -1f;

    private void Update()
    {
        float value = GetBoundaryWeight(Player.Instance.transform.position);
        if (lastValue == value)
        {
            return;
        }
        value = Mathf.Clamp01(value * weight);
        Shader.SetGlobalFloat("_MaxHeight", Mathf.Lerp(100, 50, value));
        Shader.SetGlobalFloat("_MinHeight", Mathf.Lerp(0, -75, value));
        lastValue = value;
    }
}