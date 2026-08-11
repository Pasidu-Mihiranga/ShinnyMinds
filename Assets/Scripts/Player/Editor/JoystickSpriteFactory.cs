using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ShinyMinds.PlayerTools
{
    /// <summary>
    /// Generates the pieces the touch stick is drawn from: two ring weights, a soft bloom
    /// and a chevron.
    ///
    /// The Joystick Pack's own sprites are flat cartoon discs with baked-in arrows and a
    /// baked-in colour, so restyling them means editing PNGs by hand and re-editing them
    /// the next time the palette moves. These are drawn from a few numbers instead: the
    /// ring weight, the bloom's falloff and the chevron's stroke are all parameters, and
    /// every piece is tinted at the Image so one sprite serves the red rim and the hairline
    /// circles alike.
    ///
    /// Same trick as MissionUIBuilder's ellipse: generate large, feather by a texel, and the
    /// edge stays crisp at whatever size the control is set to.
    /// </summary>
    public static class JoystickSpriteFactory
    {
        const string Folder = "Assets/Art/UI";
        const int Size = 512;
        const int ChevronSize = 128;

        /// <summary>The bright rim. Thickness is a fraction of the radius.</summary>
        public static Sprite RimRing() => Ring("rim", 0.11f);

        /// <summary>The hairline circles inside it.</summary>
        public static Sprite HairRing() => Ring("hair", 0.022f);

        /// <summary>
        /// An annulus with both rims feathered over one texel. Drawn to the very edge of the
        /// texture, so an Image sized to the base circle puts the ring exactly on its rim.
        /// </summary>
        public static Sprite Ring(string name, float thickness)
        {
            return Ensure($"{Folder}/UI_Ring_{name}.png", () => Draw(Size, (x, y) =>
            {
                float radius = Size * 0.5f;
                float d = Distance(x, y, radius) / radius;

                float mid = 1f - thickness * 0.5f;
                float feather = 1f / radius;

                return Mathf.Clamp01((thickness * 0.5f - Mathf.Abs(d - mid)) / feather);
            }));
        }

        /// <summary>
        /// The bloom behind the rim: a Gaussian ring peaking at <see cref="GlowPeak"/> of the
        /// radius, so an Image sized 1/GlowPeak of the base circle lands the brightest part of
        /// the bloom on the rim itself and lets the rest spill outwards.
        /// </summary>
        public const float GlowPeak = 0.72f;

        public static Sprite Glow()
        {
            return Ensure($"{Folder}/UI_Glow.png", () => Draw(Size, (x, y) =>
            {
                float radius = Size * 0.5f;
                float d = Distance(x, y, radius) / radius;

                const float sigma = 0.115f;
                float t = (d - GlowPeak) / sigma;
                float alpha = Mathf.Exp(-0.5f * t * t);

                // The tail is faint by the texture's edge but not zero, and a bloom that ends
                // in a visible circle is worse than no bloom.
                return alpha * Mathf.Clamp01((1f - d) / 0.08f);
            }));
        }

        /// <summary>
        /// A chevron pointing right, to be rotated into the other three. Two strokes meeting at
        /// a point, so the join and the caps come out round for free.
        /// </summary>
        public static Sprite Chevron()
        {
            return Ensure($"{Folder}/UI_Chevron.png", () => Draw(ChevronSize, (x, y) =>
            {
                float u = (x + 0.5f) / ChevronSize;
                float v = (y + 0.5f) / ChevronSize;

                var p = new Vector2(u, v);
                var top = new Vector2(0.30f, 0.90f);
                var tip = new Vector2(0.78f, 0.50f);
                var bottom = new Vector2(0.30f, 0.10f);

                float d = Mathf.Min(SegmentDistance(p, top, tip), SegmentDistance(p, tip, bottom));

                const float halfStroke = 0.070f;
                const float feather = 0.013f;

                return Mathf.Clamp01((halfStroke - d) / feather);
            }));
        }

        // ------------------------------------------------------------------ drawing

        static float Distance(int x, int y, float radius)
        {
            float dx = x + 0.5f - radius;
            float dy = y + 0.5f - radius;
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float lengthSq = ab.sqrMagnitude;

            float t = lengthSq > 1e-6f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / lengthSq) : 0f;

            return Vector2.Distance(p, a + ab * t);
        }

        /// <summary>White everywhere; the shape lives entirely in the alpha, so it can be tinted.</summary>
        static byte[] Draw(int size, Func<int, int, float> alphaAt)
        {
            var pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float a = Mathf.Clamp01(alphaAt(x, y));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.SetPixels32(pixels);
            tex.Apply();

            byte[] png = tex.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(tex);

            return png;
        }

        // ------------------------------------------------------------------ assets

        static Sprite Ensure(string path, Func<byte[]> encode)
        {
            if (!File.Exists(path))
            {
                EnsureFolder(path);
                File.WriteAllBytes(path, encode());
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                Debug.Log($"Generated {path}");
            }

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                importer = AssetImporter.GetAtPath(path) as TextureImporter;
            }

            // This project's import default is Multiple, which slices a texture into named
            // sub-sprites and, with no slices defined, yields no Sprite at all — the control
            // then renders as a plain white box. Set it explicitly, and only when it is wrong,
            // or SaveAndReimport loops.
            if (importer != null &&
                (importer.textureType != TextureImporterType.Sprite ||
                 importer.spriteImportMode != SpriteImportMode.Single ||
                 !importer.alphaIsTransparency))
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);

            if (sprite == null)
                Debug.LogError($"{path} did not import as a Sprite. That piece of the stick " +
                               "will draw as a white box. Check the texture's Import Settings: " +
                               "Texture Type = Sprite, Sprite Mode = Single.");

            return sprite;
        }

        static void EnsureFolder(string assetPath)
        {
            string dir = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');

            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
                AssetDatabase.Refresh();
            }
        }
    }
}
