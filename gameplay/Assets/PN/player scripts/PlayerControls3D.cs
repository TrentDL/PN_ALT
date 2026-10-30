// PlayerControls3D.cs
// ADDED (3D pass): a 3D copy of PlayerControl4, made only to test how moving and
// jumping feel on ProBuilder platforms. Height now comes from real 3D collisions,
// so GroundArea, TileStep, ElevationRamp, PolygonArea and PlatformBounds are not used.
//
// REMOVED (3D pass), and why:
//   - Perspective scaling (PlayerScale, PlayerRatio, AdjustPlayerScale, ApplyScaleToSpeed,
//     frozen scale): a 3D perspective camera shrinks far-away objects on its own.
//   - Boundary / area / edge / gate checks, groundPosition, elevation, lastSafe*:
//     3D colliders now block the player, and the controller finds the floor itself.
//   - Shadow logic: removed as requested.
//   - FixedUpdate + Rigidbody2D path: the CharacterController moves in Update only.
//   - logAreaChanges: there are no areas any more.


using UnityEngine;


// ADDED (3D pass): Unity adds a CharacterController automatically when this script is
// added, and stops it being removed - the script cannot work without one.
[RequireComponent(typeof(CharacterController))]
public class PlayerControls3D : MonoBehaviour
{
    // COMMENTED OUT (3D pass): only used by MoveToPoint, which is still 2D (see bottom).
    // static float moveSpeed = 5f, moveAccuracy = 0.15f;

    static readonly int FacingRightHash = Animator.StringToHash("FacingRight");

    #region movement fields/properties

    [Header("Facing")]
    [Tooltip("Animator holding the flip state.")]
    public Animator animator;

    // CHANGED (3D pass): was Rigidbody2D. A CharacterController collides with 3D colliders
    // (ProBuilder meshes) and walks up slopes and small steps by itself, without physics
    // forces shoving the player around.
    private CharacterController controller;

    // COMMENTED OUT (3D pass): Dash and Knockback hand back a Vector2 velocity made for the
    // 2D floor. Not converted, as requested.
    // private Dash dash;
    // private Knockback knockback;

    private Vector2 moveInput;

    // CHANGED (3D pass): Vector2 -> Vector3. X = left/right, Z = toward/away from the camera.
    // Y stays 0 here - up/down lives in verticalVelocity so the two never mix.
    private Vector3 moveVelocity;

    [Header("Movement")]
    public float moveXspeed = 5f;

    // CHANGED (3D pass): was moveYspeed. In 3D, Y is "up", so walking into the screen is Z.
    public float moveZspeed = 2.5f;

    [Header("Sprint Settings")]
    public string sprintKeyLeft = "a";
    public string sprintKeyRight = "d";
    public float sprintMultiplier = 2f;
    public float doubleTapWindow = 0.2f;

    private float lastTapTimeLeft = -1f;
    private float lastTapTimeRight = -1f;
    private bool isSprintingLeft = false;
    private bool isSprintingRight = false;

    [Header("Jump Settings")]
    // CHANGED (3D pass): now in world units (1 = one ProBuilder grid square), not elevation units.
    [Tooltip("Maximum height of a held jump, in world units.")]
    public float jumpHeight = 2f;
    [Tooltip("How long the ASCENT takes. The fall is separate and self-timing.")]
    public float jumpDuration = 0.5f;

    [Tooltip("World units per second squared. Higher = heavier.")]
    public float fallAcceleration = 20f;

    [Tooltip("Keep moving at takeoff speed if the movement keys are released mid-jump. Off = hard stop in the air.")]
    public bool JumpCarryOver = true;

    // CHANGED (3D pass): Vector2 -> Vector3 to match moveVelocity.
    private Vector3 jumpMomentum;

    private float jumpYOffset = 0f;

    private bool isJumping = false;

    // ADDED (3D pass): up/down speed in world units per second. Positive = rising,
    // negative = falling. The one number that replaces elevation + isFalling + FallCoroutine.
    private float verticalVelocity = 0f;

    // ADDED (3D pass): seconds into the current ascent. Kept as a field because the jump
    // now runs a little each Update instead of inside a coroutine.
    private float jumpElapsed = 0f;

    // CHANGED (3D pass): "falling" is now simply "not touching ground and not rising",
    // so the controller's own ground check replaces the isFalling flag.
    public bool IsAirborne => isJumping || !controller.isGrounded;

    [Header("Variable Jump")]
    [Tooltip("Height a tapped jump reaches. Must be below jumpHeight or holding does nothing.")]
    public float tapJumpHeight = 0.8f;

    // ADDED (3D pass)
    [Header("Grounding")]
    [Tooltip("Small downward push while standing, so the controller keeps touching the floor " +
             "on slopes and down small steps (otherwise isGrounded flickers off).")]
    public float groundStickSpeed = 2f;

    // ADDED (3D pass): replaces the fall-floor + lastSafePosition recovery.
    [Header("Respawn")]
    [Tooltip("Falling below this world height puts the player back where they started.")]
    public float fallFloorY = -20f;

    private Vector3 startPosition;

    [Header(" TEMP Debug")]
    [Tooltip("Log height at takeoff, at the top of the jump, at every landing and on reset.")]
    public bool logHeight = false;

    [Tooltip("Log why sprint turned on or off, per key.")]
    public bool logSprint = false;

    // ADDED (3D pass): last frame's grounded state, so a landing (false -> true) can be logged.
    private bool wasGrounded = true;

    #endregion


    void Start()
    {
        controller = GetComponent<CharacterController>();

        // dash = GetComponent<Dash>();
        // knockback = GetComponent<Knockback>();

        if (animator == null)
        {
            Debug.LogError("PlayerControls3D: animator is not assigned. Facing will not change.", this);
        }

        // ADDED (3D pass): remembered for the fall-floor respawn.
        startPosition = transform.position;

        // ADDED (3D pass): 2D physics never touches 3D colliders, and a 3D Rigidbody fights
        // the CharacterController. Easy to leave behind when copying the 2D player.
        if (GetComponent<Rigidbody2D>() != null || GetComponent<Collider2D>() != null || GetComponent<Rigidbody>() != null)
        {
            Debug.LogWarning("PlayerControls3D: remove Rigidbody2D / Collider2D / Rigidbody from the player - " +
                             "the CharacterController does their job in 3D.", this);
        }
    }//end of function >:D


    void Update()
    {
        moveInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        Vector2 dir = moveInput.normalized;

        // CHANGED (3D pass): W/S (input Y) now moves along world Z.
        moveVelocity = new Vector3(dir.x * moveXspeed, 0f, dir.y * moveZspeed);

        DetectDoubleTap();
        ApplySprintSpeed();

        // if (dash != null && dash.IsDashing) moveVelocity = dash.Velocity;
        // if (knockback != null && knockback.IsKnockedBack) moveVelocity = knockback.Velocity;

        UpdateFacing();
        JumpPhysics();

        // CHANGED (3D pass): StartJump instead of StartCoroutine; "on the ground" is now
        // the controller's own check (via IsAirborne).
        if (Input.GetKeyDown(KeyCode.Space) && !IsAirborne)
        {
            StartJump();
        }

        // ADDED (3D pass): decide up/down speed first, then move once with everything.
        UpdateVertical(Time.deltaTime);
        ApplyMovement(Time.deltaTime);
    }//end of function >:D


    private void JumpPhysics()
    {
        if (!IsAirborne)
        {
            jumpMomentum = moveVelocity;
            return;
        }

        if (!JumpCarryOver) return;

        if (moveInput == Vector2.zero) moveVelocity = jumpMomentum;
    }


    // CHANGED (3D pass): the whole boundary / area / edge / gate block is gone - 3D colliders
    // do that job now. Exactly ONE Move per frame, on purpose: the controller's isGrounded and
    // collisionFlags only describe the most recent Move, so a second call would overwrite them.
    private void ApplyMovement(float deltaTime)
    {
        Vector3 velocity = moveVelocity + Vector3.up * verticalVelocity;
        controller.Move(velocity * deltaTime);

        bool grounded = controller.isGrounded;
        if (grounded && !wasGrounded) LogHeight("land");
        wasGrounded = grounded;

        if (transform.position.y < fallFloorY) ResetTo(startPosition);
    }//end of function >:D


    // ADDED (3D pass): the ONE place verticalVelocity is decided each frame.
    // Three cases, checked in order: rising (jumping), standing, falling.
    private void UpdateVertical(float deltaTime)
    {
        // Paused game (Time.timeScale = 0) gives deltaTime = 0. Rise divides by it, and
        // dividing by zero would launch the player - so do nothing while paused.
        if (deltaTime <= 0f) return;

        if (isJumping)
        {
            Rise(deltaTime);
            return;
        }

        if (controller.isGrounded)
        {
            verticalVelocity = -groundStickSpeed;
            return;
        }

        // Falling: speed downward grows every frame. Same rule FallCoroutine used.
        // Walking off a ledge lands here too - no special case needed.
        verticalVelocity -= fallAcceleration * deltaTime;
    }//end of function >:D


    // ADDED (3D pass): replaces the jump's first lines (before its loop).
    private void StartJump()
    {
        isJumping = true;
        jumpElapsed = 0f;
        jumpYOffset = 0f;
        LogHeight("takeoff");
    }//end of function >:D


    // ADDED (3D pass): replaces JumpCoroutine's loop. Same sine curve as before, but instead
    // of PLACING the player at a height, it turns "how much higher than last frame" into a
    // speed. Moving by speed lets the controller stop the player on a ceiling.
    private void Rise(float deltaTime)
    {
        jumpElapsed += deltaTime;
        float t = Mathf.Clamp01(jumpElapsed / jumpDuration);

        float newOffset = Mathf.Sin(t * Mathf.PI * 0.5f) * jumpHeight;

        // distance gained this frame / time this frame = speed.
        // e.g. gained 0.1 in 0.02 seconds -> 5 units per second.
        verticalVelocity = (newOffset - jumpYOffset) / deltaTime;
        jumpYOffset = newOffset;

        // CollisionFlags.Above = "the last Move bumped something overhead".
        bool hitCeiling = (controller.collisionFlags & CollisionFlags.Above) != 0;
        bool released = !Input.GetKey(KeyCode.Space) && jumpYOffset >= tapJumpHeight;

        if (t >= 1f || released || hitCeiling) EndJump(hitCeiling);
    }//end of function >:D


    // ADDED (3D pass): replaces the hand-off at the end of JumpCoroutine. verticalVelocity is
    // left as it is, so the fall starts at the rise's last speed and a short hop does not stop
    // dead in the air (what riseRate did before) - without the extra math.
    private void EndJump(bool hitCeiling)
    {
        isJumping = false;
        jumpYOffset = 0f;

        // Bumped a ceiling: drop at once instead of pressing into it for a few frames.
        if (hitCeiling) verticalVelocity = 0f;

        LogHeight("apex");
    }//end of function >:D


    // CHANGED (3D pass): takes a world Vector3 instead of a floor Vector2. The controller is
    // switched off for the teleport because, while it is on, it can overwrite a direct
    // transform.position change with its own stored position.
    public void ResetTo(Vector3 point)
    {
        isJumping = false;
        jumpYOffset = 0f;
        verticalVelocity = 0f;
        jumpMomentum = Vector3.zero;

        controller.enabled = false;
        transform.position = point;
        controller.enabled = true;

        LogHeight("reset");
    }// end of function >:D


    // CHANGED (3D pass): reports the real 3D height, the up/down speed and the ground check.
    private void LogHeight(string moment)
    {
        if (!logHeight) return;

        Debug.Log(
            $"[{moment}] y={transform.position.y:F3} jumpY={jumpYOffset:F3} " +
            $"vy={verticalVelocity:F3} grounded={controller.isGrounded} frame={Time.frameCount}", this);
    }// end of function >:D


    private void LogSprint(string moment, float gap)
    {
        if (!logSprint) return;

        Debug.Log(
            $"[sprint {moment}] gap={gap:F3}s window={doubleTapWindow:F3}s " +
            $"L={isSprintingLeft} R={isSprintingRight} moveX={moveInput.x} " +
            $"timeScale={Time.timeScale:F2} frame={Time.frameCount}", this);
    }// end of function >:D


    private void UpdateFacing()
    {
        if (animator == null || moveInput.x == 0f) return;
        animator.SetBool(FacingRightHash, moveInput.x > 0f);
    }



    private void UpdateAnimations()
    {
        /*TODOS
        logic similar to updateFacing that uses a list
        
        */
    }


    private void DetectDoubleTap()
    {
        if (Input.GetKeyDown(sprintKeyLeft))
        {
            float gap = Time.time - lastTapTimeLeft;
            if (gap < doubleTapWindow) isSprintingLeft = true;
            lastTapTimeLeft = Time.time;
            LogSprint("left down", gap);
        }

        if (Input.GetKeyDown(sprintKeyRight))
        {
            float gap = Time.time - lastTapTimeRight;
            if (gap < doubleTapWindow) isSprintingRight = true;
            lastTapTimeRight = Time.time;
            LogSprint("right down", gap);
        }

        if (Input.GetKeyUp(sprintKeyLeft))  { isSprintingLeft = false;  LogSprint("left up", 0f); }
        if (Input.GetKeyUp(sprintKeyRight)) { isSprintingRight = false; LogSprint("right up", 0f); }

        if (isSprintingLeft && !Input.GetKey(sprintKeyLeft))   LogSprint("STUCK left", 0f);
        if (isSprintingRight && !Input.GetKey(sprintKeyRight)) LogSprint("STUCK right", 0f);
    }


    private void ApplySprintSpeed()
    {
        if ((isSprintingLeft && moveInput.x < 0) || (isSprintingRight && moveInput.x > 0))
        {
            moveVelocity *= sprintMultiplier;
        }
    }


    // COMMENTED OUT (3D pass): works in 2D only. Casting position to Vector2 drops Z, so
    // the final "target.position = destination" would snap the object to Z = 0.
    // Also needs "using System.Collections;" back at the top if re-enabled.
    //
    // public IEnumerator MoveToPoint(Transform target, Vector2 destination)
    // {
    //     Vector2 positionDifference = destination - (Vector2)target.position;
    //
    //     while (positionDifference.magnitude > moveAccuracy)
    //     {
    //         target.Translate(moveSpeed * positionDifference.normalized * Time.deltaTime);
    //         positionDifference = destination - (Vector2)target.position;
    //         yield return null;
    //     }
    //
    //     target.position = destination;
    // } //end of function >:D
}