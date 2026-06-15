using UnityEngine;
using TMPro;

public class DoorController : MonoBehaviour
{
    public Transform door;
    public GameObject doorPrompt;

    public AudioSource audioSource;
    public AudioClip openSound;
    public AudioClip closeSound;

    public float openAngle = 90f;
    public float doorOpenSpeed = 3f;

    private bool playerNearby = false;
    private bool isOpen = false;

    private Quaternion closedRotation;
    private Quaternion openRotation;
    private Quaternion targetRotation;

    private TMP_Text promptText;

    [HideInInspector]
    public bool playerIsOutside = true;

    void Start()
    {
        if (door == null)
        {
            Debug.LogError("Door not assigned!");
            return;
        }

        closedRotation = door.localRotation;

        openRotation =
            closedRotation *
            Quaternion.Euler(0, openAngle, 0);

        targetRotation = closedRotation;

        if (doorPrompt != null)
        {
            doorPrompt.SetActive(false);

            promptText =
                doorPrompt.GetComponent<TMP_Text>();

            if (promptText == null)
            {
                promptText =
                    doorPrompt.GetComponentInChildren<TMP_Text>();
            }
        }
    }

    void Update()
    {
        door.localRotation = Quaternion.Lerp(
            door.localRotation,
            targetRotation,
            Time.deltaTime * doorOpenSpeed
        );

        if (playerNearby && promptText != null)
        {
            promptText.text =
                isOpen
                ? "Press E to Close Door"
                : "Press E to Open Door";
        }

        if (playerNearby && Input.GetKeyDown(KeyCode.E))
        {
            Animator playerAnimator =
                GameObject.FindGameObjectWithTag("Player")
                .GetComponent<Animator>();

            // OUTSIDE -> OPEN
            if (!isOpen && playerIsOutside)
            {
                playerAnimator.SetTrigger("PushDoor");

                Invoke(nameof(OpenDoor), 1.5f);
            }

            // OUTSIDE -> CLOSE
            else if (isOpen && playerIsOutside)
            {
                playerAnimator.SetTrigger("PullDoor");

                Invoke(nameof(CloseDoor), 2.0f);
            }

            // INSIDE -> OPEN
            else if (!isOpen && !playerIsOutside)
            {
                playerAnimator.SetTrigger("PullDoor");

                Invoke(nameof(OpenDoor), 2.0f);
            }

            // INSIDE -> CLOSE
            else if (isOpen && !playerIsOutside)
            {
                playerAnimator.SetTrigger("PullDoor");

                Invoke(nameof(CloseDoor), 2.0f);
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerNearby = true;

            if (doorPrompt != null)
                doorPrompt.SetActive(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerNearby = false;

            if (doorPrompt != null)
                doorPrompt.SetActive(false);
        }
    }

    public void OpenDoor()
    {
        isOpen = true;
        targetRotation = openRotation;

        if (audioSource != null && openSound != null)
        {
            audioSource.PlayOneShot(openSound);
        }

        Debug.Log("Door Opened");
    }

    public void CloseDoor()
    {
        isOpen = false;
        targetRotation = closedRotation;

        if (audioSource != null && closeSound != null)
        {
            audioSource.PlayOneShot(closeSound);
        }

        Debug.Log("Door Closed");
    }
}