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

        // Mirrors PlayerController's own 0.2 threshold, so the loop starts on the same
        // frame the character does. Reading the stick as well as the keys is what keeps
        // footsteps audible on touch, where W and S are never pressed.
        float forward = Mathf.Abs(Input.GetAxisRaw("Vertical")) > 0.2f
            ? Input.GetAxisRaw("Vertical")
            : MobileInput.AxisV;

        bool walking = Mathf.Abs(forward) > 0.2f;

        bool running =
            forward > 0.2f &&
            (Input.GetKey(KeyCode.LeftShift) || MobileInput.RunHeld);

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