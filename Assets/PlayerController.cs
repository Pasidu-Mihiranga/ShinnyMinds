using UnityEngine;

public class PlayerController : MonoBehaviour
{
    // =========================
    // COMPONENTS
    // =========================
    private Animator animator;
    private CharacterController controller;

    // =========================
    // MOVEMENT SETTINGS
    // =========================
    public float walkSpeed = 0.3f;
    public float runSpeed = 1f;
    public float backwardSpeed = 0.5f;
    public float turnSpeed = 120f;

    // =========================
    // GRAVITY
    // =========================
    public float gravity = -9.81f;

    // VERTICAL VELOCITY
    private Vector3 velocity;

    void Start()
    {
        // GET COMPONENTS
        animator = GetComponent<Animator>();
        controller = GetComponent<CharacterController>();
    }

    void Update()
    {
        // =========================
        // VARIABLES
        // =========================
        float speed = 0f;

        bool turningLeft = false;
        bool turningRight = false;
        bool movingBackward = false;

        // MOVEMENT DIRECTION
        Vector3 move = Vector3.zero;

        // =========================
        // TURN LEFT
        // =========================
        if (Input.GetKey(KeyCode.A))
        {
            turningLeft = true;

            transform.Rotate(
                0,
                -turnSpeed * Time.deltaTime,
                0
            );
        }

        // =========================
        // TURN RIGHT
        // =========================
        if (Input.GetKey(KeyCode.D))
        {
            turningRight = true;

            transform.Rotate(
                0,
                turnSpeed * Time.deltaTime,
                0
            );
        }

        // =========================
        // WALK FORWARD
        // =========================
        if (Input.GetKey(KeyCode.W))
        {
            speed = 2f;

            move =
                transform.forward
                * walkSpeed;
        }

        // =========================
        // RUN
        // =========================
        if (
            Input.GetKey(KeyCode.LeftShift)
            &&
            Input.GetKey(KeyCode.W)
        )
        {
            speed = 6f;

            move =
                transform.forward
                * runSpeed;
        }

        // =========================
        // WALK BACKWARD
        // =========================
        if (Input.GetKey(KeyCode.S))
        {
            movingBackward = true;

            move =
                -transform.forward
                * backwardSpeed;
        }

        // =========================
        // GRAVITY
        // =========================
        if (controller.isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

        velocity.y += gravity * Time.deltaTime;

        // ADD VERTICAL MOVEMENT
        move.y = velocity.y;

        // =========================
        // MOVE CHARACTER
        // =========================
        controller.Move(
            move * Time.deltaTime
        );

        // =========================
        // ANIMATOR PARAMETERS
        // =========================
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

        // =========================
        // JUMP
        // =========================
        if (
            Input.GetKeyDown(KeyCode.Space)
            &&
            controller.isGrounded
        )
        {
            animator.SetTrigger("Jump");

            velocity.y = 5f;
        }
    }
}
