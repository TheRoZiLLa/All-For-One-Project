using UnityEngine;

namespace Recorded.ColorSystem
{
    /// <summary>
    /// Layer gate for Spectrum shots. Structural meshes stay on Default and block the ray.
    /// Only this layer receives a color payload.
    /// </summary>
    public static class ColorInteractable
    {
        public const string LayerName = "ColorInteractable";

        public static int Layer => LayerMask.NameToLayer(LayerName);

        public static bool Is(Collider collider)
        {
            int layer = Layer;
            return layer >= 0 && collider != null && collider.gameObject.layer == layer;
        }
    }
}
