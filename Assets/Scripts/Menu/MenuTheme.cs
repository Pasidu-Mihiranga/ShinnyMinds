using UnityEngine;

namespace ShinyMinds.Menu
{
    /// <summary>
    /// Colours, sizes and generated sprites for the main menu.
    ///
    /// The menu is built from code rather than a prefab, so the values that would
    /// normally live in the Inspector live here instead - one place to adjust, and
    /// no scene file to merge.
    /// </summary>
    public static class MenuTheme
    {
        // Sampled to sit against the school background: a deep blue that stays legible
        // over both the pale centre and the sky.
        public static readonly Color Ink = new Color(0.11f, 0.19f, 0.33f, 1f);
        public static readonly Color InkMuted = new Color(0.36f, 0.44f, 0.55f, 1f);
        public static readonly Color Panel = new Color(1f, 1f, 1f, 0.92f);
        public static readonly Color PanelSoft = new Color(0.96f, 0.97f, 1f, 0.95f);

        public static readonly Color Primary = new Color(0.13f, 0.63f, 0.55f, 1f);
        public static readonly Color PrimaryHover = new Color(0.10f, 0.55f, 0.48f, 1f);
        public static readonly Color Secondary = new Color(0.35f, 0.42f, 0.85f, 1f);
        public static readonly Color Danger = new Color(0.85f, 0.30f, 0.31f, 1f);
        public static readonly Color Disabled = new Color(0.75f, 0.78f, 0.82f, 1f);
        public static readonly Color Field = new Color(1f, 1f, 1f, 1f);
        public static readonly Color FieldBorder = new Color(0.80f, 0.84f, 0.89f, 1f);
        public static readonly Color ErrorText = new Color(0.75f, 0.18f, 0.20f, 1f);
        public static readonly Color SuccessText = new Color(0.10f, 0.50f, 0.30f, 1f);

        /// <summary>Design resolution the canvas scales from.</summary>
        public static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

        /// <summary>
        /// Width of the centre column. The background art places characters and the
        /// school gate at the left and right edges and leaves the middle clear, so the
        /// UI is kept narrow enough not to cover them.
        /// </summary>
        public const float ColumnWidth = 620f;

        public const int TitleFontSize = 64;
        public const int HeadingFontSize = 34;
        public const int BodyFontSize = 24;
        public const int ButtonFontSize = 28;
        public const int SmallFontSize = 20;

        public const float ButtonHeight = 68f;
        public const float FieldHeight = 62f;
        public const float CornerRadius = 18f;

        private static Sprite _roundedSprite;

        /// <summary>
        /// A white rounded-rectangle sprite, generated once and 9-sliced so it stays
        /// crisp at any size. Generating it avoids shipping UI art for a shape this simple.
        /// </summary>
        public static Sprite RoundedRect
        {
            get
            {
                if (_roundedSprite == null)
                {
                    _roundedSprite = BuildRoundedSprite(64, 18);
                }

                return _roundedSprite;
            }
        }

        private static Sprite BuildRoundedSprite(int size, int radius)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "ShinyMinds.RoundedRect",
            };

            Color[] pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    pixels[y * size + x] = new Color(1f, 1f, 1f, CornerAlpha(x, y, size, radius));
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();

            return Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(radius, radius, radius, radius));
        }

        /// <summary>
        /// Alpha for one pixel, antialiased across the corner arc so edges are smooth
        /// rather than stair-stepped.
        /// </summary>
        private static float CornerAlpha(int x, int y, int size, int radius)
        {
            float cx = x < radius ? radius : (x >= size - radius ? size - radius - 1 : x);
            float cy = y < radius ? radius : (y >= size - radius ? size - radius - 1 : y);

            float distance = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));

            return Mathf.Clamp01(radius - distance + 0.5f);
        }
    }
}
