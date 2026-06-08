using UnityEngine;

public class NPCInteraction : MonoBehaviour
{
    public GroqDialogue groqDialogue;

    private bool playerNear = false;

    void Update()
    {
        if(playerNear &&
        !groqDialogue.IsDialogueOpen &&
        Input.GetKeyDown(KeyCode.E))
        {
            groqDialogue.GenerateConversation();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerNear = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerNear = false;
        }
    }
}