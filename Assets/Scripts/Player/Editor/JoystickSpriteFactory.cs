using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ShinyMinds.PlayerTools
{
    /// <summary>
    /// Generates the pieces the touch stick is drawn from: two ring weights, a chevron, the
    /// frost and the two button glyphs.
    ///
    /// The Joystick Pack's own sprites are flat cartoon discs with baked-in arrows and a
    /// baked-in colour, so restyling them means editing PNGs by hand and re-editing them
    /// the next time the palette moves. These are drawn from a few numbers instead: the ring
    /// weight, the frost's noise and the stroke widths are all parameters, and every piece is
    /// tinted at the Image so one sprite serves the bezel and the hairline circles alike.
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

        // There was a Glow() here too: a Gaussian ring that spilled outside the base circle,
        // used first as a red bloom and then as a dark halo. Removed by request — anything with
        // a soft edge outside the control reads as a glow around it, whatever colour it is.
        // Assets/Art/UI/UI_Glow.png and UI_Sheen.png may still be in the project from earlier
        // builds; nothing reads either of them.

        /// <summary>
        /// The frost: two octaves of value noise inside a clean circular edge.
        ///
        /// Frosted glass is not a flat wash — it is ground glass, so the haze mottles. The
        /// coarse octave gives that mottling and the fine one the grain. The floor of 0.45
        /// keeps the haze continuous: noise allowed to reach zero would leave clear patches,
        /// and the disc would read as dirty rather than frosted.
        ///
        /// The shape lives in the alpha, so the caller decides whether the haze darkens or
        /// lightens the glass. MobileControlsBuilder tints it black: a light haze turns into
        /// visible polish as soon as the control sits over something pale.
        /// </summary>
        public static Sprite Frost()
        {
            return Ensure($"{Folder}/UI_Frost.png", () => Draw(Size, (x, y) =>
            {
                float radius = Size * 0.5f;
                float d = Distance(x, y, radius) / radius;

                // The haze must stop exactly where the glass does.
                float mask = Mathf.Clamp01((1f - d) * radius);

                float noise = 0.62f * Noise(x, y, 22f) + 0.38f * Noise(x, y, 74f);

                return mask * (0.45f + 0.55f * noise);
            }));
        }

        // There was a Sheen() here: an off-centre radial highlight, tinted to a few percent
        // white, that made the glass look polished. Removed by request — the control is matte
        // ground glass, and the frost above is the only light on it. Assets/Art/UI/UI_Sheen.png
        // may still be sitting in the project from an earlier build; nothing reads it.

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

        // ------------------------------------------------------------------ button icons

        const int IconSize = 128;

        // Slightly finer than the axis chevrons: these glyphs have more strokes in the same
        // space, and at the stick's weight the Jump arrow closes up into a blob.
        const float IconStroke = 0.058f;

        /// <summary>
        /// Jump: an arrow springing up off the ground it leaves. Drawn as line art in the same
        /// language as the axis chevrons, so the stick and the buttons read as one instrument.
        /// </summary>
        public static Sprite JumpIcon()
        {
            return Ensure($"{Folder}/UI_Icon_Jump.png", () => Draw(IconSize, (x, y) =>
            {
                Vector2 p = Unit(x, y, IconSize);

                // Stem, an open head, then the baseline.
                float d = SegmentDistance(p, new Vector2(0.50f, 0.34f), new Vector2(0.50f, 0.87f));
                d = Mathf.Min(d, SegmentDistance(p, new Vector2(0.28f, 0.64f), new Vector2(0.50f, 0.89f)));
                d = Mathf.Min(d, SegmentDistance(p, new Vector2(0.72f, 0.64f), new Vector2(0.50f, 0.89f)));
                d = Mathf.Min(d, SegmentDistance(p, new Vector2(0.24f, 0.13f), new Vector2(0.76f, 0.13f)));

                return Stroke(d);
            }));
        }

        /// <summary>
        /// Run: the double chevron games have used for sprint long enough that it needs no
        /// caption, which is the entire point of dropping the caption.
        /// </summary>
        public static Sprite RunIcon()
        {
            return Ensure($"{Folder}/UI_Icon_Run.png", () => Draw(IconSize, (x, y) =>
            {
                Vector2 p = Unit(x, y, IconSize);

                float d = Mathf.Min(ChevronDistance(p, 0.24f), ChevronDistance(p, 0.54f));

                return Stroke(d);
            }));
        }

        /// <summary>Distance to a chevron whose back edge stands at <paramref name="x"/>.</summary>
        static float ChevronDistance(Vector2 p, float x)
        {
            var top = new Vector2(x, 0.76f);
            var tip = new Vector2(x + 0.22f, 0.50f);
            var bottom = new Vector2(x, 0.24f);

            return Mathf.Min(SegmentDistance(p, top, tip), SegmentDistance(p, tip, bottom));
        }

        static Vector2 Unit(int x, int y, int size) => new Vector2((x + 0.5f) / size, (y + 0.5f) / size);

        static float Stroke(float distance) => Mathf.Clamp01((IconStroke - distance) / 0.013f);

        // ------------------------------------------------------------------ drawing

        static float Distance(int x, int y, float radius)
        {
            float dx = x + 0.5f - radius;
            float dy = y + 0.5f - radius;
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// Value noise on a lattice of <paramref name="cells"/> across the texture, smoothed so
        /// the mottling has soft edges instead of visible squares.
        /// </summary>
        static float Noise(int x, int y, float cells)
        {
            float scale = cells / Size;
            float fx = x * scale;
            float fy = y * scale;

            int x0 = Mathf.FloorToInt(fx);
            int y0 = Mathf.FloorToInt(fy);

            float tx = Smooth(fx - x0);
            float ty = Smooth(fy - y0);

            float bottom = Mathf.Lerp(Hash(x0, y0), Hash(x0 + 1, y0), tx);
            float top = Mathf.Lerp(Hash(x0, y0 + 1), Hash(x0 + 1, y0 + 1), tx);

            return Mathf.Lerp(bottom, top, ty);
        }

        static float Smooth(float t) => t * t * (3f - 2f * t);

        /// <summary>
        /// Hashed rather than Random: the same lattice value every run, so regenerating the
        /// asset does not silently change the look of the control.
        /// </summary>
        static float Hash(int x, int y)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263;
                h = (h ^ (h >> 13)) * 1274126177;
                return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
            }
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

        /// <summary>
        /// Generates the PNG once and thereafter only checks its import settings. Change a
        /// thickness or a falloff above and the file on disk is already there, so delete it
        /// from Assets/Art/UI and rebuild the controls to see the new shape.
        /// </summary>
        static Sprite Ensure(string path, Func<byte[]> encode)
        {
            if (!File.Exists(path))
            {
                EnsureFolder(path);
                File.WriteAllBytes(path, encode());
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                Debug.Log($"Generated {path}");
            }

            return Import(path);
        }

        /// <summary>
        /// Loads hand-supplied artwork as a Sprite, forcing the same import settings the
        /// generated pieces get. Returns null if the file is not in the project — the caller
        /// decides what to fall back to.
        ///
        /// The settings matter as much here as for the generated shapes: this project's import
        /// default is Multiple, which yields no Sprite at all and draws the control as a white
        /// box.
        /// </summary>
        public static Sprite Imported(string path)
        {
            return File.Exists(path) ? Import(path) : null;
        }

        static Sprite Import(string path)
        {
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
