using UnityEngine;

public class CarAI : MonoBehaviour
{
    // WAYPOINTS
    public Transform[] waypoints;

    // MOVEMENT SPEED
    public float speed = 5f;

    // TURN SPEED
    public float turnSpeed = 5f;

    // CURRENT TARGET
    private int currentWaypoint = 0;

    void Update()
    {
        // NO WAYPOINTS
        if (waypoints.Length == 0)
            return;

        // CURRENT TARGET
        Transform target =
            waypoints[currentWaypoint];

        // TARGET POSITION
        Vector3 targetPosition =
            target.position;

        // KEEP SAME HEIGHT
        targetPosition.y =
            transform.position.y;

        // DIRECTION
        Vector3 direction =
            (targetPosition - transform.position)
            .normalized;

        // MOVE
        transform.position +=
            direction
            * speed
            * Time.deltaTime;

        // ROTATE
        Quaternion lookRotation =
            Quaternion.LookRotation(direction)
            * Quaternion.Euler(0, 180, 180);

        transform.rotation =
            Quaternion.Slerp(
                transform.rotation,
                lookRotation,
                turnSpeed * Time.deltaTime
            );

        // DISTANCE TO TARGET
        float distance =
            Vector3.Distance(
                transform.position,
                targetPosition
            );

        // NEXT WAYPOINT
        if (distance < 1f)
        {
            currentWaypoint++;

            // LOOP BACK TO START
            if (currentWaypoint >= waypoints.Length)
            {
                currentWaypoint = 0;
            }
        }
    }
}

