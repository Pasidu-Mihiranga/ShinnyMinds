using UnityEngine;
using ShinyMinds.Core;

public class CameraController : MonoBehaviour
{
    // =========================
    // SETTINGS
    // =========================
    public float mouseSensitivity = 2f;

    // CAMERA FOLLOW SPEED
    public float followSpeed = 5f;

    // PLAYER
    public Transform playerBody;

    // VERTICAL LOOK
    private float xRotation = 0f;

    void Start()
    {
        // Cursor ownership belongs to PlayerInputLock so that choice UIs can
        // temporarily free the cursor and reliably get it back.
        PlayerInputLock.ApplyCursor();
    }

    void Update()
    {
        // Belt and braces: even if PlayerLockBinder is mis-wired, never spin the
        // camera while a cutscene or choice panel owns input.
        if (PlayerInputLock.IsLocked)
            return;

        // =========================
        // MOUSE INPUT
        // =========================
        float mouseX =
            Input.GetAxis("Mouse X")
            * mouseSensitivity;

        float mouseY =
            Input.GetAxis("Mouse Y")
            * mouseSensitivity;

        // =========================
        // FREE CAMERA ROTATION
        // =========================
        transform.Rotate(
            Vector3.up * mouseX
        );

        // =========================
        // VERTICAL LOOK
        // =========================
        xRotation -= mouseY;

        xRotation = Mathf.Clamp(
            xRotation,
            -70f,
            70f
        );

        Camera.main.transform.localRotation =
            Quaternion.Euler(
                xRotation,
                0f,
                0f
            );

        // =========================
        // MOVEMENT CHECK
        // =========================
        bool isMoving =
            Input.GetKey(KeyCode.W)
            ||
            Input.GetKey(KeyCode.S);

        // =========================
        // ONLY WHEN MOVING
        // =========================
        if (isMoving)
        {
            // ROTATE PLAYER
            playerBody.Rotate(
                Vector3.up * mouseX
            );

            // AUTO CENTER CAMERA
            Quaternion targetRotation =
                Quaternion.Euler(
                    transform.eulerAngles.x,
                    playerBody.eulerAngles.y,
                    0f
                );

            transform.rotation =
                Quaternion.Slerp(
                    transform.rotation,
                    targetRotation,
                    followSpeed * Time.deltaTime
                );
        }
    }
}

