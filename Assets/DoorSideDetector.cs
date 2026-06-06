using UnityEngine;

public class DoorSideDetector : MonoBehaviour
{
    public DoorController doorController;
    public bool isOutsideZone;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            doorController.playerIsOutside = isOutsideZone;

            Debug.Log(
                isOutsideZone
                ? "Player Outside"
                : "Player Inside"
            );
        }
    }
}