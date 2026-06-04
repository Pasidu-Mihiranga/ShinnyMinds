using UnityEngine;

public class PlayerController : MonoBehaviour
{
    // =========================
    // COMPONENTS
    // =========================
    private Animator animator;
    private CharacterController controller;

    // =========================
    // MOVEMENT SETTINGS
    // =========================
    public float walkSpeed = 3f;
    public float runSpeed = 6f;
    public float backwardSpeed = 2f;
    public float turnSpeed = 120f;

    // =========================
    // GRAVITY
    // =========================
    public float gravity = -20f;
    private float verticalVelocity = 0f;
    private bool isJumping = false;

    // =========================
    // FIXED GROUND POSITION
    // =========================
    public bool forceExactGroundY = true;
    public float exactGroundY = -0.057f;

    // =========================
    // COLLIDER SETUP
    // =========================
    public bool addMissingEnvironmentColliders = true;

    void Start()
    {
        animator = GetComponent<Animator>();
        controller = GetComponent<CharacterController>();

        if (animator != null)
            animator.applyRootMotion = false;

        // Step 1: adjust controller size ONLY (no snapping inside)
        if (controller != null)
            AdjustCharacterControllerToModel();

        // Step 2: snap ONCE after size is finalized
        if (controller != null)
            SnapToGround();

        if (forceExactGroundY)
        {
            transform.position = new Vector3(transform.position.x, exactGroundY, transform.position.z);
            Debug.Log($"[PlayerController] Forced exact ground Y = {exactGroundY:F4}");
        }

        // Step 3: add environment colliders
        if (addMissingEnvironmentColliders)
            AddMissingEnvironmentColliders();
    }

    // =========================
    // FIX CHARACTER CONTROLLER
    // SIZE TO MATCH MODEL
    // =========================
    private void AdjustCharacterControllerToModel()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();

        if (renderers == null || renderers.Length == 0)
        {
            controller.height = 1.8f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.radius = 0.3f;
            Debug.LogWarning("[PlayerController] No renderers — using default size: height=1.8");
            return;
        }

        float worldMinY = float.MaxValue;
        float worldMaxY = float.MinValue;

        foreach (Renderer r in renderers)
        {
            Bounds b = r.bounds;
            if (b.min.y < worldMinY) worldMinY = b.min.y;
            if (b.max.y > worldMaxY) worldMaxY = b.max.y;
        }

        if (worldMinY == float.MaxValue || worldMaxY == float.MinValue)
        {
            controller.height = 1.8f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            Debug.LogWarning("[PlayerController] Invalid renderer bounds — using default size.");
            return;
        }

        float modelHeightWorld = worldMaxY - worldMinY;
        float lossyY = Mathf.Abs(transform.lossyScale.y);
        if (lossyY < 0.001f) lossyY = 1f;

        float modelHeightLocal = modelHeightWorld / lossyY;
        float minHeight = controller.radius * 2.1f;
        
        // Use calculated height, but ensure minimum of 1.6 for character models
        float newHeight = Mathf.Max(modelHeightLocal, minHeight, 1.6f);

        controller.height = newHeight;
        controller.center = new Vector3(0f, newHeight * 0.5f, 0f);

        Debug.Log($"[AdjustController] height={newHeight:F3} center.y={controller.center.y:F3} (worldHeight={modelHeightWorld:F3} lossyY={lossyY:F3})");
    }

    // =========================
    // SNAP PLAYER FEET
    // TO THE GROUND
    // =========================
    private void SnapToGround()
    {
        controller.enabled = false;

        // Find the lowest point of the visual mesh (renderers)
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        float visualBottomY = float.MaxValue;

        foreach (Renderer r in renderers)
        {
            if (r.bounds.min.y < visualBottomY)
                visualBottomY = r.bounds.min.y;
        }

        // If we have renderers, raycast from the visual bottom
        // Otherwise raycast from transform position
        float rayOriginY = (visualBottomY != float.MaxValue) ? visualBottomY + 0.5f : transform.position.y + 5f;
        Vector3 rayOrigin = new Vector3(transform.position.x, rayOriginY, transform.position.z);

        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 200f))
        {
            float groundY = hit.point.y;

            if (visualBottomY != float.MaxValue)
            {
                // Don't place at ground level; place feet slightly below visual bottom (around 0.03 offset)
                // This keeps feet from sinking but avoids going to actual ground collision depth
                float delta = groundY - visualBottomY + 0.12f;  // Reduced downward push
                transform.position += new Vector3(0f, delta, 0f);
                Debug.Log($"[SnapToGround] Visual bottom at {visualBottomY:F4}, Ground at {groundY:F4}, moved by {delta:F4}");
            }
            else
            {
                // Fallback: use CharacterController geometry
                float desiredY = groundY - controller.center.y + (controller.height * 0.5f) - 0.1f;
                transform.position = new Vector3(transform.position.x, desiredY, transform.position.z);
                Debug.Log($"[SnapToGround] No renderers; Ground Y={groundY:F4} | Player set to Y={desiredY:F4}");
            }
        }
        else
        {
            Debug.LogWarning("[SnapToGround] No ground found below player!");
        }

        controller.enabled = true;
        verticalVelocity = -2f;
    }
    // =========================
    // ADD COLLIDERS TO
    // ENVIRONMENT MESHES
    // =========================
    private void AddMissingEnvironmentColliders()
    {
        MeshFilter[] meshFilters = FindObjectsOfType<MeshFilter>();
        int addedCount = 0;

        foreach (MeshFilter meshFilter in meshFilters)
        {
            GameObject go = meshFilter.gameObject;

            if (ShouldSkipCollider(go)) continue;
            if (meshFilter.sharedMesh == null) continue;

            MeshCollider mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = meshFilter.sharedMesh;
            mc.convex = false;
            addedCount++;
        }

        Debug.Log($"[PlayerController] Added {addedCount} environment colliders.");
    }

    private bool ShouldSkipCollider(GameObject go)
    {
        if (go == gameObject) return true;
        if (go.transform.IsChildOf(transform)) return true;
        if (go.GetComponent<Collider>() != null) return true;
        if (go.GetComponent<SkinnedMeshRenderer>() != null) return true;
        if (go.GetComponent<Light>() != null) return true;
        if (go.GetComponent<Camera>() != null) return true;
        if (go.GetComponent<Canvas>() != null) return true;
        return false;
    }

    // =========================
    // UPDATE LOOP
    // =========================
    void Update()
    {
        float speed = 0f;
        bool turningLeft = false;
        bool turningRight = false;
        bool movingBackward = false;

        Vector3 horizontalMove = Vector3.zero;

        // --- TURN LEFT ---
        if (Input.GetKey(KeyCode.A))
        {
            turningLeft = true;
            transform.Rotate(0, -turnSpeed * Time.deltaTime, 0);
        }

        // --- TURN RIGHT ---
        if (Input.GetKey(KeyCode.D))
        {
            turningRight = true;
            transform.Rotate(0, turnSpeed * Time.deltaTime, 0);
        }

        // --- WALK FORWARD ---
        if (Input.GetKey(KeyCode.W))
        {
            speed = 2f;
            horizontalMove = transform.forward * walkSpeed;
        }

        // --- RUN (Shift + W) ---
        if (Input.GetKey(KeyCode.LeftShift) && Input.GetKey(KeyCode.W))
        {
            speed = 6f;
            horizontalMove = transform.forward * runSpeed;
        }

        // --- WALK BACKWARD ---
        if (Input.GetKey(KeyCode.S))
        {
            movingBackward = true;
            horizontalMove = -transform.forward * backwardSpeed;
        }

        // =========================
        // GRAVITY — Fixed:
        // vertical velocity is kept
        // separate from move vector
        // =========================
        if (controller.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -2f; // Small negative to keep grounded
        }

        // --- JUMP ---
        if (Input.GetKeyDown(KeyCode.Space) && controller.isGrounded)
        {
            animator.SetTrigger("Jump");
            verticalVelocity = 7f;
            isJumping = true;
        }

        verticalVelocity += gravity * Time.deltaTime;

        // Combine horizontal + vertical into final move
        // Gravity is NOT multiplied by Time.deltaTime again here (already applied above)
        Vector3 finalMove = horizontalMove * Time.deltaTime
                          + Vector3.up * verticalVelocity * Time.deltaTime;

        controller.Move(finalMove);

        if (controller.isGrounded)
        {
            isJumping = false;
        }

        // =========================
        // ANIMATOR
        // =========================
        animator.SetFloat("Speed", speed);
        animator.SetBool("TurnLeft", turningLeft);
        animator.SetBool("TurnRight", turningRight);
        animator.SetBool("Backward", movingBackward);
    }

    private void LateUpdate()
    {
        if (forceExactGroundY && controller != null && !isJumping && controller.isGrounded)
        {
            transform.position = new Vector3(transform.position.x, exactGroundY, transform.position.z);
            verticalVelocity = -2f;
        }
    }
}