using UnityEngine;

public class footstepaudio : MonoBehaviour
{
    public AudioSource audioSource;
    public AudioClip walkClip;
    public AudioClip runClip;

    private Animator animator;
    private CharacterController controller;

    void Start()
    {
        animator = GetComponent<Animator>();
        controller = GetComponent<CharacterController>();
    }

    void Update()
    {
        bool jumping = !controller.isGrounded;

        bool walking =
            Input.GetKey(KeyCode.W) ||
            Input.GetKey(KeyCode.S);

        bool running =
            Input.GetKey(KeyCode.W) &&
            Input.GetKey(KeyCode.LeftShift);

        // Stop footsteps while jumping
        if (jumping)
        {
            audioSource.Stop();
            return;
        }

        // Running sound
        if (running)
        {
            if (audioSource.clip != runClip)
            {
                audioSource.clip = runClip;
            }

            if (!audioSource.isPlaying)
            {
                audioSource.loop = true;
                audioSource.Play();
            }
        }
        // Walking sound (forward and backward)
        else if (walking)
        {
            if (audioSource.clip != walkClip)
            {
                audioSource.clip = walkClip;
            }

            if (!audioSource.isPlaying)
            {
                audioSource.loop = true;
                audioSource.Play();
            }
        }
        // Idle
        else
        {
            audioSource.Stop();
        }
    }
}