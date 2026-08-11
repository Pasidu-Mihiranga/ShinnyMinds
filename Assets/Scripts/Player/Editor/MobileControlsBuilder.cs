using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using ShinyMinds.Missions.EditorTools;
using static ShinyMinds.Missions.EditorTools.MissionEditorUtil;

namespace ShinyMinds.PlayerTools
{
    /// <summary>
    /// Rebuilds the on-screen touch controls in the open scene, fully wired.
    ///
    /// The hand-built MobileControls this replaces had six independent faults, each of
    /// which alone was enough to stop the stick working: no MobileInput component existed
    /// anywhere in the scene (so every reader saw a null Instance), the Jump button was a
    /// bare TextMeshPro label with no handler, Run and Jump were stacked on the same
    /// centre-screen coordinates, the stick's background Image was disabled, its Handle
    /// object was deactivated, and a second FixedJoystick with null background/handle
    /// references sat on a child throwing a NullReferenceException from Start.
    ///
    /// Re-runnable: it destroys whatever is there and builds again.
    /// </summary>
    public static class MobileControlsBuilder
    {
        const string RootName = "MobileControls";

        // Landscape, matching the game's aspect. The old canvas used a 1080x1920 portrait
        // reference, which shrank every control to about two thirds on a wide screen.
        static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

        // Jump and Run share a size and a baseline so the right-thumb cluster reads as one
        // control group. Change these two and both buttons follow.
        static readonly Vector2 ButtonSize = new Vector2(180f, 180f);
        const float ButtonBaseline = 90f;   // bottom edge, up from the screen bottom
        const float ButtonInset = 70f;      // Jump's right edge, in from the screen right
        const float ButtonGap = 40f;        // clear space between the two

        static readonly Vector2 BottomRight = new Vector2(1f, 0f);

        // The shooter-HUD palette: a lit red rim and hairline gauge circles over a smoked
        // dark disc, rather than the Joystick Pack's flat pastel. Everything is a tint on a
        // white generated sprite, so the whole control recolours from these seven values.
        static readonly Color Rim = new Color(0.92f, 0.10f, 0.12f, 0.95f);
        static readonly Color RimGlow = new Color(1.00f, 0.13f, 0.15f, 0.40f);
        static readonly Color Smoke = new Color(0.05f, 0.05f, 0.06f, 0.62f);
        static readonly Color Hairline = new Color(0.86f, 0.88f, 0.92f, 0.32f);
        static readonly Color Arrow = new Color(0.90f, 0.92f, 0.96f, 0.55f);
        static readonly Color HandleFill = new Color(0.09f, 0.09f, 0.10f, 0.88f);
        static readonly Color HandleDot = new Color(1f, 1f, 1f, 0.85f);

        // Light labels now: the buttons went from pastel fills to the same smoked disc.
        static readonly Color Ink = new Color(0.92f, 0.94f, 0.98f);

        [MenuItem("ShinyMinds/Mobile/Rebuild Touch Controls")]
        public static void Build()
        {
            RemoveExisting();

            GameObject root = new GameObject(RootName,
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Above the minimap canvas (0) so a thumb never lands on the map, below
            // MissionUI (100) so dialogue and choice panels still draw over the controls.
            canvas.sortingOrder = 50;

            // A code-created Canvas omits the extra shader channels TMP needs, and the
            // JUMP/RUN labels would render muddy. Same fix MissionUIBuilder applies.
            Missions.EditorTools.MissionUIBuilder.ApplyTextShaderChannels(canvas);

            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.matchWidthOrHeight = 0.5f;

            var input = root.AddComponent<MobileInput>();

            // Everything switchable lives under here. MobileInput itself must stay active
            // to hear the unlock event, so it cannot be on the object it hides.
            GameObject controls = Rect("Controls", root.transform);
            Stretch(controls);

            TouchJoystick stick = BuildStick(controls.transform);
            BuildJump(controls.transform);
            BuildRun(controls.transform);

            Wire(input,
                ("joystick", stick),
                ("controlsRoot", controls),
                ("touchDevicesOnly", false),
                // Testing aid, off by default: on means the desktop build reads "Touch" on its
                // prompts and advances dialogue on a mouse click.
                ("simulateTouchOnDesktop", false));

            FixEventSystem();

            Undo.RegisterCreatedObjectUndo(root, "Rebuild Touch Controls");
            Selection.activeGameObject = root;
            EditorSceneManager.MarkSceneDirty(root.scene);

            Debug.Log("Touch controls rebuilt: red-rim stick (bottom left), Jump and Run " +
                      "(bottom right).", root);
        }

        // ------------------------------------------------------------------ pieces

        const float StickSize = 340f;

        /// <summary>
        /// The stick, drawn as a shooter HUD: a smoked disc under a lit red rim, with a bloom
        /// spilling off it, two hairline gauge circles, four chevrons marking the axes, and a
        /// dark thumb with a bright pip at its centre.
        ///
        /// Built as separate tinted layers rather than one baked sprite so the palette above is
        /// the only thing to edit, and drawn in child order: bloom, rim, circles, chevrons,
        /// thumb on top. The disc that catches the touch is the Stick's own Image — the layers
        /// all have their raycast target OFF, because a child that swallowed the pointer would
        /// cancel the drag under the finger.
        /// </summary>
        static TouchJoystick BuildStick(Transform parent)
        {
            GameObject stick = Rect("Stick", parent);
            Anchor(stick, new Vector2(0f, 0f), new Vector2(StickSize, StickSize), new Vector2(60f, 60f));
            AddCircle(stick, MissionUIBuilder.EnsureEllipseSprite(), Smoke, true);

            // Sized so the brightest part of the bloom lands on the rim and the tail spills
            // outwards past the base circle.
            Layer(stick.transform, "Glow", StickSize / JoystickSpriteFactory.GlowPeak,
                  JoystickSpriteFactory.Glow(), RimGlow);

            Layer(stick.transform, "Rim", StickSize, JoystickSpriteFactory.RimRing(), Rim);
            Layer(stick.transform, "Gauge", 214f, JoystickSpriteFactory.HairRing(), Hairline);

            BuildChevrons(stick.transform);

            GameObject handle = Layer(stick.transform, "Handle", 112f,
                                      MissionUIBuilder.EnsureEllipseSprite(), HandleFill);
            RectTransform handleRt = RT(handle);

            Layer(handle.transform, "Collar", 56f, JoystickSpriteFactory.HairRing(), Hairline);
            Layer(handle.transform, "Pip", 12f, MissionUIBuilder.EnsureEllipseSprite(), HandleDot);

            var joystick = stick.AddComponent<TouchJoystick>();

            // 112px thumb inside a 340px base: 1 - (56 / 170) = 0.67, so 0.65 lands the thumb
            // just inside the rim at full tilt instead of hanging off it.
            Wire(joystick,
                ("background", RT(stick)),
                ("handle", handleRt),
                ("deadZone", 0.15f),
                ("handleRange", 0.65f));

            return joystick;
        }

        /// <summary>
        /// The four axis markers. One chevron sprite pointing right, rotated into the other
        /// three, sitting between the gauge circle and the rim where a thumb is not covering
        /// them.
        /// </summary>
        static void BuildChevrons(Transform parent)
        {
            Sprite chevron = JoystickSpriteFactory.Chevron();

            const float radius = 130f;
            const float size = 34f;

            var directions = new (string name, Vector2 offset, float angle)[]
            {
                ("Chevron_N", new Vector2(0f, radius), 90f),
                ("Chevron_E", new Vector2(radius, 0f), 0f),
                ("Chevron_S", new Vector2(0f, -radius), 270f),
                ("Chevron_W", new Vector2(-radius, 0f), 180f),
            };

            foreach ((string name, Vector2 offset, float angle) in directions)
            {
                GameObject go = Rect(name, parent);
                RectTransform rt = Anchor(go, new Vector2(0.5f, 0.5f), new Vector2(size, size), offset);
                rt.localRotation = Quaternion.Euler(0f, 0f, angle);
                AddCircle(go, chevron, Arrow, false);
            }
        }

        /// <summary>One centred, tinted layer of a round control.</summary>
        static GameObject Layer(Transform parent, string name, float size, Sprite sprite, Color tint)
        {
            GameObject go = Rect(name, parent);
            Anchor(go, new Vector2(0.5f, 0.5f), new Vector2(size, size), Vector2.zero);
            AddCircle(go, sprite, tint, false);
            return go;
        }

        static Image AddCircle(GameObject go, Sprite sprite, Color tint, bool raycastTarget)
        {
            Image img = AddImage(go, tint, raycastTarget);
            img.sprite = sprite;
            img.type = Image.Type.Simple;
            return img;
        }

        static void BuildJump(Transform parent)
        {
            GameObject jump = Rect("JumpButton", parent);
            Anchor(jump, BottomRight, ButtonSize, new Vector2(-ButtonInset, ButtonBaseline));
            Face(jump, Hairline);
            jump.AddComponent<JumpButton>();
            Label(jump.transform, "JUMP", 38f);
        }

        static void BuildRun(Transform parent)
        {
            GameObject run = Rect("RunButton", parent);

            // Sits to the left of Jump on the same baseline. Both anchor bottom-right with
            // a bottom-right pivot, so equal Y offsets put their bottom edges on one line.
            float x = ButtonInset + ButtonSize.x + ButtonGap;

            Anchor(run, BottomRight, ButtonSize, new Vector2(-x, ButtonBaseline));

            // Run wears the stick's red: it is the one button that changes how the girl moves,
            // and a red ring reads as "held" at a glance in a way a second white one does not.
            Face(run, Rim);
            run.AddComponent<RunButton>();
            Label(run.transform, "RUN", 38f);
        }

        /// <summary>
        /// A button in the stick's language: the same smoked disc, ringed, with the pressed
        /// state left to the label and the ring colour.
        /// </summary>
        static void Face(GameObject go, Color ring)
        {
            AddCircle(go, MissionUIBuilder.EnsureEllipseSprite(), Smoke, true);
            Layer(go.transform, "Ring", ButtonSize.x, JoystickSpriteFactory.HairRing(), ring);
        }

        static void Label(Transform parent, string text, float size)
        {
            GameObject go = Rect("Label", parent);
            Stretch(go);
            AddText(go, text, size, TextAlignmentOptions.Center, Ink, FontStyles.Bold);
        }

        // The Joystick Pack's PNGs are no longer loaded by anything: every piece of the stick
        // and both buttons are generated white shapes tinted from the palette above, which is
        // what makes the whole control recolour from one place. The pack can stay in the
        // project or go; nothing here reads it.

        // ------------------------------------------------------------------ cleanup

        static void RemoveExisting()
        {
            foreach (GameObject go in Object.FindObjectsByType<GameObject>(
                         FindObjectsInactive.Include))
            {
                if (go != null && go.name == RootName)
                    Undo.DestroyObjectImmediate(go);
            }
        }

        /// <summary>
        /// The scene's EventSystem carries both a StandaloneInputModule and an
        /// InputSystemUIInputModule. Two active modules on one EventSystem fight over the
        /// pointer and deliver duplicate or dropped drag events, which is exactly what a
        /// half-working joystick feels like. Keep the Input System one — it is the module
        /// already wired to the project's actions asset.
        /// </summary>
        static void FixEventSystem()
        {
            EventSystem es = Object.FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include);

            if (es == null)
            {
                Debug.LogWarning("MobileControlsBuilder: no EventSystem in the scene. " +
                                 "The touch controls will not receive input.");
                return;
            }

            BaseInputModule[] modules = es.GetComponents<BaseInputModule>();

            if (modules.Length < 2)
                return;

            foreach (BaseInputModule module in modules)
            {
                if (module is StandaloneInputModule)
                {
                    Undo.DestroyObjectImmediate(module);
                    Debug.Log("Removed the duplicate StandaloneInputModule from the EventSystem.", es);
                }
            }
        }
    }
}
