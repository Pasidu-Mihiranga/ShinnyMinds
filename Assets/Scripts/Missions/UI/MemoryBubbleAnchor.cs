using UnityEngine;

namespace ShinyMinds.Missions.UI
{
    /// <summary>
    /// Sizes the memory oval, and for a thought puts it beside the person thinking it with
    /// its trail of dots running back to their head.
    ///
    /// A thought can happen anywhere — Aisha thinks it wherever she is standing — and three
    /// dots authored to one corner point at empty tarmac as soon as she moves, which leaves
    /// the bubble reading as a box of text belonging to nobody. So a thought's oval is placed
    /// every frame from the head, the same way <see cref="WorldSpeechBubble"/> aims a speech
    /// balloon.
    ///
    /// A **remembered scene keeps the staging it was authored with**: parked in the top-right
    /// corner at full size with the trail exactly as placed in the prefab. Mission 01's
    /// memory plays at the school gate with the girl standing still below it, so it was
    /// already pointing at her, and a memory has a lot more to hold than a thought does.
    ///
    /// The oval is sized here too, because the two things it holds are not the same size: a
    /// remembered scene needs room for a 16:9 render and two stand-ins, and a thought is one
    /// line in one balloon.
    /// </summary>
    public class MemoryBubbleAnchor : MonoBehaviour
    {
        [Tooltip("The oval itself — this is what gets moved and resized.")]
        [SerializeField] RectTransform bubble;
        [Tooltip("The space the oval is positioned in, normally the full-screen memory panel.")]
        [SerializeField] RectTransform canvasRect;
        [Tooltip("Biggest first: the trail steps out from the oval's rim and shrinks as it " +
                 "reaches the head. Their positions in the prefab are the authored pose a " +
                 "memory keeps; a thought's are computed.")]
        [SerializeField] RectTransform[] dots = new RectTransform[0];

        [Header("Size")]
        [Tooltip("With the stage inside it: a 16:9 render, two stand-ins and their balloons.")]
        [SerializeField] Vector2 memorySize = new Vector2(860f, 580f);
        [Tooltip("A thought is one balloon on empty paper, so the oval only has to hold that. " +
                 "Shrink it here and ThoughtBalloon's width in MissionUIBuilder together — an " +
                 "ellipse cuts the balloon's corners first.")]
        [SerializeField] Vector2 thoughtSize = new Vector2(440f, 250f);

        [Header("Placement")]
        [Tooltip("Metres above the transform to aim at. Pivots are at the feet, so this is " +
                 "roughly their height.")]
        [SerializeField] float headHeight = 1.7f;
        [Tooltip("Canvas pixels of clear space between the head and the oval's rim.")]
        [SerializeField] float headroom = 90f;
        [Tooltip("How far the oval leans to one side of the head, as a fraction of its half " +
                 "width. It leans towards the middle of the screen: on the near side there " +
                 "is no room for it.")]
        [SerializeField] float sideLean = 0.55f;
        [SerializeField] float screenMargin = 24f;
        [Tooltip("Clear space between the last dot and the head. Dots landing on a face read " +
                 "as a rash rather than as thinking.")]
        [SerializeField] float dotHeadroom = 28f;
        [Tooltip("Inset from the top-right corner. Where a memory always sits, and where a " +
                 "thought falls back to when there is nobody on screen to point it at.")]
        [SerializeField] float parkedInset = 45f;

        Transform owner;
        Camera worldCamera;
        Vector2[] dotRest;

        /// <summary>Sizes the oval for what is going in it and places it.</summary>
        /// <param name="who">
        /// The person a thought belongs to. Ignored for a memory, which stays in its corner.
        /// </param>
        public void Attach(Transform who, Camera camera, bool withStage)
        {
            // Lazily, because the panel is inactive until the first memory opens, so Awake
            // has not necessarily run — and this has to happen before anything moves a dot.
            CaptureDotRest();

            if (bubble != null)
                bubble.sizeDelta = withStage ? memorySize : thoughtSize;

            if (withStage)
            {
                // A memory keeps its authored staging: corner, full size, trail as placed.
                owner = null;
                worldCamera = null;

                Park();
                RestoreDots();
                return;
            }

            owner = who;
            worldCamera = camera;

            // Now, not on the first LateUpdate: the oval swells as it opens, and swelling in
            // the wrong place and then jumping is worse than not moving at all.
            Reposition();
        }

        public void Detach()
        {
            owner = null;
            worldCamera = null;
            SetDots(false);
        }

        // LateUpdate: the camera director moves its shots in LateUpdate too, and reading a
        // camera pose before it settles leaves the trail lagging a frame behind the cut.
        void LateUpdate()
        {
            if (owner != null)
                Reposition();
        }

        void Reposition()
        {
            if (bubble == null || canvasRect == null)
                return;

            Rect canvas = canvasRect.rect;
            Vector2 half = bubble.rect.size * 0.5f;

            if (!HeadPoint(out Vector2 head))
            {
                // A trail with nothing on the end of it is worse than no trail.
                Park();
                SetDots(false);
                return;
            }

            float lean = head.x <= 0f ? 1f : -1f;

            Vector2 wanted = new Vector2(
                head.x + lean * half.x * sideLean,
                head.y + headroom + half.y);

            Vector2 limit = new Vector2(
                Mathf.Max(0f, canvas.width * 0.5f - half.x - screenMargin),
                Mathf.Max(0f, canvas.height * 0.5f - half.y - screenMargin));

            // Someone standing near the top of the frame would push the oval off it, so the
            // oval stops at the edge and the trail takes up the difference.
            Vector2 placed = new Vector2(
                Mathf.Clamp(wanted.x, -limit.x, limit.x),
                Mathf.Clamp(wanted.y, -limit.y, limit.y));

            bubble.anchoredPosition = placed;

            LayOutDots(head - placed, half);
        }

        bool HeadPoint(out Vector2 point)
        {
            point = Vector2.zero;

            if (owner == null || worldCamera == null)
                return false;

            Vector3 screen = worldCamera.WorldToScreenPoint(owner.position + Vector3.up * headHeight);

            // Behind the lens, WorldToScreenPoint mirrors the point through the centre, which
            // would fling the oval to the opposite side of the screen.
            if (screen.z <= 0f)
                return false;

            return RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, screen, null, out point);
        }

        /// <summary>The top-right corner, which is where the oval sat before it learnt to move.</summary>
        void Park()
        {
            if (bubble == null || canvasRect == null)
                return;

            Rect canvas = canvasRect.rect;
            Vector2 half = bubble.rect.size * 0.5f;

            bubble.anchoredPosition = new Vector2(
                canvas.width * 0.5f - half.x - parkedInset,
                canvas.height * 0.5f - half.y - parkedInset);
        }

        void CaptureDotRest()
        {
            int count = dots != null ? dots.Length : 0;

            if (dotRest != null && dotRest.Length == count)
                return;

            dotRest = new Vector2[count];

            for (int i = 0; i < count; i++)
                dotRest[i] = dots[i] != null ? dots[i].anchoredPosition : Vector2.zero;
        }

        /// <summary>Puts the trail back where the prefab placed it.</summary>
        void RestoreDots()
        {
            if (dots == null || dotRest == null)
                return;

            for (int i = 0; i < dots.Length && i < dotRest.Length; i++)
            {
                if (dots[i] == null) continue;

                dots[i].anchoredPosition = dotRest[i];
                dots[i].gameObject.SetActive(true);
            }
        }

        /// <summary>
        /// <paramref name="toHead"/> is the head measured from the oval's centre, which is
        /// also the space the dots live in: they are centre-anchored children of the oval, so
        /// they swell with it as the memory opens.
        /// </summary>
        void LayOutDots(Vector2 toHead, Vector2 half)
        {
            if (dots == null || dots.Length == 0)
                return;

            float distance = toHead.magnitude;

            if (distance < 1f)
            {
                SetDots(false);
                return;
            }

            Vector2 direction = toHead / distance;

            // Where the line out to the head leaves the paper. An ellipse, so this is not
            // simply half the width.
            float rim = EllipseRadius(direction, half);
            float span = distance - rim - dotHeadroom;

            // The head is inside the oval, or close enough that the dots would sit on it.
            if (span <= 0f)
            {
                SetDots(false);
                return;
            }

            for (int i = 0; i < dots.Length; i++)
            {
                if (dots[i] == null)
                    continue;

                // Spread across the gap with a half-step at each end, so the first dot clears
                // the rim and the last stops short of the head.
                float t = (i + 0.5f) / dots.Length;

                dots[i].gameObject.SetActive(true);
                dots[i].anchoredPosition = direction * (rim + span * t);
            }
        }

        /// <summary>Distance from the centre to the rim of a half-(x,y) ellipse, along a unit direction.</summary>
        static float EllipseRadius(Vector2 direction, Vector2 half)
        {
            float x = direction.x / Mathf.Max(1f, half.x);
            float y = direction.y / Mathf.Max(1f, half.y);

            return 1f / Mathf.Max(0.0001f, Mathf.Sqrt(x * x + y * y));
        }

        void SetDots(bool visible)
        {
            if (dots == null)
                return;

            for (int i = 0; i < dots.Length; i++)
            {
                if (dots[i] != null && dots[i].gameObject.activeSelf != visible)
                    dots[i].gameObject.SetActive(visible);
            }
        }
    }
}
