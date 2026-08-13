using UnityEngine;
using ShinyMinds.Core;

/// <summary>
/// The camera rig. This object (CameraHolder) is a CHILD of the girl, sitting at her origin
/// with the camera hanging behind and above it, so it follows her about for free.
///
/// Being her child, it also inherits her rotation — and that was the real reason the joystick
/// still swung the view after the stick stopped turning her: transform parenting was carrying
/// the camera round as she faced each new walking direction, which no amount of input gating
/// can prevent. So the rig owns its heading in WORLD space (<see cref="yaw"/>) and writes
/// transform.rotation every frame, which overrides whatever the parent contributed. Her
/// rotation now moves the camera's position, never its direction.
/// </summary>
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

    // The heading this rig owns, in world degrees. Nothing but the look gesture and the
    // keyboard's auto-centre may change it.
    private float yaw = 0f;

    void OnEnable()
    {
        PlayerInputLock.GameplayLockChanged += OnLockChanged;
    }

    void OnDisable()
    {
        PlayerInputLock.GameplayLockChanged -= OnLockChanged;
    }

    /// <summary>
    /// A cutscene can walk or teleport the girl anywhere while this script is standing down, so
    /// resume behind her rather than snapping back to a heading from before it started.
    /// </summary>
    void OnLockChanged(bool locked)
    {
        if (!locked && playerBody != null)
            yaw = playerBody.eulerAngles.y;
    }

    void Start()
    {
        yaw = transform.eulerAngles.y;

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
        //
        // Reading input is all that is skipped. Holding the heading is NOT — see the bottom of
        // this method. A locked frame that also skipped that would let the rig fall back to
        // inheriting the girl's rotation, and a cutscene that turns her to face someone (the
        // stranger calling her name) would swing the player's view a full half-turn with her.
        bool locked = PlayerInputLock.IsLocked;

        if (!locked)
            ReadLookAndFollow();

        // =========================
        // OWN THE HEADING
        // =========================
        // Every frame, locked or not, and as a world rotation, so the girl's own turning is
        // discarded rather than inherited. Pitch stays at zero here — it lives on the camera
        // child, and the rig is only ever a yaw pivot.
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
    }

    void ReadLookAndFollow()
    {
        // =========================
        // LOOK INPUT
        // =========================
        // The stick's own finger must never turn the camera. On mobile, legacy Input reports
        // the primary touch as the mouse, so a thumb dragging the stick arrives here as
        // Mouse X/Y and swings the view around while she walks. Looking is the look gesture's
        // job, and a look gesture is any drag that is NOT on the stick.
        bool stickOwnsTheFinger = MobileInput.StickDragging;

        float mouseX = stickOwnsTheFinger
            ? 0f
            : Input.GetAxis("Mouse X") * mouseSensitivity;

        float mouseY = stickOwnsTheFinger
            ? 0f
            : Input.GetAxis("Mouse Y") * mouseSensitivity;

        // =========================
        // FREE CAMERA ROTATION
        // =========================
        // Accumulated, not Rotate(): Rotate() adds to whatever the parent had already turned
        // us to this frame, which is how her heading used to leak into the camera.
        yaw += mouseX;

        // =========================
        // VERTICAL LOOK
        // =========================
        xRotation -= mouseY;

        xRotation = Mathf.Clamp(
            xRotation,
            -70f,
            70f
        );

        // Guarded like the read at the top of this file. A cutscene renders by switching
        // the main camera off, and Camera.main only ever returns an ENABLED camera, so
        // there are frames — the blend back out of a cutscene — with no main camera at all.
        Camera view = Camera.main;

        if (view != null)
        {
            view.transform.localRotation =
                Quaternion.Euler(
                    xRotation,
                    0f,
                    0f
                );
        }

        // =========================
        // MOVEMENT CHECK
        // =========================
        // Keyboard only, deliberately. The block below swings the camera round behind the
        // girl, and the stick used to count as movement here so that a stick turn dragged the
        // view along with it. The stick no longer turns her on the spot at all — it walks her
        // in one of four directions and she faces the way she goes — so counting it here would
        // put it back in charge of the camera angle by the back door, which is the one thing
        // it must not do. On touch the view is moved by the look gesture and nothing else.
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
            // LerpAngle, so it takes the short way round through 0/360.
            yaw = Mathf.LerpAngle(
                yaw,
                playerBody.eulerAngles.y,
                followSpeed * Time.deltaTime
            );
        }
    }
}

