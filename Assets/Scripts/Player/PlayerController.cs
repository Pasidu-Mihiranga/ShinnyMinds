using UnityEngine;

public class PlayerController : MonoBehaviour
{
    // COMPONENTS
    private Animator animator;
    private CharacterController controller;

    // MOVEMENT SETTINGS
    public float walkSpeed = 3f;
    public float runSpeed = 6f;
    public float backwardSpeed = 2f;
    public float turnSpeed = 120f;

    // How fast she swings round to face a new stick direction. Much faster than turnSpeed,
    // which is the rate she pivots on the spot for the keyboard: a thumb flicked to a new
    // direction should not feel like a three-point turn.
    public float faceTurnSpeed = 540f;

    // GRAVITY
    public float gravity = -20f;
    private float verticalVelocity = 0f;
    private bool isJumping = false;

    void Start()
    {
        animator = GetComponent<Animator>();
        controller = GetComponent<CharacterController>();

        if (animator != null)
            animator.applyRootMotion = false;

        if (controller != null)
            AdjustCharacterControllerToModel();

        if (controller != null)
            SnapToGround();
    }

    // =========================
    // FIX CHARACTER CONTROLLER SIZE
    // =========================
    private void AdjustCharacterControllerToModel()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();

        if (renderers == null || renderers.Length == 0)
        {
            controller.height = 1.8f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.radius = 0.3f;
            return;
        }

        float worldMinY = float.MaxValue;
        float worldMaxY = float.MinValue;

        foreach (Renderer r in renderers)
        {
            Bounds b = r.bounds;

            if (b.min.y < worldMinY)
                worldMinY = b.min.y;

            if (b.max.y > worldMaxY)
                worldMaxY = b.max.y;
        }

        float modelHeightWorld = worldMaxY - worldMinY;

        float lossyY = Mathf.Abs(transform.lossyScale.y);

        if (lossyY < 0.001f)
            lossyY = 1f;

        float modelHeightLocal =
            modelHeightWorld / lossyY;

        float minHeight =
            controller.radius * 2.1f;

        float newHeight =
            Mathf.Max(
                modelHeightLocal,
                minHeight,
                1.6f
            );

        controller.height = newHeight;

        controller.center =
            new Vector3(
                0f,
                newHeight * 0.5f,
                0f
            );
    }

    // =========================
    // SNAP TO GROUND
    // =========================
    private void SnapToGround()
    {
        controller.enabled = false;

        Renderer[] renderers =
            GetComponentsInChildren<Renderer>();

        float visualBottomY =
            float.MaxValue;

        foreach (Renderer r in renderers)
        {
            if (r.bounds.min.y < visualBottomY)
                visualBottomY = r.bounds.min.y;
        }

        float rayOriginY =
            (visualBottomY != float.MaxValue)
            ? visualBottomY + 0.5f
            : transform.position.y + 5f;

        Vector3 rayOrigin =
            new Vector3(
                transform.position.x,
                rayOriginY,
                transform.position.z
            );

        if (
            Physics.Raycast(
                rayOrigin,
                Vector3.down,
                out RaycastHit hit,
                200f
            )
        )
        {
            float groundY =
                hit.point.y;

            if (visualBottomY != float.MaxValue)
            {
                float delta =
                    groundY
                    - visualBottomY
                    + 0.12f;

                transform.position +=
                    new Vector3(
                        0f,
                        delta,
                        0f
                    );
            }
        }

        controller.enabled = true;

        verticalVelocity = -2f;
    }

    // =========================
    // CAMERA HEADING
    // =========================
    /// <summary>
    /// The camera's heading flattened onto the ground, which is the frame the stick's four
    /// directions are read in. Falls back to her own facing when there is no main camera — a
    /// mission cutscene disables it — or when the camera is looking straight down and has no
    /// heading left to give.
    /// </summary>
    private Vector3 CameraForward()
    {
        Camera cam = Camera.main;

        Vector3 forward = cam != null ? cam.transform.forward : transform.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude < 0.001f)
            forward = transform.forward;

        return forward.normalized;
    }

    // =========================
    // UPDATE LOOP
    // =========================
    void Update()
{
    float speed = 0f;

    bool turningLeft = false;
    bool turningRight = false;
    bool movingBackward = false;

    Vector3 horizontalMove = Vector3.zero;

    // -----------------------------
    // Keyboard Input
    // -----------------------------
    float horizontalInput = Input.GetAxisRaw("Horizontal");
    float verticalInput = Input.GetAxisRaw("Vertical");

    bool run = Input.GetKey(KeyCode.LeftShift);
    bool jump = Input.GetKeyDown(KeyCode.Space);

    // -----------------------------
    // Mobile Input
    // -----------------------------
    // Run and Jump merge with the keys either way. MobileInput.JumpTapped is already a
    // single-frame edge, matching GetKeyDown.
    run = run || MobileInput.RunHeld;
    jump = jump || MobileInput.JumpTapped;

    Vector2 stick = new Vector2(MobileInput.AxisH, MobileInput.AxisV);

    // Squared against 0.2 squared, so the stick crosses into "driving" at the same push the
    // keys below use.
    bool stickDriving = stick.sqrMagnitude > 0.04f;

    if (stickDriving)
    {
        // -----------------------------
        // Stick: four directions, and none of them is "turn the camera"
        // -----------------------------
        // Read against the camera, so pushing away from yourself always walks away from the
        // camera however the look gesture has swung it, and she turns to face the direction
        // she is walking. Pulling down therefore walks her towards the camera facing it,
        // rather than reversing — she is never walking backwards, so there is no reverse
        // shuffle to animate.
        //
        // Nothing here rotates the camera rig. That belongs to the look gesture alone; see
        // the note in CameraController about why the stick is no longer counted as movement
        // there either.
        Vector3 forward = CameraForward();
        Vector3 right = Vector3.Cross(Vector3.up, forward);

        Vector3 direction = forward * stick.y + right * stick.x;

        if (direction.sqrMagnitude > 0.001f)
        {
            direction.Normalize();

            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                Quaternion.LookRotation(direction, Vector3.up),
                faceTurnSpeed * Time.deltaTime);

            // Full speed past the dead zone rather than scaling with the push: the animator
            // has a walk clip and a run clip, and moving her at half speed under a walk clip
            // slides her feet across the pavement.
            speed = run ? 6f : 2f;
            horizontalMove = direction * (run ? runSpeed : walkSpeed);
        }
    }
    else
    {
        // -----------------------------
        // Keyboard: A/D pivot her on the spot, W/S walk her along her own facing
        // -----------------------------
        if (Mathf.Abs(horizontalInput) > 0.2f)
        {
            turningLeft = horizontalInput < 0f;
            turningRight = horizontalInput > 0f;

            transform.Rotate(0, turnSpeed * horizontalInput * Time.deltaTime, 0);
        }

        if (verticalInput > 0.2f)
        {
            speed = run ? 6f : 2f;
            horizontalMove = transform.forward * (run ? runSpeed : walkSpeed);
        }

        if (verticalInput < -0.2f)
        {
            movingBackward = true;
            horizontalMove = -transform.forward * backwardSpeed;
        }
    }

    // -----------------------------
    // Gravity
    // -----------------------------
    if (controller.isGrounded && verticalVelocity < 0)
        verticalVelocity = -2f;

    // -----------------------------
    // Jump
    // -----------------------------
    if (jump && controller.isGrounded)
    {
        if (animator != null)
            animator.SetTrigger("Jump");

        verticalVelocity = 7f;
        isJumping = true;
    }

    verticalVelocity += gravity * Time.deltaTime;

    Vector3 finalMove =
        horizontalMove * Time.deltaTime +
        Vector3.up * verticalVelocity * Time.deltaTime;

    controller.Move(finalMove);

    if (controller.isGrounded)
        isJumping = false;

    // -----------------------------
    // Animator
    // -----------------------------
    animator.SetFloat("Speed", speed);
    animator.SetBool("TurnLeft", turningLeft);
    animator.SetBool("TurnRight", turningRight);
    animator.SetBool("Backward", movingBackward);
}
}