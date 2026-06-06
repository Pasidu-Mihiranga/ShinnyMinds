using UnityEngine;

public class MiniMapArrow : MonoBehaviour
{
    public Transform player;

    void Update()
    {
        transform.localRotation =
            Quaternion.Euler(
                0f,
                0f,
                -player.eulerAngles.y
            );
    }
}