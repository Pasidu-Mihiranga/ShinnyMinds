using UnityEngine;

public class CameraCollision : MonoBehaviour
{
    [Header("References")]
    public Transform player;
    public Transform cameraTransform;

    [Header("Collision")]
    public float sphereRadius = 0.3f;
    public float wallOffset = 0.4f;
    public float minDistance = 2.0f;
    public float collisionBuffer = 0.5f;

    [Header("Camera Speed")]
    public float collisionSmoothTime = 0.15f;  // was 0.08 — less aggressive
    public float returnSmoothTime = 0.35f;     // was 0.25 — smoother return

    [Header("Hysteresis (prevents flickering)")]
    public float collisionEnterThreshold = 0.05f; // must be THIS close to trigger
    public float collisionExitThreshold  = 0.12f; // must be THIS far to release

    private Vector3 defaultLocalPosition;
    private Vector3 smoothVelocity;           // single shared velocity — no snapping
    private bool isInCollision = false;

    void Start()
    {
        defaultLocalPosition = cameraTransform.localPosition;
    }

    void LateUpdate()
    {
        Vector3 desiredWorldPosition = transform.TransformPoint(defaultLocalPosition);
        Vector3 direction = desiredWorldPosition - player.position;
        float desiredDistance = direction.magnitude;
        direction.Normalize();

        bool hitSomething = Physics.SphereCast(
            player.position,
            sphereRadius,
            direction,
            out RaycastHit hit,
            desiredDistance + collisionBuffer
        );

        Vector3 targetPosition;

        if (hitSomething)
        {
            float safeDistance = Mathf.Max(hit.distance - wallOffset, minDistance);
            targetPosition = player.position + direction * safeDistance;

            // Enter collision state only if camera is meaningfully closer than desired
            float currentDistance = Vector3.Distance(player.position, cameraTransform.position);
            if (!isInCollision && (desiredDistance - safeDistance) > collisionEnterThreshold)
                isInCollision = true;
        }
        else
        {
            targetPosition = desiredWorldPosition;

            // Exit collision state only once camera is close enough to desired position
            if (isInCollision)
            {
                float gap = Vector3.Distance(cameraTransform.position, desiredWorldPosition);
                if (gap < collisionExitThreshold)
                    isInCollision = false;
            }
        }

        // One SmoothDamp call, one velocity — no competing vectors
        float smoothTime = isInCollision ? collisionSmoothTime : returnSmoothTime;
        cameraTransform.position = Vector3.SmoothDamp(
            cameraTransform.position,
            targetPosition,
            ref smoothVelocity,   // shared — velocity is preserved across state changes
            smoothTime
        );
    }
}