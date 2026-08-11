using System.IO;
using ShinyMinds.Missions.UI;
using TMPro;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine;
using UnityEngine.UI;
using static ShinyMinds.Missions.EditorTools.MissionEditorUtil;

namespace ShinyMinds.Missions.EditorTools
{
    /// <summary>
    /// Generates Assets/Prefabs/UI/MissionUI.prefab, fully wired.
    ///
    /// Building this by hand is ~25 objects and ~30 inspector references, and every
    /// one of them is a chance to leave a field null or forget to switch a raycast
    /// target off. Generating it is deterministic and re-runnable.
    /// </summary>
    public static class MissionUIBuilder
    {
        const string AssetPath = PrefabRoot + "/UI/MissionUI.prefab";

        /// <summary>
        /// Printed when the prefab is built. Unity runs menu items against the assembly it
        /// has compiled, not the file on disk, so clicking Build before a recompile finishes
        /// silently regenerates the *previous* design — which looks exactly like the change
        /// not working. If the Console does not name the change you just made, Unity had not
        /// compiled it yet: give it focus, wait for the spinner, and run it again.
        /// </summary>
        const string BuildStamp =
            "24px rounded corners on every balloon, panel and button";

        static readonly Color Ink = new Color(0.96f, 0.97f, 1.00f);
        static readonly Color Panel = new Color(0.07f, 0.09f, 0.14f, 0.94f);
        static readonly Color Accent = new Color(0.45f, 0.80f, 1.00f);
        static readonly Color ButtonIdle = new Color(0.16f, 0.22f, 0.33f, 0.98f);

        // The memory bubble is the one warm surface in the UI — a remembered afternoon,
        // not the cold navy of the present.
        static readonly Color MemoryPaper = new Color(0.99f, 0.96f, 0.90f, 0.98f);
        static readonly Color MemoryInk = new Color(0.42f, 0.33f, 0.26f);

        // Storybook speech balloons: white paper, near-black text. The dark plate used
        // everywhere else in this UI works over the game's world, but inside the memory it
        // is a dark shape sitting on a bright drawing, and the pale text on it was the
        // hardest thing in the game to read.
        // Fully opaque, because the balloon is drawn as three overlapping white pieces and
        // any transparency would show as bright seams where they cross.
        static readonly Color BalloonPaper = new Color(1f, 1f, 1f, 1f);
        static readonly Color BalloonInk = new Color(0.11f, 0.13f, 0.17f);
        static readonly Color BalloonOutline = new Color(0f, 0f, 0f, 1f);

        [MenuItem("ShinyMinds/Setup/1. Build Mission UI Prefab")]
        public static GameObject Build()
        {
            GameObject root = new GameObject("MissionUI",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // The two per-NPC Groq canvases sit at 0, so this always draws above them.
            canvas.sortingOrder = 100;
            ApplyTextShaderChannels(canvas);

            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var view = root.AddComponent<MissionUIView>();

            // ---------------------------------------------------------- letterbox
            GameObject letterbox = Rect("Letterbox", root.transform);
            Stretch(letterbox);

            GameObject barTop = Rect("BarTop", letterbox.transform);
            HorizontalBar(barTop, true, 0f);
            AddImage(barTop, Color.black, false);          // raycast OFF - see note below

            GameObject barBottom = Rect("BarBottom", letterbox.transform);
            HorizontalBar(barBottom, false, 0f);
            AddImage(barBottom, Color.black, false);

            // ---------------------------------------------------------- fade
            // Raycast target MUST be off. With InputSystemUIInputModule a full-screen
            // raycast target swallows every click and the choice buttons look dead.
            GameObject fade = Rect("FadeOverlay", root.transform);
            Stretch(fade);
            Image fadeImg = AddImage(fade, new Color(0f, 0f, 0f, 0f), false);

            // ---------------------------------------------------------- objective
            GameObject objRoot = Rect("ObjectiveHud", root.transform);
            Anchor(objRoot, new Vector2(0f, 1f), new Vector2(520f, 60f), new Vector2(40f, -32f));
            AddRounded(objRoot, new Color(0f, 0f, 0f, 0.45f));
            var objHud = objRoot.AddComponent<ObjectiveHud>();

            GameObject objText = Rect("ObjectiveText", objRoot.transform);
            Stretch(objText, 14f);
            TextMeshProUGUI objTmp = AddText(objText, "Objective", 26f, TextAlignmentOptions.Left, Ink);

            Wire(objHud, ("root", objRoot), ("text", objTmp));

            // ---------------------------------------------------------- dialogue
            GameObject dialoguePanel = Rect("DialoguePanel", root.transform);
            Anchor(dialoguePanel, new Vector2(0.5f, 0f), new Vector2(1400f, 240f), new Vector2(0f, 60f));

            GameObject frame = Rect("Frame", dialoguePanel.transform);
            Stretch(frame);
            AddRounded(frame, Panel);

            GameObject speaker = Rect("SpeakerLabel", frame.transform);
            Anchor(speaker, new Vector2(0f, 1f), new Vector2(600f, 44f), new Vector2(36f, -18f));
            TextMeshProUGUI speakerTmp = AddText(speaker, "Speaker", 30f, TextAlignmentOptions.Left, Accent, FontStyles.Bold);

            GameObject line = Rect("LineText", frame.transform);
            RectTransform lineRt = Stretch(line);
            lineRt.offsetMin = new Vector2(36f, 56f);
            lineRt.offsetMax = new Vector2(-36f, -66f);
            TextMeshProUGUI lineTmp = AddText(line, "Line", 32f, TextAlignmentOptions.TopLeft, Ink);
            var lineWriter = line.AddComponent<TypewriterText>();
            Wire(lineWriter, ("target", lineTmp));

            GameObject cont = Rect("ContinuePrompt", frame.transform);
            Anchor(cont, new Vector2(1f, 0f), new Vector2(320f, 36f), new Vector2(-36f, 16f));
            TextMeshProUGUI contTmp = AddText(cont, "Press E", 24f, TextAlignmentOptions.Right,
                                              new Color(0.75f, 0.79f, 0.88f));

            // ---------------------------------------------------------- thought
            GameObject thoughtPanel = Rect("ThoughtPanel", root.transform);
            Anchor(thoughtPanel, new Vector2(0.5f, 0.5f), new Vector2(1000f, 200f), new Vector2(0f, 190f));

            GameObject bubble = Rect("Bubble", thoughtPanel.transform);
            Stretch(bubble);
            AddRounded(bubble, new Color(0.93f, 0.95f, 1f, 0.14f));

            GameObject thought = Rect("ThoughtText", bubble.transform);
            Stretch(thought, 34f);
            TextMeshProUGUI thoughtTmp = AddText(thought, "Thought", 34f, TextAlignmentOptions.Center,
                                                 new Color(0.80f, 0.90f, 1f), FontStyles.Italic);
            var thoughtWriter = thought.AddComponent<TypewriterText>();
            Wire(thoughtWriter, ("target", thoughtTmp));

            // ---------------------------------------------------------- speech balloon
            // Everyone with a body in the scene speaks in one of these, hanging over their
            // head, instead of into the subtitle bar above. Same balloon as the memory
            // uses, so the mission speaks one visual language throughout.
            GameObject worldLayer = Rect("WorldBalloon", root.transform);
            Stretch(worldLayer);

            Balloon world = BuildBalloon(worldLayer.transform, "Balloon", 560f, 30f,
                                         new RectOffset(26, 26, 18, 18), 0f);
            Anchor(world.root, new Vector2(0.5f, 0.5f), new Vector2(560f, 96f), Vector2.zero);

            var worldBubble = worldLayer.AddComponent<WorldSpeechBubble>();
            Wire(worldBubble,
                ("root", worldLayer),
                ("bubble", RT(world.root)),
                ("canvasRect", RT(worldLayer)),
                ("tail", world.tail),
                ("neck", world.neck),
                ("lineText", world.line),
                ("typewriter", world.writer),
                ("headHeight", 1.7f),
                ("headroom", 40f),
                ("screenMargin", 28f),
                ("tailTravel", 0.6f));

            GameObject worldCont = Rect("WorldContinuePrompt", root.transform);
            Anchor(worldCont, new Vector2(1f, 0f), new Vector2(320f, 36f), new Vector2(-48f, 40f));
            TextMeshProUGUI worldContTmp = AddText(worldCont, "Press E", 26f, TextAlignmentOptions.Right,
                                                   new Color(0.94f, 0.95f, 1f));

            // ---------------------------------------------------------- memory
            // A remembered exchange, staged as a thought bubble: the world dims, a warm
            // frame swells up with the scene playing inside it, and a trail of dots ties
            // it back to the girl standing at the gate remembering it.
            GameObject memoryPanel = Rect("MemoryPanel", root.transform);
            Stretch(memoryPanel);

            var memoryGroup = memoryPanel.AddComponent<CanvasGroup>();
            memoryGroup.interactable = false;
            memoryGroup.blocksRaycasts = false;      // never swallow a click - see the fade note above

            GameObject memoryDim = Rect("Dim", memoryPanel.transform);
            Stretch(memoryDim);
            // Lighter than a cutscene fade: the bubble sits in one corner now, so the
            // world it is remembered from should stay legible around it.
            AddImage(memoryDim, new Color(0f, 0f, 0f, 0.40f), false);

            // An oval: a thought bubble, and small enough to leave the person it belongs to
            // on screen underneath it, which is the whole point of the trail. An oval costs
            // its corners, so it runs a little wider than the rectangle it replaces to hold
            // the same picture.
            //
            // Centre-anchored, and both its position and its size are set at runtime by
            // MemoryBubbleAnchor — a remembered scene and a bare thought are not the same
            // size, and neither one belongs in a fixed corner.
            GameObject memoryBubble = Rect("Bubble", memoryPanel.transform);
            Anchor(memoryBubble, new Vector2(0.5f, 0.5f), new Vector2(860f, 580f), Vector2.zero);
            AddEllipse(memoryBubble, MemoryPaper);

            // The render is masked to its own, slightly smaller oval.
            GameObject memoryFrame = Rect("Frame", memoryBubble.transform);
            Anchor(memoryFrame, new Vector2(0.5f, 0.5f), new Vector2(800f, 468f), Vector2.zero);
            AddEllipse(memoryFrame, new Color(0.99f, 0.95f, 0.88f, 1f));
            memoryFrame.AddComponent<Mask>().showMaskGraphic = true;

            // Wider than the oval and cropped by it, at the texture's exact 16:9 — nothing
            // is stretched, because a squashed face is the one thing a child will notice.
            GameObject memoryRender = Rect("Render", memoryFrame.transform);
            Anchor(memoryRender, new Vector2(0.5f, 0.5f), new Vector2(832f, 468f), Vector2.zero);
            var memoryRaw = memoryRender.AddComponent<RawImage>();
            memoryRaw.texture = MissionMemoryStageBuilder.EnsureRenderTexture();
            memoryRaw.raycastTarget = false;

            // Centred low in the paper margin: the oval has no bottom-right corner to sit in.
            // Anchored as a FRACTION of the oval's height rather than a pixel offset, because
            // the oval is resized at runtime — 0.066 keeps it just inside the pinched bottom
            // of the ellipse whether it is holding a memory or a thought.
            GameObject memoryCont = Rect("ContinuePrompt", memoryBubble.transform);
            Anchor(memoryCont, new Vector2(0.5f, 0.066f), new Vector2(200f, 28f), Vector2.zero);
            TextMeshProUGUI memoryContTmp = AddText(memoryCont, "Press E", 20f, TextAlignmentOptions.Center,
                                                    MemoryInk);

            // Placed from the centre rather than the corners, which an oval does not have.
            // Each tail leans outwards, towards the stand-in standing under it.
            MemoryBubble memoryLeft = BuildMemoryBubble(memoryBubble.transform, "LeftBalloon",
                                                        new Vector2(0.5f, 0.5f), new Vector2(-150f, 100f), -50f);
            MemoryBubble memoryRight = BuildMemoryBubble(memoryBubble.transform, "RightBalloon",
                                                         new Vector2(0.5f, 0.5f), new Vector2(150f, 100f), 50f);

            // A thought is played in this same bubble with the picture switched off, so it
            // needs one balloon of its own: centred in the paper the render would have
            // filled, and wide enough to be the only thing in there.
            MemoryBubble memoryThought = BuildThoughtBalloon(memoryBubble.transform);

            // The thought-tail: three shrinking dots stepping diagonally out from the oval's
            // lower-left arc, towards the girl below it. Children of the bubble, so they follow
            // it wherever it goes and swell with it as the memory opens.
            //
            // These offsets are what a MEMORY keeps — MemoryBubbleAnchor captures them once
            // and puts them back every time it opens one. For a thought, which can happen
            // wherever the girl is standing, it walks them onto the line to her head instead.
            RectTransform dot0 = BuildTailDot(memoryBubble.transform, "TailDot_0", 32f, new Vector2(-322f, -217f));
            RectTransform dot1 = BuildTailDot(memoryBubble.transform, "TailDot_1", 21f, new Vector2(-352f, -237f));
            RectTransform dot2 = BuildTailDot(memoryBubble.transform, "TailDot_2", 13f, new Vector2(-374f, -252f));

            var memoryAnchor = memoryPanel.AddComponent<MemoryBubbleAnchor>();
            Wire(memoryAnchor,
                ("bubble", RT(memoryBubble)),
                ("canvasRect", RT(memoryPanel)),
                ("memorySize", new Vector2(860f, 580f)),
                ("thoughtSize", new Vector2(440f, 250f)),
                ("headHeight", 1.7f),
                ("headroom", 90f),
                ("sideLean", 0.55f),
                ("screenMargin", 24f),
                ("dotHeadroom", 28f),
                ("parkedInset", 45f));

            WireList(memoryAnchor, "dots", dot0, dot1, dot2);

            // ---------------------------------------------------------- choices
            GameObject choicePanel = Rect("ChoicePanel", root.transform);
            Anchor(choicePanel, new Vector2(0.5f, 0.5f), new Vector2(1100f, 460f), Vector2.zero);
            AddRounded(choicePanel, Panel);

            GameObject prompt = Rect("ChoicePrompt", choicePanel.transform);
            Anchor(prompt, new Vector2(0.5f, 1f), new Vector2(1000f, 60f), new Vector2(0f, -28f));
            TextMeshProUGUI promptTmp = AddText(prompt, "What should Aisha do?", 38f,
                                                TextAlignmentOptions.Center, Ink, FontStyles.Bold);

            GameObject choices = Rect("Choices", choicePanel.transform);
            RectTransform choicesRt = Stretch(choices);
            choicesRt.offsetMin = new Vector2(60f, 40f);
            choicesRt.offsetMax = new Vector2(-60f, -110f);

            var layout = choices.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 18f;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childAlignment = TextAnchor.UpperCenter;

            MissionChoiceButton[] buttons = new MissionChoiceButton[3];
            for (int i = 0; i < 3; i++)
                buttons[i] = BuildChoiceButton(choices.transform, i);

            // ---------------------------------------------------------- ending
            GameObject endingCardGo = Rect("EndingCard", root.transform);
            Anchor(endingCardGo, new Vector2(0.5f, 0.5f), new Vector2(1100f, 620f), Vector2.zero);
            var endingCard = endingCardGo.AddComponent<MissionEndingCard>();

            GameObject endFrame = Rect("Frame", endingCardGo.transform);
            Stretch(endFrame);
            AddRounded(endFrame, Panel);

            GameObject badge = Rect("Badge", endFrame.transform);
            Anchor(badge, new Vector2(0.5f, 1f), new Vector2(120f, 120f), new Vector2(0f, -40f));
            // A Sprite, never an emoji glyph - TMP's default atlas renders those as boxes.
            Image badgeImg = AddImage(badge, Color.white, false);
            badgeImg.enabled = false;
            badgeImg.preserveAspect = true;

            GameObject endTitle = Rect("EndingTitle", endFrame.transform);
            Anchor(endTitle, new Vector2(0.5f, 1f), new Vector2(1000f, 70f), new Vector2(0f, -180f));
            TextMeshProUGUI endTitleTmp = AddText(endTitle, "Ending", 46f, TextAlignmentOptions.Center,
                                                  Ink, FontStyles.Bold);

            GameObject lesson = Rect("LessonText", endFrame.transform);
            RectTransform lessonRt = Stretch(lesson);
            lessonRt.offsetMin = new Vector2(70f, 140f);
            lessonRt.offsetMax = new Vector2(-70f, -270f);
            TextMeshProUGUI lessonTmp = AddText(lesson, "Lesson", 30f, TextAlignmentOptions.Top, Ink);

            GameObject btnRow = Rect("Buttons", endFrame.transform);
            Anchor(btnRow, new Vector2(0.5f, 0f), new Vector2(760f, 84f), new Vector2(0f, 36f));
            var row = btnRow.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 30f;
            row.childForceExpandWidth = true;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childAlignment = TextAnchor.MiddleCenter;

            Button retry = BuildButton(btnRow.transform, "RetryButton", "Try Again");
            Button cont2 = BuildButton(btnRow.transform, "ContinueButton", "Continue");

            Wire(endingCard,
                ("root", endingCardGo),
                ("badge", badgeImg),
                ("title", endTitleTmp),
                ("lesson", lessonTmp),
                ("retryButton", retry),
                ("continueButton", cont2));

            // ---------------------------------------------------------- the view
            Wire(view,
                ("dialoguePanel", dialoguePanel),
                ("speakerLabel", speakerTmp),
                ("lineText", lineTmp),
                ("lineTypewriter", lineWriter),
                ("continuePrompt", contTmp),
                ("worldBubble", worldBubble),
                ("worldContinuePrompt", worldContTmp),
                ("thoughtPanel", thoughtPanel),
                ("thoughtText", thoughtTmp),
                ("thoughtTypewriter", thoughtWriter),
                ("memoryPanel", memoryPanel),
                ("memoryGroup", memoryGroup),
                ("memoryBubble", RT(memoryBubble)),
                ("memoryAnchor", memoryAnchor),
                ("memoryStageView", memoryFrame),
                ("memoryLeft", memoryLeft),
                ("memoryRight", memoryRight),
                ("memoryThought", memoryThought),
                ("memoryContinuePrompt", memoryContTmp),
                ("memoryFadeSeconds", 0.45f),
                ("choicePanel", choicePanel),
                ("choicePrompt", promptTmp),
                ("endingCard", endingCard),
                ("objectiveHud", objHud),
                ("fadeOverlay", fadeImg),
                ("letterboxTop", RT(barTop)),
                ("letterboxBottom", RT(barBottom)),
                // 0 disables the cinematic bars everywhere. Set to 110 to bring them back.
                ("letterboxHeight", 0f));

            WireList(view, "choiceButtons", buttons[0], buttons[1], buttons[2]);

            // Legibility pass: the story text carries the lesson, so it gets the shadowed
            // material. Buttons and HUD sit on their own opaque plates and don't need it.
            Material subtitle = EnsureSubtitleMaterial();
            if (subtitle != null)
            {
                lineTmp.fontSharedMaterial = subtitle;
                thoughtTmp.fontSharedMaterial = subtitle;
                speakerTmp.fontSharedMaterial = subtitle;
                endTitleTmp.fontSharedMaterial = subtitle;
                lessonTmp.fontSharedMaterial = subtitle;
                // This one sits directly on the world with no plate under it at all.
                worldContTmp.fontSharedMaterial = subtitle;
            }

            // Panels start hidden; MissionUIView.Awake() also calls HideAll() at runtime.
            dialoguePanel.SetActive(false);
            thoughtPanel.SetActive(false);
            memoryPanel.SetActive(false);
            worldLayer.SetActive(false);
            worldCont.SetActive(false);
            choicePanel.SetActive(false);
            endingCardGo.SetActive(false);
            fade.SetActive(false);

            GameObject prefab = SavePrefab(root, AssetPath);
            AssetDatabase.SaveAssets();

            if (prefab != null)
            {
                Selection.activeObject = prefab;
                EditorGUIUtility.PingObject(prefab);
                Debug.Log($"Built {AssetPath} — {BuildStamp}", prefab);
            }

            return prefab;
        }

        const string SubtitleMaterialPath =
            "Assets/TextMesh Pro/Resources/Fonts & Materials/Subtitle SDF.mat";

        /// <summary>
        /// A copy of the default font material with an underlay (soft drop shadow) enabled.
        ///
        /// Subtitles have to survive a bright sky and a dark interior in the same scene,
        /// and a backing plate alone cannot do that once the camera starts cutting. This
        /// is a separate material rather than an edit to the shared LiberationSans
        /// material, which would restyle every TMP object in the project including the
        /// Groq NPC panels.
        /// </summary>
        static Material EnsureSubtitleMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(SubtitleMaterialPath);
            if (existing != null) return existing;

            TMP_FontAsset font = TMP_Settings.defaultFontAsset;
            if (font == null || font.material == null)
            {
                Debug.LogWarning("No default TMP font asset; subtitles will use the plain material.");
                return null;
            }

            var mat = new Material(font.material) { name = "Subtitle SDF" };

            mat.EnableKeyword("UNDERLAY_ON");
            mat.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 0.9f));
            mat.SetFloat("_UnderlayOffsetX", 0.4f);
            mat.SetFloat("_UnderlayOffsetY", -0.4f);
            mat.SetFloat("_UnderlayDilate", 0.2f);
            mat.SetFloat("_UnderlaySoftness", 0.25f);

            EnsureFolder(SubtitleMaterialPath);
            AssetDatabase.CreateAsset(mat, SubtitleMaterialPath);
            AssetDatabase.SaveAssets();

            Debug.Log($"Created {SubtitleMaterialPath}");
            return mat;
        }

        /// <summary>
        /// TextMeshPro's SDF shader reads per-vertex scale data out of TEXCOORD1, and
        /// reads Normal/Tangent for its bevel and lighting variants. A Canvas created
        /// from script defaults additionalShaderChannels to Nothing, so the canvas feeds
        /// the shader zeros, the distance-field maths degenerates, and text renders muddy
        /// and dark whatever colour you set on it. Creating a Canvas via the GameObject
        /// menu sets these for you; creating one in code does not.
        /// </summary>
        public static void ApplyTextShaderChannels(Canvas canvas)
        {
            canvas.additionalShaderChannels =
                AdditionalCanvasShaderChannels.TexCoord1 |
                AdditionalCanvasShaderChannels.Normal |
                AdditionalCanvasShaderChannels.Tangent;
        }

        /// <summary>
        /// Repairs canvases that were generated before the fix above, so an existing
        /// scene does not have to be rebuilt from scratch.
        /// </summary>
        [MenuItem("ShinyMinds/Setup/Fix Text Rendering On Existing Canvases")]
        public static void FixExistingCanvases()
        {
            int fixedCount = 0;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetPath);
            if (prefab != null)
            {
                var canvas = prefab.GetComponent<Canvas>();
                if (canvas != null)
                {
                    ApplyTextShaderChannels(canvas);
                    EditorUtility.SetDirty(prefab);
                    AssetDatabase.SaveAssets();
                    fixedCount++;
                }
            }

            foreach (Canvas canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include))
            {
                if (canvas.additionalShaderChannels == AdditionalCanvasShaderChannels.None)
                {
                    ApplyTextShaderChannels(canvas);
                    EditorUtility.SetDirty(canvas);
                    fixedCount++;
                }
            }

            Debug.Log($"Fixed shader channels on {fixedCount} canvas(es). Save the scene.");
        }

        /// <summary>
        /// The corner radius every dialogue surface gets, in canvas pixels. The sprite is
        /// generated at one texture pixel per canvas unit and 9-sliced, so the radius drawn
        /// into the texture and the border handed to the importer are the same number.
        /// </summary>
        const int CornerRadius = 24;

        // Radius + a flat middle to stretch + radius.
        const int RoundedSpriteSize = CornerRadius * 4;

        const string RoundedSpritePath = "Assets/Art/UI/UI_RoundedRect.png";
        const string EllipseSpritePath = "Assets/Art/UI/UI_Ellipse.png";

        /// <summary>
        /// Rounds a panel, balloon or button. Every dialogue surface in this UI goes through
        /// here, so the whole thing shares one radius.
        /// </summary>
        static Image AddRounded(GameObject go, Color color, bool raycastTarget = false)
        {
            Image img = AddImage(go, color, raycastTarget);
            img.sprite = EnsureRoundedSprite();
            img.type = Image.Type.Sliced;
            return img;
        }

        /// <summary>
        /// A black keyline around a graphic. Outline duplicates the mesh into the four
        /// diagonals, so the distance is the border's thickness.
        /// </summary>
        static Outline AddOutline(GameObject go, float thickness = 2f)
        {
            var outline = go.AddComponent<Outline>();
            outline.effectColor = BalloonOutline;
            outline.effectDistance = new Vector2(thickness, thickness);
            return outline;
        }

        /// <summary>
        /// A disc stretched to the RectTransform, which is how the memory bubble gets its
        /// oval and the tail its dots.
        /// </summary>
        static Image AddEllipse(GameObject go, Color color)
        {
            Image img = AddImage(go, color, false);
            img.sprite = EnsureEllipseSprite();
            img.type = Image.Type.Simple;
            return img;
        }

        /// <summary>
        /// Generates the 9-sliced rounded rectangle every panel and balloon is drawn with.
        ///
        /// Unity's built-in UISprite was doing this job, and it is a 32px texture with a 3px
        /// border: sliced, its corners stay 3 canvas pixels however big the panel is, which on
        /// a 1400-wide dialogue bar is indistinguishable from a square. Generating the sprite
        /// means the radius is whatever <see cref="CornerRadius"/> says, and 9-slicing keeps it
        /// exactly that at every size, from an 86px button to a 620px card.
        /// </summary>
        static Sprite roundedSprite;

        static Sprite EnsureRoundedSprite()
        {
            if (roundedSprite != null) return roundedSprite;

            if (!File.Exists(RoundedSpritePath))
                WriteRoundedRectPng();

            var importer = AssetImporter.GetAtPath(RoundedSpritePath) as TextureImporter;
            if (importer == null)
            {
                AssetDatabase.ImportAsset(RoundedSpritePath, ImportAssetOptions.ForceSynchronousImport);
                importer = AssetImporter.GetAtPath(RoundedSpritePath) as TextureImporter;
            }

            var border = new Vector4(CornerRadius, CornerRadius, CornerRadius, CornerRadius);

            // Without the border the Image has nothing to slice and stretches the corners into
            // smears, which is worse than the square corners this replaces.
            if (importer != null &&
                (importer.textureType != TextureImporterType.Sprite ||
                 importer.spriteImportMode != SpriteImportMode.Single ||
                 importer.spriteBorder != border ||
                 !importer.alphaIsTransparency))
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spriteBorder = border;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }

            roundedSprite = AssetDatabase.LoadAssetAtPath<Sprite>(RoundedSpritePath);

            if (roundedSprite == null)
                Debug.LogError($"{RoundedSpritePath} did not import as a Sprite. Panels and " +
                               "balloons will have square corners. Check the texture's Import " +
                               "Settings: Texture Type = Sprite, Sprite Mode = Single.");

            return roundedSprite;
        }

        static void WriteRoundedRectPng()
        {
            const int size = RoundedSpriteSize;
            const float half = size * 0.5f;
            const float flat = half - CornerRadius;      // half-extent of the straight section

            var pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Distance into the corner quadrant: zero along the flat edges, so the
                    // shape is a plain rectangle everywhere except its four corners.
                    float dx = Mathf.Max(Mathf.Abs(x + 0.5f - half) - flat, 0f);
                    float dy = Mathf.Max(Mathf.Abs(y + 0.5f - half) - flat, 0f);

                    // Feathered over one texel, so the corners antialias rather than
                    // stair-step. The straight edges need none: they are axis-aligned.
                    float alpha = Mathf.Clamp01(CornerRadius - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);

                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.SetPixels32(pixels);
            tex.Apply();

            EnsureFolder(RoundedSpritePath);
            File.WriteAllBytes(RoundedSpritePath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(RoundedSpritePath, ImportAssetOptions.ForceSynchronousImport);
            Debug.Log($"Generated {RoundedSpritePath} — {CornerRadius}px corners");
        }

        /// <summary>
        /// Generates a 512px white disc to stretch into ovals.
        ///
        /// Unity's built-in Knob is only a few dozen pixels across, and blowing that up to
        /// an 860-wide bubble gives a soft, wobbling edge. Generating the disc once, large,
        /// keeps the rim crisp at any size the bubble is ever set to.
        /// </summary>
        static Sprite ellipseSprite;

        /// <summary>
        /// Public because the touch controls are drawn from discs too, and one generator owning
        /// the asset beats two writing the same PNG with different import settings.
        /// </summary>
        public static Sprite EnsureEllipseSprite()
        {
            if (ellipseSprite != null) return ellipseSprite;

            if (!File.Exists(EllipseSpritePath))
                WriteEllipsePng();

            var importer = AssetImporter.GetAtPath(EllipseSpritePath) as TextureImporter;
            if (importer == null)
            {
                AssetDatabase.ImportAsset(EllipseSpritePath, ImportAssetOptions.ForceSynchronousImport);
                importer = AssetImporter.GetAtPath(EllipseSpritePath) as TextureImporter;
            }

            if (importer != null &&
                (importer.textureType != TextureImporterType.Sprite ||
                 importer.spriteImportMode != SpriteImportMode.Single ||
                 !importer.alphaIsTransparency))
            {
                importer.textureType = TextureImporterType.Sprite;
                // Explicit, because this project's import default is Multiple — which slices
                // the texture into named sub-sprites and, with no slices defined, produces no
                // Sprite at all. Image.sprite then lands on null and every ellipse in the
                // memory bubble silently renders as a plain rectangle.
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }

            ellipseSprite = AssetDatabase.LoadAssetAtPath<Sprite>(EllipseSpritePath);

            if (ellipseSprite == null)
                Debug.LogError($"{EllipseSpritePath} did not import as a Sprite. The memory " +
                               "bubble will be a rectangle and its tail dots invisible. Check " +
                               "the texture's Import Settings: Texture Type = Sprite, Sprite Mode = Single.");

            return ellipseSprite;
        }

        static void WriteEllipsePng()
        {
            const int size = 512;
            const float radius = size * 0.5f;

            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - radius) / radius;
                    float dy = (y + 0.5f - radius) / radius;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);

                    // Feathered over roughly one texel, so the rim antialiases instead of
                    // stair-stepping once it is scaled up.
                    float alpha = Mathf.Clamp01((1f - d) * radius);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.SetPixels32(pixels);
            tex.Apply();

            EnsureFolder(EllipseSpritePath);
            File.WriteAllBytes(EllipseSpritePath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(EllipseSpritePath, ImportAssetOptions.ForceSynchronousImport);
            Debug.Log($"Generated {EllipseSpritePath}");
        }

        /// <summary>
        /// One dot of the thought-tail, positioned from the parent's centre — an oval has
        /// no corner to anchor to, and the centre is the space MemoryBubbleAnchor works in.
        /// </summary>
        static RectTransform BuildTailDot(Transform parent, string name, float size, Vector2 offset)
        {
            GameObject go = Rect(name, parent);
            RectTransform rt = Anchor(go, new Vector2(0.5f, 0.5f), new Vector2(size, size), offset);
            AddEllipse(go, MemoryPaper);
            return rt;
        }

        /// <summary>The pieces of a balloon a caller has to wire or move.</summary>
        struct Balloon
        {
            public GameObject root;
            public CanvasGroup group;
            public RectTransform tail;
            public RectTransform neck;
            public TextMeshProUGUI line;
            public TypewriterText writer;
        }

        /// <summary>
        /// A storybook speech balloon: white paper, a black keyline, dark text centred in
        /// it, and a tail pointing down at whoever is speaking. Used both inside the memory
        /// bubble and over the heads of the actors in the world, so the two read as one
        /// language.
        ///
        /// No speaker name anywhere. The tail says who is talking, and the line is far
        /// easier to read with the whole balloon to itself.
        ///
        /// Built back to front, because the outline decides the order:
        ///
        ///   Tail   a square turned 45 degrees, outlined, UNDER the balloon so the balloon
        ///          hides its top half and that half's outline
        ///   Fill   the balloon itself, outlined
        ///   Neck   a small white patch over the join, hiding the run of balloon outline
        ///          that would otherwise cut straight across the tail
        ///
        /// The result is one continuous black silhouette around balloon and tail, which is
        /// how a drawn speech bubble reads. The width is fixed and the height follows the
        /// text, so a short line gets a small balloon instead of a half-empty box.
        /// </summary>
        static Balloon BuildBalloon(Transform parent, string name, float width, float fontSize,
                                    RectOffset padding, float tailX)
        {
            GameObject go = Rect(name, parent);
            RT(go).sizeDelta = new Vector2(width, 90f);

            var group = go.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            // Only the text is laid out; the paper and the tail are scenery and opt out.
            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.padding = padding;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.MiddleCenter;

            var fitter = go.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;   // width stays as set
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            GameObject tail = Rect("Tail", go.transform);
            RectTransform tailRt = Anchor(tail, new Vector2(0.5f, 0f), new Vector2(26f, 26f),
                                          new Vector2(tailX, -6f));
            AddImage(tail, BalloonPaper, false);
            AddOutline(tail);
            tailRt.localRotation = Quaternion.Euler(0f, 0f, 45f);
            IgnoreLayout(tail);

            GameObject fill = Rect("Fill", go.transform);
            Stretch(fill);
            AddRounded(fill, BalloonPaper);
            AddOutline(fill);
            IgnoreLayout(fill);

            GameObject lineGo = Rect("Line", go.transform);
            TextMeshProUGUI lineTmp = AddText(lineGo, "Line", fontSize, TextAlignmentOptions.Center, BalloonInk);

            var writer = lineGo.AddComponent<TypewriterText>();
            Wire(writer, ("target", lineTmp));

            // Deliberately NOT the subtitle material: its dark underlay exists to lift pale
            // text off a bright background, and under dark text on white it only smears.

            GameObject neck = Rect("Neck", go.transform);
            RectTransform neckRt = Anchor(neck, new Vector2(0.5f, 0f), new Vector2(30f, 9f),
                                          new Vector2(tailX, -3f));
            AddImage(neck, BalloonPaper, false);
            IgnoreLayout(neck);

            return new Balloon
            {
                root = go, group = group, tail = tailRt, neck = neckRt,
                line = lineTmp, writer = writer,
            };
        }

        static void IgnoreLayout(GameObject go) => go.AddComponent<LayoutElement>().ignoreLayout = true;

        static MemoryBubble BuildMemoryBubble(Transform parent, string name, Vector2 anchor,
                                              Vector2 offset, float tailX)
        {
            Balloon b = BuildBalloon(parent, name, 280f, 22f, new RectOffset(18, 18, 12, 12), tailX);

            // Kept well inside the oval: its corners are the first thing an ellipse cuts.
            Anchor(b.root, anchor, new Vector2(280f, 90f), offset);

            var bubble = b.root.AddComponent<MemoryBubble>();
            Wire(bubble,
                ("root", b.root),
                ("group", b.group),
                ("lineText", b.line),
                ("typewriter", b.writer),
                ("dimmedAlpha", 0.62f));

            b.root.SetActive(false);    // MissionUIView shows whichever side is speaking
            return bubble;
        }

        /// <summary>
        /// The balloon a thought gets: centred in the memory bubble, wider than the two stage
        /// balloons because it has the whole oval to itself, and italic, which is how the
        /// thought panel it replaces read.
        ///
        /// Sized against the oval's thought size (440×250), not its memory size. 320 wide
        /// keeps its corners inside the ellipse even when the text runs to three lines — the
        /// corners are the first thing an oval cuts.
        /// </summary>
        static MemoryBubble BuildThoughtBalloon(Transform parent)
        {
            Balloon b = BuildBalloon(parent, "ThoughtBalloon", 320f, 26f,
                                     new RectOffset(22, 22, 16, 16), 0f);

            // Slightly above centre: the continue prompt sits low in the paper margin, and a
            // balloon whose text grows to three lines must not reach it.
            Anchor(b.root, new Vector2(0.5f, 0.5f), new Vector2(320f, 90f), new Vector2(0f, 18f));

            // No tail. A tail points at whoever is speaking and there is nobody in the oval
            // to point at — the bubble's own trail of dots already ties the thought back to
            // the girl standing under it.
            b.tail.gameObject.SetActive(false);
            b.neck.gameObject.SetActive(false);

            b.line.fontStyle = FontStyles.Italic;

            var bubble = b.root.AddComponent<MemoryBubble>();
            Wire(bubble,
                ("root", b.root),
                ("group", b.group),
                ("lineText", b.line),
                ("typewriter", b.writer),
                ("dimmedAlpha", 0.62f));

            b.root.SetActive(false);
            return bubble;
        }

        static MissionChoiceButton BuildChoiceButton(Transform parent, int index)
        {
            GameObject go = Rect($"ChoiceButton_{index}", parent);
            RT(go).sizeDelta = new Vector2(0f, 86f);

            var le = go.AddComponent<LayoutElement>();
            le.minHeight = 86f;
            le.preferredHeight = 86f;

            Image bg = AddRounded(go, ButtonIdle, true);    // raycast ON - it is clickable

            var button = go.AddComponent<Button>();
            button.targetGraphic = bg;

            var colors = button.colors;
            colors.highlightedColor = new Color(0.26f, 0.42f, 0.62f);
            colors.pressedColor = new Color(0.20f, 0.34f, 0.52f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;

            GameObject letter = Rect("Letter", go.transform);
            Anchor(letter, new Vector2(0f, 0.5f), new Vector2(70f, 70f), new Vector2(24f, 0f));
            TextMeshProUGUI letterTmp = AddText(letter, ((char)('A' + index)).ToString(), 34f,
                                                TextAlignmentOptions.Center, Accent, FontStyles.Bold);

            GameObject label = Rect("Label", go.transform);
            RectTransform labelRt = Stretch(label);
            labelRt.offsetMin = new Vector2(104f, 0f);
            labelRt.offsetMax = new Vector2(-28f, 0f);
            TextMeshProUGUI labelTmp = AddText(label, "Choice", 32f, TextAlignmentOptions.Left, Ink);

            var mcb = go.AddComponent<MissionChoiceButton>();
            Wire(mcb, ("button", button), ("label", labelTmp), ("letter", letterTmp));

            return mcb;
        }

        static Button BuildButton(Transform parent, string name, string text)
        {
            GameObject go = Rect(name, parent);
            Image bg = AddRounded(go, ButtonIdle, true);

            var button = go.AddComponent<Button>();
            button.targetGraphic = bg;

            var colors = button.colors;
            colors.highlightedColor = new Color(0.26f, 0.42f, 0.62f);
            colors.pressedColor = new Color(0.20f, 0.34f, 0.52f);
            button.colors = colors;

            GameObject label = Rect("Label", go.transform);
            Stretch(label);
            AddText(label, text, 30f, TextAlignmentOptions.Center, Ink, FontStyles.Bold);

            return button;
        }
    }
}
