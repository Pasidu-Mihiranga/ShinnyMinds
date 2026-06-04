
using UnityEngine;

public class CarAI : MonoBehaviour
{
    // WAYPOINTS
    public Transform[] waypoints;

    // SPEED
    public float speed = 5f;

    // ROTATION SPEED
    public float turnSpeed = 5f;

    // CURRENT TARGET
    private int currentWaypoint = 0;

    void Update()
    {
        // NO WAYPOINTS
        if (waypoints.Length == 0)
            return;

        // TARGET POINT
        Transform target =
            waypoints[currentWaypoint];

        // DIRECTION
        Vector3 direction = (target.position - transform.position); // IGNORE HEIGHT DIFFERENCE 
        direction.y = 0f; 
        direction = direction.normalized;
        
        // MOVE
        transform.position +=
            direction
            * speed
            * Time.deltaTime;

        // ROTATE SMOOTHLY
        
        Quaternion lookRotation =
            Quaternion.LookRotation(direction)
            * Quaternion.Euler(0, 180, 180);


        transform.rotation =
            Quaternion.Slerp(
                transform.rotation,
                lookRotation,
                turnSpeed * Time.deltaTime
            );

        // DISTANCE CHECK
        float distance =
            Vector3.Distance(
                transform.position,
                target.position
            );

        // NEXT WAYPOINT
        if (distance < 1f)
        {
            currentWaypoint++;

            // LOOP BACK
            if (currentWaypoint >= waypoints.Length)
            {
                currentWaypoint = 0;
            }
        }
    }
}

