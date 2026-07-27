using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ShinyMinds.Missions.Data
{
    public enum ShotType
    {
        /// <summary>Both subjects in frame, camera off their shared axis.</summary>
        TwoShot,
        /// <summary>Behind the first subject's shoulder, looking at the second.</summary>
        OverShoulder,
        /// <summary>Tight on a single subject's face.</summary>
        CloseUp,
        /// <summary>Pulled back. Reads as open and unthreatening.</summary>
        Wide,
    }

    /// <summary>
    /// Computes a camera pose from where the actors actually are, instead of moving to a
    /// fixed marker.
    ///
    /// A marker cannot know that the Stranger stopped short of his mark, or that the
    /// player walked in from a different angle, so authored poses routinely end up
    /// pointing at empty pavement. Deriving the pose from the subjects fixes that for
    /// every beat at once, and removes most of the marker-placement work.
    /// </summary>
    [Serializable]
    public class FramedShotAction : CutsceneAction
    {
        [Tooltip("Actors to keep in frame. TwoShot/OverShoulder use the first two; " +
                 "CloseUp uses the first.")]
        public List<string> subjectActorKeys = new List<string>();

        public ShotType shotType = ShotType.TwoShot;

        [Header("Framing")]
        [Tooltip("Degrees off the subjects' shared axis. 0 would be a flat side-on view; " +
                 "30-45 reads naturally.")]
        [Range(-89f, 89f)] public float angleDegrees = 35f;

        [Tooltip("Height above the actor pivot to aim at. Roughly eye level.")]
        public float heightOffset = 1.5f;

        [Tooltip("Extra room around the subjects. 1 = exactly filling the frame.")]
        [Range(1f, 3f)] public float margin = 1.35f;

        [Tooltip("Raise or lower the camera relative to the aim point. Negative looks up " +
                 "at the subject, which reads as intimidating.")]
        public float cameraHeightBias = 0.25f;

        public float minDistance = 1.6f;
        public float maxDistance = 22f;

        [Header("Blend")]
        public float blendSeconds = 1f;
        [Tooltip("Keep tracking this actor after the blend. Empty = hold the fixed pose.")]
        public string trackActorKey;
        public bool letterbox = true;

        [Header("Obstruction")]
        [Tooltip("Pull the camera in when scenery blocks the view. Exclude CameraIgnore " +
                 "so the actors themselves never count as obstructions.")]
        public LayerMask obstructionMask = ~0;
        public float obstructionRadius = 0.35f;

        public override IEnumerator Execute(IMissionContext ctx)
        {
            if (ctx.Camera == null || subjectActorKeys == null || subjectActorKeys.Count == 0)
            {
                Debug.LogWarning("FramedShotAction: no camera or no subjects.");
                yield break;
            }

            var points = new List<Vector3>();
            foreach (string key in subjectActorKeys)
            {
                IMissionActor a = ctx.GetActor(key);
                if (a != null) points.Add(a.Transform.position + Vector3.up * heightOffset);
            }

            if (points.Count == 0)
            {
                Debug.LogWarning($"FramedShotAction: none of [{string.Join(", ", subjectActorKeys)}] are registered.");
                yield break;
            }

            IMissionActor first = ctx.GetActor(subjectActorKeys[0]);
            Vector3 aim = Centroid(points);
            Vector3 position = Solve(ctx, points, first, aim);

            position = AvoidObstruction(position, aim);

            Quaternion rotation = Quaternion.LookRotation((aim - position).normalized, Vector3.up);

            Transform track = null;
            if (!string.IsNullOrEmpty(trackActorKey))
            {
                IMissionActor t = ctx.GetActor(trackActorKey);
                if (t != null) track = t.Transform;
            }

            yield return ctx.Camera.ShotToPose(position, rotation, blendSeconds, track, letterbox);
        }

        Vector3 Solve(IMissionContext ctx, List<Vector3> points, IMissionActor first, Vector3 aim)
        {
            float vFov = ctx.Camera.FieldOfView;
            float aspect = ctx.Camera.Aspect;

            switch (shotType)
            {
                case ShotType.OverShoulder when points.Count >= 2:
                {
                    // Sit behind and beside subject A, looking past them to subject B.
                    Vector3 a = points[0];
                    Vector3 b = points[1];
                    Vector3 toB = b - a; toB.y = 0f;

                    if (toB.sqrMagnitude < 0.0001f) toB = Vector3.forward;
                    toB.Normalize();

                    Vector3 side = Vector3.Cross(Vector3.up, toB);
                    Vector3 pos = a - toB * 1.1f + side * 0.65f;
                    pos.y = a.y + cameraHeightBias;
                    return pos;
                }

                case ShotType.CloseUp:
                {
                    // In front of the subject's face, offset so it is not a flat mugshot.
                    Vector3 fwd = first != null ? first.Transform.forward : Vector3.forward;
                    fwd.y = 0f;
                    if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
                    fwd.Normalize();

                    Vector3 dir = Quaternion.AngleAxis(angleDegrees, Vector3.up) * fwd;
                    float dist = Mathf.Clamp(1.1f * margin, minDistance, maxDistance);

                    Vector3 pos = aim + dir * dist;
                    pos.y = aim.y + cameraHeightBias;
                    return pos;
                }

                default:
                {
                    // TwoShot and Wide: stand off the subjects' shared axis far enough
                    // that the horizontal spread fits inside the frustum.
                    Vector3 axis;

                    if (points.Count >= 2)
                    {
                        axis = points[1] - points[0];
                        axis.y = 0f;
                    }
                    else
                    {
                        axis = first != null ? first.Transform.right : Vector3.right;
                    }

                    if (axis.sqrMagnitude < 0.0001f) axis = Vector3.right;
                    axis.Normalize();

                    Vector3 perpendicular = Vector3.Cross(Vector3.up, axis);
                    Vector3 dir = Quaternion.AngleAxis(angleDegrees, Vector3.up) * perpendicular;

                    float spread = Spread(points, axis);
                    float effectiveMargin = shotType == ShotType.Wide ? margin * 2.2f : margin;

                    // Horizontal FOV from the vertical one, then the distance at which
                    // `spread` subtends it.
                    float hFovRad = 2f * Mathf.Atan(Mathf.Tan(vFov * 0.5f * Mathf.Deg2Rad) * aspect);
                    float needed = (spread * 0.5f * effectiveMargin) / Mathf.Max(0.05f, Mathf.Tan(hFovRad * 0.5f));

                    // A pair standing close together still needs breathing room.
                    needed = Mathf.Max(needed, 2.2f * effectiveMargin);

                    float dist = Mathf.Clamp(needed, minDistance, maxDistance);

                    Vector3 pos = aim + dir * dist;
                    pos.y = aim.y + cameraHeightBias + (shotType == ShotType.Wide ? 1.2f : 0f);
                    return pos;
                }
            }
        }

        /// <summary>Pull the camera in if scenery sits between it and the subjects.</summary>
        Vector3 AvoidObstruction(Vector3 position, Vector3 aim)
        {
            Vector3 toCamera = position - aim;
            float distance = toCamera.magnitude;

            if (distance < 0.01f)
                return position;

            Vector3 dir = toCamera / distance;

            if (Physics.SphereCast(aim, obstructionRadius, dir, out RaycastHit hit,
                                   distance, obstructionMask, QueryTriggerInteraction.Ignore))
            {
                float safe = Mathf.Max(minDistance, hit.distance - obstructionRadius);
                return aim + dir * safe;
            }

            return position;
        }

        static Vector3 Centroid(List<Vector3> points)
        {
            Vector3 sum = Vector3.zero;
            foreach (Vector3 p in points) sum += p;
            return sum / points.Count;
        }

        /// <summary>Extent of the subjects measured along the framing axis.</summary>
        static float Spread(List<Vector3> points, Vector3 axis)
        {
            float min = float.MaxValue;
            float max = float.MinValue;

            foreach (Vector3 p in points)
            {
                float d = Vector3.Dot(p, axis);
                if (d < min) min = d;
                if (d > max) max = d;
            }

            return Mathf.Max(0f, max - min);
        }
    }
}
