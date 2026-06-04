
using UnityEngine;

public class CameraController : MonoBehaviour
{
    // =========================
    // SETTINGS
    // =========================
    public float mouseSensitivity = 3200000f;

    // CAMERA FOLLOW SPEED
    public float followSpeed = 2f;

    // PLAYER
    public Transform playerBody;

    // VERTICAL ROTATION
    private float xRotation = 0f;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
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
        // CHECK MOVEMENT
        // =========================
        bool isMoving =
            Input.GetKey(KeyCode.W)
            ||
            Input.GetKey(KeyCode.S);

        // =========================
        // ROTATE PLAYER WHEN MOVING
        // =========================
        if (isMoving)
        {
            playerBody.Rotate(
                Vector3.up * mouseX
            );
        }

        // =========================
        // AUTO CAMERA CENTERING
        // =========================
        Quaternion targetRotation =
            Quaternion.Euler(
                transform.eulerAngles.x,
                playerBody.eulerAngles.y,
                0f
            );

        // SMOOTH FOLLOW
        transform.rotation =
            Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                followSpeed * Time.deltaTime
            );
    }
}

