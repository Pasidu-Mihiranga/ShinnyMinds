using System.Collections;
using UnityEngine;

namespace ShinyMinds.Missions.Data
{
    /// <summary>
    /// Everything a CutsceneAction is allowed to touch. Keeping the surface this
    /// narrow is what lets mission content live in ScriptableObjects while the
    /// scene-side implementations stay in the Runtime namespace.
    /// </summary>
    public interface IMissionContext
    {
        IMissionActor GetActor(string key);
        Transform GetMarker(string key);

        IMissionUi Ui { get; }
        IMissionCamera Camera { get; }
        AudioSource Sfx { get; }
        Transform Player { get; }

        void SetFlag(string key, bool value);
        bool GetFlag(string key);
    }

    public interface IMissionActor
    {
        string ActorKey { get; }
        Transform Transform { get; }
        GameObject GameObject { get; }
        Animator Animator { get; }
        IActorMover Mover { get; }

        void TeleportTo(Vector3 position, Quaternion rotation, bool snapToGround);
        void ResetToSpawn();
    }

    public interface IActorMover
    {
        bool IsMoving { get; }
        IEnumerator MoveTo(Vector3 target, bool run, bool backwards = false);
        IEnumerator FaceTowards(Vector3 worldPoint, float seconds);
        IEnumerator FaceDirection(Vector3 direction, float seconds);
        void Stop();
        void SnapDown();
    }

    public interface IMissionUi
    {
        IEnumerator Fade(bool toBlack, float seconds, float holdSeconds = 0f);
        IEnumerator SetLetterbox(bool on, float seconds);
        void SetObjective(string text);
    }

    public interface IMissionCamera
    {
        bool HasControl { get; }

        /// <summary>Vertical field of view, needed to compute framing distances.</summary>
        float FieldOfView { get; }
        float Aspect { get; }

        IEnumerator ShotTo(Transform marker, float blendSeconds, Transform lookAt, bool letterbox);

        /// <summary>
        /// Move to a pose computed at runtime rather than authored on a marker. This is
        /// what lets a shot frame the actors wherever they actually ended up.
        /// </summary>
        IEnumerator ShotToPose(Vector3 position, Quaternion rotation, float blendSeconds,
                               Transform lookAt, bool letterbox);

        /// <summary>Dolly along the view axis. Positive pushes in.</summary>
        IEnumerator PushIn(float distance, float seconds);

        /// <summary>
        /// Start a dolly and return immediately, so the move continues underneath the
        /// dialogue that follows. A cutscene node joins its parallel actions before it
        /// ends, so a blocking push would stall the line it is meant to underscore.
        /// </summary>
        void BeginPushIn(float distance, float seconds);

        /// <summary>Additive positional shake, applied after the shot pose.</summary>
        void Shake(float amplitude, float duration);

        IEnumerator Release(float blendSeconds);
        void HardRelease();
    }
}
