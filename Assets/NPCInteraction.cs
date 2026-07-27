using UnityEngine;
using ShinyMinds.Core;

// Runs before DoorController (order 0) so a talkable NPC wins an overlapping E press,
// and after GroqDialogue / MissionRunner (order -100) which advance open dialogue.
[DefaultExecutionOrder(-50)]
public class NPCInteraction : MonoBehaviour
{
    public GroqDialogue groqDialogue;

    private bool playerNear = false;

    void Update()
    {
        // A mission cutscene or an open dialogue owns input.
        if (PlayerInputLock.IsLocked)
            return;

        if(playerNear &&
        !groqDialogue.IsDialogueOpen &&
        InteractKey.TryConsumeWorld())
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