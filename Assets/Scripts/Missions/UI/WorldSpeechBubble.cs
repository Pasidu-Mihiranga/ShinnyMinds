using TMPro;
using UnityEngine;

namespace ShinyMinds.Missions.UI
{
    /// <summary>
    /// A speech balloon that hangs over whoever is talking, in the same storybook style as
    /// the memory bubble: white paper, dark text, a tail pointing down at the speaker.
    ///
    /// It replaces the subtitle bar for anyone who has a body in the scene. A bar at the
    /// bottom of the screen never says *who* is speaking without printing their name, and
    /// in a scene where a stranger walks up behind a child, which of the two is talking is
    /// the single most important thing on screen.
    ///
    /// The balloon is positioned every frame from the speaker's head projected into canvas
    /// space, clamped to stay fully on screen, and its tail slides along the underside to
    /// keep pointing at the head even when the balloon has been pushed back into view.
    /// </summary>
    public class WorldSpeechBubble : MonoBehaviour
    {
        [SerializeField] GameObject root;
        [Tooltip("The balloon itself — this is what gets moved.")]
        [SerializeField] RectTransform bubble;
        [Tooltip("The space the balloon is positioned in, normally the canvas.")]
        [SerializeField] RectTransform canvasRect;
        [Tooltip("Slides along the balloon's underside to stay over the speaker's head.")]
        [SerializeField] RectTransform tail;
        [Tooltip("The patch hiding the balloon's outline where the tail joins it. Slides " +
                 "with the tail, or it would uncover the outline it exists to hide.")]
        [SerializeField] RectTransform neck;
        [SerializeField] TMP_Text lineText;
        [SerializeField] TypewriterText typewriter;

        [Header("Placement")]
        [Tooltip("Metres above the speaker's transform to aim at. Pivots are at the feet, " +
                 "so this is roughly their height.")]
        [SerializeField] float headHeight = 1.7f;
        [Tooltip("Canvas pixels between the head and the bottom of the balloon.")]
        [SerializeField] float headroom = 40f;
        [Tooltip("Kept this far inside the screen edges.")]
        [SerializeField] float screenMargin = 28f;
        [Tooltip("How far the tail may slide from the balloon's centre, as a fraction of " +
                 "its half width. Past this it would fall off the rounded corner.")]
        [SerializeField] float tailTravel = 0.6f;

        Transform speaker;
        Camera worldCamera;
        float tailY;
        float neckY;

        public TypewriterText Typewriter => typewriter;

        void Awake()
        {
            if (tail != null) tailY = tail.anchoredPosition.y;
            if (neck != null) neckY = neck.anchoredPosition.y;
        }

        public void Show(string text, Transform who, Camera camera)
        {
            speaker = who;
            worldCamera = camera;

            if (lineText != null) lineText.text = text;
            if (root != null) root.SetActive(true);

            // Place it before the first frame is drawn, or the balloon appears at wherever
            // the last speaker was standing and slides across.
            Reposition();
        }

        public void Hide()
        {
            speaker = null;
            if (root != null) root.SetActive(false);
        }

        // LateUpdate: the camera director moves its shots in LateUpdate too, and reading a
        // camera pose before it settles leaves the balloon lagging a frame behind the cut.
        void LateUpdate()
        {
            if (speaker != null) Reposition();
        }

        void Reposition()
        {
            if (speaker == null || worldCamera == null || bubble == null || canvasRect == null)
                return;

            Vector3 head = speaker.position + Vector3.up * headHeight;
            Vector3 screen = worldCamera.WorldToScreenPoint(head);

            // Behind the lens, WorldToScreenPoint mirrors the point through the centre,
            // which would fling the balloon to the opposite side of the screen.
            if (screen.z <= 0f)
                return;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect, screen, null, out Vector2 headPoint))
                return;

            Rect canvas = canvasRect.rect;
            Rect self = bubble.rect;

            float halfW = self.width * 0.5f;
            float halfH = self.height * 0.5f;

            Vector2 wanted = new Vector2(headPoint.x, headPoint.y + headroom + halfH);

            float limitX = canvas.width * 0.5f - halfW - screenMargin;
            float limitY = canvas.height * 0.5f - halfH - screenMargin;

            // A speaker near the top of the frame would push the balloon off it, so the
            // balloon stops at the edge and the tail takes up the difference.
            Vector2 placed = new Vector2(
                Mathf.Clamp(wanted.x, -limitX, limitX),
                Mathf.Clamp(wanted.y, -limitY, limitY));

            bubble.anchoredPosition = placed;

            float slide = Mathf.Clamp(headPoint.x - placed.x, -halfW * tailTravel, halfW * tailTravel);

            if (tail != null) tail.anchoredPosition = new Vector2(slide, tailY);
            if (neck != null) neck.anchoredPosition = new Vector2(slide, neckY);
        }
    }
}
