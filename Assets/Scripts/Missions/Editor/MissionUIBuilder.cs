using ShinyMinds.Missions.UI;
using TMPro;
using UnityEditor;
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

        static readonly Color Ink = new Color(0.96f, 0.97f, 1.00f);
        static readonly Color Panel = new Color(0.07f, 0.09f, 0.14f, 0.94f);
        static readonly Color Accent = new Color(0.45f, 0.80f, 1.00f);
        static readonly Color ButtonIdle = new Color(0.16f, 0.22f, 0.33f, 0.98f);

        [MenuItem("ShinyMinds/Setup/1. Build Mission UI Prefab")]
        public static GameObject Build()
        {
            GameObject root = new GameObject("MissionUI",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // The two per-NPC Groq canvases sit at 0, so this always draws above them.
            canvas.sortingOrder = 100;

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
            AddImage(objRoot, new Color(0f, 0f, 0f, 0.45f), false);
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
            AddImage(frame, Panel, false);

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
            AddImage(bubble, new Color(0.93f, 0.95f, 1f, 0.14f), false);

            GameObject thought = Rect("ThoughtText", bubble.transform);
            Stretch(thought, 34f);
            TextMeshProUGUI thoughtTmp = AddText(thought, "Thought", 34f, TextAlignmentOptions.Center,
                                                 new Color(0.80f, 0.90f, 1f), FontStyles.Italic);
            var thoughtWriter = thought.AddComponent<TypewriterText>();
            Wire(thoughtWriter, ("target", thoughtTmp));

            // ---------------------------------------------------------- choices
            GameObject choicePanel = Rect("ChoicePanel", root.transform);
            Anchor(choicePanel, new Vector2(0.5f, 0.5f), new Vector2(1100f, 460f), Vector2.zero);
            AddImage(choicePanel, Panel, false);

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
            AddImage(endFrame, Panel, false);

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
                ("thoughtPanel", thoughtPanel),
                ("thoughtText", thoughtTmp),
                ("thoughtTypewriter", thoughtWriter),
                ("choicePanel", choicePanel),
                ("choicePrompt", promptTmp),
                ("endingCard", endingCard),
                ("objectiveHud", objHud),
                ("fadeOverlay", fadeImg),
                ("letterboxTop", RT(barTop)),
                ("letterboxBottom", RT(barBottom)),
                ("letterboxHeight", 110f));

            WireList(view, "choiceButtons", buttons[0], buttons[1], buttons[2]);

            // Panels start hidden; MissionUIView.Awake() also calls HideAll() at runtime.
            dialoguePanel.SetActive(false);
            thoughtPanel.SetActive(false);
            choicePanel.SetActive(false);
            endingCardGo.SetActive(false);
            fade.SetActive(false);

            GameObject prefab = SavePrefab(root, AssetPath);
            AssetDatabase.SaveAssets();

            if (prefab != null)
            {
                Selection.activeObject = prefab;
                EditorGUIUtility.PingObject(prefab);
                Debug.Log($"Built {AssetPath}", prefab);
            }

            return prefab;
        }

        static MissionChoiceButton BuildChoiceButton(Transform parent, int index)
        {
            GameObject go = Rect($"ChoiceButton_{index}", parent);
            RT(go).sizeDelta = new Vector2(0f, 86f);

            var le = go.AddComponent<LayoutElement>();
            le.minHeight = 86f;
            le.preferredHeight = 86f;

            Image bg = AddImage(go, ButtonIdle, true);      // raycast ON - it is clickable

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
            Image bg = AddImage(go, ButtonIdle, true);

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
