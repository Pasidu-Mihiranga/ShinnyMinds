using UnityEngine;

public class PlayerController : MonoBehaviour
{
    private Animator animator;

    public float walkSpeed = 0.3f;
    public float runSpeed = 1f;
    public float backwardSpeed = 1.5f;
    public float turnSpeed = 120f;

    void Start()
    {
        animator = GetComponent<Animator>();
    }

    void Update()
    {
        float speed = 0f;

        bool turningLeft = false;
        bool turningRight = false;
        bool movingBackward = false;

        // TURN LEFT
        if (Input.GetKey(KeyCode.A))
        {
            turningLeft = true;

            transform.Rotate(0, -turnSpeed * Time.deltaTime, 0);
        }

        // TURN RIGHT
        if (Input.GetKey(KeyCode.D))
        {
            turningRight = true;

            transform.Rotate(0, turnSpeed * Time.deltaTime, 0);
        }

        // WALK FORWARD
        if (Input.GetKey(KeyCode.W))
        {
            speed = 2f;

            transform.Translate(Vector3.forward * walkSpeed * Time.deltaTime);
        }

        // RUN
        if (Input.GetKey(KeyCode.LeftShift) && Input.GetKey(KeyCode.W))
        {
            speed = 6f;

            transform.Translate(Vector3.forward * runSpeed * Time.deltaTime);
        }

        // WALK BACKWARD
        if (Input.GetKey(KeyCode.S))
        {
            movingBackward = true;

            transform.Translate(Vector3.back * backwardSpeed * Time.deltaTime);
        }

        // SEND PARAMETERS TO ANIMATOR
        animator.SetFloat("Speed", speed);

        animator.SetBool("TurnLeft", turningLeft);
        animator.SetBool("TurnRight", turningRight);

        animator.SetBool("Backward", movingBackward);

        // JUMP
        if (Input.GetKeyDown(KeyCode.Space))
        {
            animator.SetTrigger("Jump");
        }
    }
}