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

        // Update() overwrites the camera's local pitch every frame from xRotation, so
        // starting it at 0 threw away the tilt authored on the camera (10 degrees down)
        // on the very first frame. On touch there is no mouse to tilt it back, which
        // left the shot aimed dead level at the scenery instead of down at the girl.
        if (Camera.main != null)
        {
            float pitch = Camera.main.transform.localEulerAngles.x;

            // localEulerAngles reports 0..360, so a downward 10 comes back as 10 but an
            // upward one comes back as 350 and would clamp to the wrong end of the range.
            if (pitch > 180f)
                pitch -= 360f;

            xRotation = Mathf.Clamp(pitch, -70f, 70f);
        }
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
        // On touch there is no mouse, so mouseX stays 0 and this block is the only thing
        // that swings the camera round behind the player. Turning the stick counts as
        // movement here, otherwise the character would rotate on the spot while the
        // camera kept staring at the old heading.
        bool stickActive =
            Mathf.Abs(MobileInput.AxisV) > 0.1f
            ||
            Mathf.Abs(MobileInput.AxisH) > 0.1f;

        bool isMoving =
            Input.GetKey(KeyCode.W)
            ||
            Input.GetKey(KeyCode.S)
            ||
            stickActive;

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

