using UnityEngine;
using TMPro;

public class DoorController : MonoBehaviour
{
    // Actual door object that rotates
    public Transform door;

    // UI text object
    public GameObject doorPrompt;

    private bool playerNearby = false;
    private bool isOpen = false;

    public float openAngle = 90f;

    private Quaternion closedRotation;
    private Quaternion openRotation;

    private TMP_Text promptText;

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
        // Update prompt text
        if (playerNearby && promptText != null)
        {
            if (isOpen)
                promptText.text = "Press E to Close Door";
            else
                promptText.text = "Press E to Open Door";
        }

        // Open / Close Door
        if (playerNearby && Input.GetKeyDown(KeyCode.E))
        {
            isOpen = !isOpen;

            if (isOpen)
            {
                door.localRotation = openRotation;
                Debug.Log("Door Opened");
            }
            else
            {
                door.localRotation = closedRotation;
                Debug.Log("Door Closed");
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
}