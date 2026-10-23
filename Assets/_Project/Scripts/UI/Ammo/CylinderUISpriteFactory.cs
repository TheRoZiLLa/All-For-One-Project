using UnityEngine;

namespace Recorded.Weapons.Ammo
{
    /// <summary>
    /// Generates high-quality anti-aliased procedural sprites in memory for the Cylinder Ammo HUD.
    /// Provides smooth circular discs, metallic rims, and directional pointer notches.
    /// </summary>
    public static class CylinderUISpriteFactory
    {
        private static Sprite _circleFilled;
        private static Sprite _circleRing;
        private static Sprite _indicatorNotch;

        public static Sprite CircleFilled
        {
            get
            {
                if (_circleFilled == null) _circleFilled = CreateCircleFilled(128);
                return _circleFilled;
            }
        }

        public static Sprite CircleRing
        {
            get
            {
                if (_circleRing == null) _circleRing = CreateCircleRing(128, 0.14f);
                return _circleRing;
            }
        }

        public static Sprite IndicatorNotch
        {
            get
            {
                if (_indicatorNotch == null) _indicatorNotch = CreateIndicatorNotch(64, 64);
                return _indicatorNotch;
            }
        }

        private static Sprite CreateCircleFilled(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            float center = (size - 1) * 0.5f;
            float radius = center - 1.5f;

            Color[] colors = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    // 1.5 pixel smooth anti-aliased edge
                    float alpha = Mathf.Clamp01((radius - dist) + 0.5f);
                    colors[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels(colors);
            tex.Apply();

            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite CreateCircleRing(int size, float thicknessRatio)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            float center = (size - 1) * 0.5f;
            float outerRadius = center - 1.5f;
            float innerRadius = outerRadius * (1f - thicknessRatio);

            Color[] colors = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    float outerAlpha = Mathf.Clamp01((outerRadius - dist) + 0.5f);
                    float innerAlpha = Mathf.Clamp01((dist - innerRadius) + 0.5f);
                    float alpha = Mathf.Min(outerAlpha, innerAlpha);

                    colors[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels(colors);
            tex.Apply();

            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite CreateIndicatorNotch(int width, int height)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            Color[] colors = new Color[width * height];

            // Upward pointing triangle/chevron notch
            for (int y = 0; y < height; y++)
            {
                float progressY = (float)y / height; // 0 at base, 1 at tip
                float halfWidthAtY = (width * 0.5f) * (1f - progressY);

                for (int x = 0; x < width; x++)
                {
                    float distFromCenter = Mathf.Abs(x - (width * 0.5f));
                    float alpha = Mathf.Clamp01((halfWidthAtY - distFromCenter) + 0.5f);
                    colors[y * width + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels(colors);
            tex.Apply();

            return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
