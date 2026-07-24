using UnityEngine;

public class Stage1Environment : EnvironmentController
{
    private float lastValue = -1f;

    private void Update()
    {
        float value = GetBoundaryWeight(Player.Instance.transform.position);
        if (lastValue == value)
        {
            return;
        }

        float fog = Mathf.Lerp(0f, 55f / 255f, value);
        Color skyColor = Color.white * (1 - value * 128 / 255f);

        Fog.color = new Color(Fog.color.r, Fog.color.g, Fog.color.b, fog);

        Shader.SetGlobalColor("_SkyColor", skyColor);
        Shader.SetGlobalFloat("_FogAlpha", value * 2f);
        lastValue = value;
    }
}