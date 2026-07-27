using System.Collections;
using ShinyMinds.Missions.Data;
using UnityEngine;

namespace ShinyMinds.Missions.Runtime
{
    /// <summary>
    /// Frames cutscene shots with a dedicated camera.
    ///
    /// Main Camera is a grandchild of GIRL 1, so moving the player during a cutscene
    /// would drag it, and CameraCollision would fight any pose we set. A separate
    /// root-level CutsceneCamera avoids both. Only the Camera *component* is toggled,
    /// never the GameObject — Main Camera carries the AudioListener.
    /// </summary>
    public class MissionCameraDirector : MonoBehaviour, IMissionCamera
    {
        [SerializeField] Camera mainCamera;
        [Tooltip("Root-level camera with its Camera component disabled and NO AudioListener.")]
        [SerializeField] Camera cutsceneCamera;
        [SerializeField] UI.MissionUIView ui;

        [Header("Look-at")]
        [Tooltip("Aim above the pivot so shots frame the head, not the feet.")]
        [SerializeField] Vector3 lookAtOffset = new Vector3(0f, 1.4f, 0f);

        Transform activeLookAt;
        bool hasControl;

        public bool HasControl => hasControl;

        void Awake()
        {
            if (mainCamera == null) mainCamera = Camera.main;

            if (cutsceneCamera != null)
            {
                cutsceneCamera.enabled = false;

                // A second AudioListener spams warnings and halves the mix.
                AudioListener stray = cutsceneCamera.GetComponent<AudioListener>();
                if (stray != null) stray.enabled = false;
            }
        }

        void LateUpdate()
        {
            if (!hasControl || activeLookAt == null || cutsceneCamera == null)
                return;

            // Keep tracking a walking actor after the blend finishes.
            AimAt(activeLookAt);
        }

        public IEnumerator ShotTo(Transform marker, float blendSeconds, Transform lookAt, bool letterbox)
        {
            if (marker == null || cutsceneCamera == null)
                yield break;

            TakeOver();
            activeLookAt = lookAt;

            if (letterbox && ui != null)
                yield return ui.SetLetterbox(true, Mathf.Min(0.4f, Mathf.Max(blendSeconds, 0.01f)));

            Transform cam = cutsceneCamera.transform;

            if (blendSeconds <= 0f)
            {
                cam.SetPositionAndRotation(marker.position, marker.rotation);
                if (lookAt != null) AimAt(lookAt);
                yield break;
            }

            Vector3 fromPos = cam.position;
            Quaternion fromRot = cam.rotation;

            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / blendSeconds;
                float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));

                cam.position = Vector3.Lerp(fromPos, marker.position, e);

                Quaternion targetRot = lookAt != null
                    ? Quaternion.LookRotation((lookAt.position + lookAtOffset) - cam.position, Vector3.up)
                    : marker.rotation;

                cam.rotation = Quaternion.Slerp(fromRot, targetRot, e);
                yield return null;
            }

            cam.position = marker.position;
            if (lookAt != null) AimAt(lookAt); else cam.rotation = marker.rotation;
        }

        public IEnumerator Release(float blendSeconds)
        {
            if (!hasControl || cutsceneCamera == null || mainCamera == null)
            {
                HardRelease();
                yield break;
            }

            activeLookAt = null;
            Transform cam = cutsceneCamera.transform;
            Transform target = mainCamera.transform;

            if (blendSeconds > 0f)
            {
                Vector3 fromPos = cam.position;
                Quaternion fromRot = cam.rotation;

                float t = 0f;
                while (t < 1f)
                {
                    t += Time.deltaTime / blendSeconds;
                    float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));

                    // Target keeps moving with the player, so re-read it every frame.
                    cam.position = Vector3.Lerp(fromPos, target.position, e);
                    cam.rotation = Quaternion.Slerp(fromRot, target.rotation, e);
                    yield return null;
                }
            }

            if (ui != null)
                yield return ui.SetLetterbox(false, 0.3f);

            HardRelease();
        }

        /// <summary>Instantly hand rendering back. Safe to call when not in control.</summary>
        public void HardRelease()
        {
            activeLookAt = null;
            hasControl = false;

            if (cutsceneCamera != null) cutsceneCamera.enabled = false;

            if (mainCamera != null)
            {
                mainCamera.gameObject.SetActive(true);
                mainCamera.enabled = true;
            }
        }

        void TakeOver()
        {
            if (hasControl || cutsceneCamera == null)
                return;

            if (mainCamera != null)
            {
                // MapToggle (M) deactivates the Main Camera GameObject, which would take
                // the AudioListener with it. Force it back before handing over.
                mainCamera.gameObject.SetActive(true);

                cutsceneCamera.transform.SetPositionAndRotation(
                    mainCamera.transform.position,
                    mainCamera.transform.rotation);

                cutsceneCamera.fieldOfView = mainCamera.fieldOfView;
            }

            cutsceneCamera.enabled = true;

            // Disable the component only — exactly one Base camera renders, which keeps
            // URP happy without any camera-stacking setup.
            if (mainCamera != null) mainCamera.enabled = false;

            hasControl = true;
        }

        void AimAt(Transform lookAt)
        {
            Transform cam = cutsceneCamera.transform;
            Vector3 dir = (lookAt.position + lookAtOffset) - cam.position;

            if (dir.sqrMagnitude > 0.0001f)
                cam.rotation = Quaternion.LookRotation(dir, Vector3.up);
        }
    }
}
