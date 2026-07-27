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
        IEnumerator ShotTo(Transform marker, float blendSeconds, Transform lookAt, bool letterbox);
        IEnumerator Release(float blendSeconds);
        void HardRelease();
    }
}
