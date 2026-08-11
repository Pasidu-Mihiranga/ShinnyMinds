using System;
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
    /// The stick and both buttons are drawn as black frosted glass — a smoked disc under a
    /// mottled milky haze, with an etched gauge and four chevrons — from generated
    /// white shapes tinted at the Image. Nothing about how they read input changed: TouchJoystick
    /// still owns the drag, MobileInput still samples it, and every layer is raycast-transparent
    /// so the base disc alone catches the finger.
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

        /// <summary>
        /// Printed on every rebuild. Unity runs a menu item against the assembly it has already
        /// compiled, not the file on disk, so clicking Rebuild before a recompile finishes
        /// silently builds the *previous* design — which looks exactly like the change not
        /// working. If the Console does not name the change you just made, Unity had not
        /// compiled it yet: give the editor focus, wait for the spinner, and run it again.
        /// </summary>
        const string BuildStamp =
            "matte black glass; supplied run/jump artwork used whole, exactly as drawn";

        // Landscape, matching the game's aspect. The old canvas used a 1080x1920 portrait
        // reference, which shrank every control to about two thirds on a wide screen.
        static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

        // Jump sits in the corner under the resting thumb; Run is one step up and one step in
        // from it, so the pair lies along the arc a right thumb actually sweeps rather than in
        // a row it has to slide sideways along.
        static readonly Vector2 ButtonSize = new Vector2(180f, 180f);
        const float ButtonBaseline = 90f;   // Jump's bottom edge, up from the screen bottom
        const float ButtonInset = 70f;      // Jump's right edge, in from the screen right

        // Just under a full button in each axis. It puts the centres 243 apart on the diagonal
        // against the 180 they would need merely to touch, so the gap is clear without throwing
        // Run so far up the screen that the thumb has to leave the cluster to reach it.
        const float DiagonalStep = 172f;

        static readonly Vector2 BottomRight = new Vector2(1f, 0f);

        // Black frosted glass, and black is the whole palette: every value below is black at a
        // different density. Nothing is opaque — you can always see the road through it, which
        // is the point of glass — and nothing is lighter than what is behind it, which is what
        // keeps it matte.
        //
        // The frost in particular is tinted DOWN. A white haze is the obvious way to draw
        // ground glass and it works over the road, but the moment the control sits on something
        // pale it reads as a smear of polish across the disc: shine, by another name. Tinted
        // black it still mottles the glass and adds no light anywhere.
        //
        // Nothing is drawn outside the base circle either: no halo, no bloom, no contact
        // shadow. A soft edge spilling onto the road is read as a glow, however dark it is.
        //
        // Everything is a tint on a white generated sprite, so the control recolours from these
        // six values alone.
        static readonly Color Glass = new Color(0.01f, 0.01f, 0.02f, 0.44f);
        static readonly Color GlassRim = new Color(0.00f, 0.00f, 0.00f, 0.60f);
        static readonly Color Frost = new Color(0.00f, 0.00f, 0.02f, 0.13f);
        static readonly Color Etch = new Color(0.00f, 0.00f, 0.00f, 0.38f);
        static readonly Color HandleGlass = new Color(0.00f, 0.00f, 0.00f, 0.50f);
        static readonly Color HandleEtch = new Color(0.00f, 0.00f, 0.00f, 0.42f);

        // The button glyphs are the one thing that must stay readable whatever is behind the
        // glass, so they are light with a dark keyline rather than another shade of black.
        static readonly Color IconInk = new Color(0.97f, 0.98f, 1.00f, 0.92f);
        static readonly Color IconEdge = new Color(0.00f, 0.00f, 0.00f, 0.85f);

        /// <summary>The filled circle behind every piece of glass.</summary>
        static Sprite Disc() => MissionUIBuilder.EnsureEllipseSprite();

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

            // A code-created Canvas omits the extra shader channels TMP needs, and any text on
            // it renders muddy and dark whatever colour is set. There is none here now that the
            // buttons carry icons, but the next person to add a label should not have to
            // rediscover this. Same fix MissionUIBuilder applies.
            MissionUIBuilder.ApplyTextShaderChannels(canvas);

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

            Debug.Log($"Touch controls rebuilt — {BuildStamp}. Stick bottom left; Jump in the " +
                      "corner with Run diagonally above it.", root);
        }

        // ------------------------------------------------------------------ pieces

        const float StickSize = 340f;
        const float HandleSize = 112f;

        /// <summary>
        /// The stick, drawn as a disc of black frosted glass: smoked fill, a dark frost, a black
        /// bezel, an etched gauge circle, four chevrons marking the axes, and a thumb of the same
        /// glass with its own frost. Matte throughout, and every layer inside the same circle —
        /// no highlight, no halo.
        ///
        /// Built as separate tinted layers rather than one baked sprite so the palette above is
        /// the only thing to edit, and drawn in child order: frost, bezel, gauge, chevrons,
        /// thumb on top. The disc that catches the touch is the Stick's own Image, so
        /// the touch area and the visible glass are the same circle and cannot drift apart —
        /// and every layer over it has its raycast target OFF, because a child that swallowed
        /// the pointer would cancel the drag under the finger.
        /// </summary>
        static TouchJoystick BuildStick(Transform parent)
        {
            GameObject stick = Rect("Stick", parent);
            Anchor(stick, new Vector2(0f, 0f), new Vector2(StickSize, StickSize), new Vector2(60f, 60f));
            AddCircle(stick, Disc(), Glass, true);

            // Nothing is drawn outside this circle. There was a soft dark halo here, spilling
            // to 1.39x the disc to give black glass an edge over a dark road; over pale ground
            // it read as a glow around the control, which is not what a sheet of glass does.
            // The bezel carries the edge on its own now.
            Frosting(stick.transform, StickSize);

            Layer(stick.transform, "Bezel", StickSize, JoystickSpriteFactory.RimRing(), GlassRim);
            Layer(stick.transform, "Gauge", 214f, JoystickSpriteFactory.HairRing(), Etch);

            BuildChevrons(stick.transform);

            GameObject handle = Layer(stick.transform, "Handle", HandleSize, Disc(), HandleGlass);
            RectTransform handleRt = RT(handle);

            // Its own frost, so the thumb is a separate piece of glass sitting on the first
            // rather than a hole in it.
            Frosting(handle.transform, HandleSize);

            Layer(handle.transform, "Collar", 56f, JoystickSpriteFactory.HairRing(), HandleEtch);
            Layer(handle.transform, "Pip", 12f, Disc(), HandleEtch);

            var joystick = stick.AddComponent<TouchJoystick>();

            // 112px thumb inside a 340px base: 1 - (56 / 170) = 0.67, so 0.65 lands the thumb
            // just inside the bezel at full tilt instead of hanging off it.
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
                AddCircle(go, chevron, Etch, false);
            }
        }

        /// <summary>
        /// What makes a dark disc frosted glass: a mottled milky haze, masked to the same circle
        /// as the fill it sits on.
        ///
        /// There was a specular highlight raking in from the upper left here as well. It was
        /// removed on purpose — it reads as polished glass, and this control is meant to be
        /// ground matte. Don't put it back.
        /// </summary>
        static void Frosting(Transform parent, float size)
        {
            Layer(parent, "Frost", size, JoystickSpriteFactory.Frost(), Frost);
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
            Face(jump);
            jump.AddComponent<JumpButton>();
            Badge(jump.transform, JumpArt, JoystickSpriteFactory.JumpIcon);
        }

        static void BuildRun(Transform parent)
        {
            GameObject run = Rect("RunButton", parent);

            // Up and to the left of Jump, on the diagonal. Both anchor bottom-right with a
            // bottom-right pivot, so one step in each axis is all it takes.
            Anchor(run, BottomRight, ButtonSize,
                   new Vector2(-(ButtonInset + DiagonalStep), ButtonBaseline + DiagonalStep));

            Face(run);
            run.AddComponent<RunButton>();
            Badge(run.transform, RunArt, JoystickSpriteFactory.RunIcon);
        }

        // The supplied artwork, rasterised whole from jump.svg / run.svg at 512px — Unity has no
        // SVG importer without com.unity.vectorgraphics, so the SVGs at the project root are
        // inert. Each is a finished badge: a grey disc inside a ring with the figure knocked out
        // of the disc, and it goes on the button exactly as drawn.
        const string JumpArt = "Assets/Art/UI/Icon_Jump.png";
        const string RunArt = "Assets/Art/UI/Icon_Run.png";

        /// <summary>
        /// Puts the artwork on a button, or the generated glyph if the artwork is not in the
        /// project — a clone missing the PNGs should still build usable buttons rather than two
        /// blank discs. The two are drawn differently on purpose: see <see cref="Art"/>.
        /// </summary>
        static void Badge(Transform parent, string path, Func<Sprite> generated)
        {
            Sprite art = JoystickSpriteFactory.Imported(path);

            if (art != null)
            {
                Art(parent, art);
                return;
            }

            Debug.LogWarning($"MobileControlsBuilder: no icon at '{path}'. Falling back to the " +
                             "generated glyph.");

            Glyph(parent, generated());
        }

        /// <summary>
        /// A button in the stick's language: the same black glass, frosted, with a hairline
        /// bezel. Uniform on purpose — the label says which one it is, and a second accent
        /// colour down there would compete with the stick for the eye.
        /// </summary>
        static void Face(GameObject go)
        {
            AddCircle(go, Disc(), Glass, true);
            Frosting(go.transform, ButtonSize.x);
            Layer(go.transform, "Bezel", ButtonSize.x, JoystickSpriteFactory.HairRing(), GlassRim);
        }

        const float IconSize = 88f;

        /// <summary>
        /// The supplied artwork, drawn exactly as designed: the full badge at the button's own
        /// size, tinted pure white so its greys come through untouched, and with no outline
        /// added. The ring lands on the button's rim, and the figure is negative space, so the
        /// frosted glass behind shows through it.
        ///
        /// It is deliberately NOT given the keyline the generated glyph gets. A keyline would be
        /// a change to the design, and the design is not ours to change.
        /// </summary>
        static void Art(Transform parent, Sprite sprite)
        {
            Layer(parent, "Icon", ButtonSize.x, sprite, Color.white);
        }

        /// <summary>
        /// The fallback glyph, used only when the artwork is missing from the project: a symbol
        /// is read at a glance by a thumb already moving, and it needs no translating for a child
        /// who reads English slowly or not at all.
        ///
        /// White with a black keyline — the same pairing the speech balloons use — because the
        /// glass is translucent: it goes pale over a wall and near-black over the road, and a
        /// glyph in any single flat colour disappears over one or the other.
        /// </summary>
        static void Glyph(Transform parent, Sprite sprite)
        {
            GameObject go = Layer(parent, "Icon", IconSize, sprite, IconInk);

            var outline = go.AddComponent<Outline>();
            outline.effectColor = IconEdge;
            outline.effectDistance = new Vector2(2.5f, 2.5f);
        }

        // The Joystick Pack's PNGs are no longer loaded by anything: every piece of the stick
        // and both buttons are generated white shapes tinted from the palette above, which is
        // what makes the whole control recolour from one place. The pack can stay in the
        // project or go; nothing here reads it.

        // ------------------------------------------------------------------ cleanup

        static void RemoveExisting()
        {
            // Qualified: `using System` makes a bare Object ambiguous with System.Object.
            foreach (GameObject go in UnityEngine.Object.FindObjectsByType<GameObject>(
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
            EventSystem es =
                UnityEngine.Object.FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include);

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
