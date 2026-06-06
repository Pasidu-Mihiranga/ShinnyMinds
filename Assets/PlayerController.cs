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
    // UPDATE LOOP
    // =========================
    void Update()
    {
        float speed = 0f;

        bool turningLeft = false;
        bool turningRight = false;
        bool movingBackward = false;

        Vector3 horizontalMove = Vector3.zero;

        // TURN LEFT
        if (Input.GetKey(KeyCode.A))
        {
            turningLeft = true;

            transform.Rotate(
                0,
                -turnSpeed * Time.deltaTime,
                0
            );
        }

        // TURN RIGHT
        if (Input.GetKey(KeyCode.D))
        {
            turningRight = true;

            transform.Rotate(
                0,
                turnSpeed * Time.deltaTime,
                0
            );
        }

        // WALK FORWARD
        if (Input.GetKey(KeyCode.W))
        {
            speed = 2f;

            horizontalMove =
                transform.forward
                * walkSpeed;
        }

        // RUN
        if (
            Input.GetKey(KeyCode.LeftShift)
            &&
            Input.GetKey(KeyCode.W)
        )
        {
            speed = 6f;

            horizontalMove =
                transform.forward
                * runSpeed;
        }

        // WALK BACKWARD
        if (Input.GetKey(KeyCode.S))
        {
            movingBackward = true;

            horizontalMove =
                -transform.forward
                * backwardSpeed;
        }

        // KEEP CHARACTER GROUNDED
        if (
            controller.isGrounded
            &&
            verticalVelocity < 0f
        )
        {
            verticalVelocity = -2f;
        }

        // JUMP
        if (
            Input.GetKeyDown(KeyCode.Space)
            &&
            controller.isGrounded
        )
        {
            animator.SetTrigger("Jump");

            verticalVelocity = 7f;

            isJumping = true;
        }

        verticalVelocity +=
            gravity * Time.deltaTime;

        Vector3 finalMove =
            horizontalMove * Time.deltaTime
            +
            Vector3.up
            * verticalVelocity * Time.deltaTime;

        controller.Move(finalMove);

        if (controller.isGrounded)
        {
            isJumping = false;
        }

        // ANIMATOR
        animator.SetFloat(
            "Speed",
            speed
        );

        animator.SetBool(
            "TurnLeft",
            turningLeft
        );

        animator.SetBool(
            "TurnRight",
            turningRight
        );

        animator.SetBool(
            "Backward",
            movingBackward
        );
    }
}

