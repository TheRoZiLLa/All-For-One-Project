using UnityEngine;

/// <summary>
/// Pulses the reactor core between cyber blue and warning red.
/// </summary>
public class CoreEnergyPulse : MonoBehaviour
{
    public Light coreLight;
    public Light redLight;
    public Renderer[] targets;
    public float speed = 1.5f;

    static readonly Color Cyan = new Color(0f, 2.4f, 4.2f);
    static readonly Color Red = new Color(4.2f, 0.25f, 0.18f);

    void Update()
    {
        float beat = Mathf.Sin(Time.time * speed) * 0.5f + 0.5f;
        float mix = Mathf.PingPong(Time.time * 0.22f, 1f);
        Color emission = Color.Lerp(Cyan, Red, mix) * Mathf.Lerp(0.75f, 1.35f, beat);

        if (coreLight != null)
            coreLight.intensity = Mathf.Lerp(5f, 18f, beat);
        if (redLight != null)
            redLight.intensity = Mathf.Lerp(1.2f, 7f, 1f - beat);

        if (targets == null)
            return;

        var block = new MaterialPropertyBlock();
        for (int i = 0; i < targets.Length; i++)
        {
            Renderer renderer = targets[i];
            if (renderer == null)
                continue;
            renderer.GetPropertyBlock(block);
            block.SetColor("_EmissionColor", emission);
            renderer.SetPropertyBlock(block);
        }
    }
}
