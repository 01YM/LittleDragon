using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(SpriteRenderer))]
public class DragonMover : MonoBehaviour
{
    [Header("Move")]
    public float walkSpeed = 3.5f;
    public float runSpeed = 6f;
    public float crawlSpeed = 1.75f;
    public float acceleration = 60f;
    public float deceleration = 70f;
    public float airControlFactor = 0.5f;

    [Header("Jump")]
    public float jumpForce = 8f;
    public float coyoteTime = 0.1f;
    public float jumpBuffer = 0.1f;
    [Range(0f, 1f)] public float jumpCutMultiplier = 0.5f;

    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundRadius = 0.15f;
    public LayerMask groundLayer;

    [Header("Fly")]
    public float flyImpulse = 10f;         // flight burst per tap
    public float maxFlapUpSpeed = 6f;      // cap for upward speed from repeated flaps

    [Header("Water")]
    [SerializeField] LayerMask waterLayer;
    [SerializeField] float swimSpeed = 3f;
    [SerializeField] float swimVerticalSpeed = 3f;
    [SerializeField] float swimAccel = 12f;
    [SerializeField] float waterLinearDamping = 4f;  // use linearDamping (Unity 6)
    [SerializeField] float waterGravityScale = 0f;   // 0 = neutral buoyancy
    [SerializeField] float sinkSpeed = 1.25f;        // slow sink when no input

    // --- Water surface control ---
    [SerializeField] float surfacePadding = 0.02f;   // how far below the visual top the cap sits
    [SerializeField] float surfaceStickEpsilon = 0.005f; // hysteresis to avoid flicker

    [Header("Gravity")]
    public float normalGravityScale = 2.5f;
    public float flyGravityScale = 0.8f;   // normal descent in flight
    public float glideGravityScale = 0.3f; // when holding jump in flight
    public float maxGlideFallSpeed = -2f;

    [Header("Visuals")]
    public SpriteRenderer sprite;          // auto-fetched in Awake if null
    public Animator animator;              // auto-fetched in Awake if null

    [Header("Debug")]
    public bool debugLogs = false;

    // --- internals ---
    Rigidbody2D rb;
    InputSystem_Actions input;

    Vector2 moveInput;

    // Locomotion state
    bool jumpHeld;           // Space held (for cut/glide)
    bool jumpedSinceGround;  // true after a grounded jump, cleared on landing
    bool runHeld;            // LeftShift
    bool crawlHeld;          // Down + horiz
    bool hideHeld;           // Down only

    // Attacks
    bool kickPressed;
    bool attackPressed;

    // Grounding
    bool grounded;

    // Flight
    bool isFlying;
    float lastGroundedTime;
    float lastJumpPressedTime;

    // Water
    bool _inWater;
    float _origGravity, _origLinearDamping;
    
    Collider2D _waterCollider;   // the trigger we’re inside
    float _waterSurfaceY;        // world Y of the surface (cap)
    bool _isAtSurface;

    // Animator hashes
    static readonly int JumpHash = Animator.StringToHash("Jump");       // Trigger
    static readonly int IsFlyingHash   = Animator.StringToHash("IsFlying");
    static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
    static readonly int IsMovingHash   = Animator.StringToHash("IsMoving");
    static readonly int IsRunningHash  = Animator.StringToHash("IsRunning");
    static readonly int IsCrawlingHash = Animator.StringToHash("IsCrawling");
    static readonly int IsHidingHash   = Animator.StringToHash("IsHiding");
    static readonly int SpeedHash      = Animator.StringToHash("Speed");
    static readonly int VSpeedHash     = Animator.StringToHash("VSpeed");

    // Water animator params (add these in Animator)
    static readonly int IsInWaterHash  = Animator.StringToHash("IsInWater");  // Bool
    static readonly int SwimDirHash    = Animator.StringToHash("SwimDir");    // Int: -1 Down, 0 Side/Idle, +1 Up
    static readonly int IsAtSurfaceHash= Animator.StringToHash("IsAtSurface");

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = animator ? animator : GetComponent<Animator>();
        sprite = sprite ? sprite : GetComponent<SpriteRenderer>();

        input = new InputSystem_Actions();
        rb.gravityScale = normalGravityScale;
        _origGravity = rb.gravityScale;
        _origLinearDamping = rb.linearDamping;
    }

    void OnEnable()
    {
        input.Enable();

        input.Player.Move.performed += OnMove;
        input.Player.Move.canceled  += OnMove;

        input.Player.Jump.performed += OnJumpPerformed;
        input.Player.Jump.canceled  += OnJumpCanceled;

        if (input.Player.Run != null) {
            input.Player.Run.performed += OnRunPerformed;
            input.Player.Run.canceled  += OnRunCanceled;
        } else if (debugLogs) Debug.LogWarning("Input action 'Run' missing.");

        if (input.Player.Kick != null)
            input.Player.Kick.performed += OnKickPerformed;
        else if (debugLogs) Debug.LogWarning("Input action 'Kick' missing.");

        if (input.Player.Attack != null)
            input.Player.Attack.performed += OnAttackPerformed;
        else if (debugLogs) Debug.LogWarning("Input action 'Attack' missing.");
    }

    void OnDisable()
    {
        // Unsubscribe only if actions exist
        input.Player.Move.performed -= OnMove;
        input.Player.Move.canceled  -= OnMove;

        input.Player.Jump.performed -= OnJumpPerformed;
        input.Player.Jump.canceled  -= OnJumpCanceled;

        if (input.Player.Run != null) {
            input.Player.Run.performed -= OnRunPerformed;
            input.Player.Run.canceled  -= OnRunCanceled;
        }
        if (input.Player.Kick != null)
            input.Player.Kick.performed -= OnKickPerformed;
        if (input.Player.Attack != null)
            input.Player.Attack.performed -= OnAttackPerformed;

        input.Disable();
    }

    // ----- Input handlers -----
    void OnMove(InputAction.CallbackContext ctx) => moveInput = ctx.ReadValue<Vector2>();
    void OnRunPerformed(InputAction.CallbackContext ctx) => runHeld = true;
    void OnRunCanceled (InputAction.CallbackContext ctx) => runHeld = false;
    void OnKickPerformed(InputAction.CallbackContext ctx) { kickPressed = true; }
    void OnAttackPerformed(InputAction.CallbackContext ctx) { attackPressed = true; }

    void OnJumpCanceled(InputAction.CallbackContext ctx)
    {
        jumpHeld = false; // releases glide / enables jump cut

        // TAP = flap (only while flying on land/air)
        if (!_inWater && isFlying && !IsGrounded())
        {
            Flap();
            if (debugLogs) Debug.Log("Flight: flap on release");
        }
    }

    void OnJumpPerformed(InputAction.CallbackContext ctx)
    {
        jumpHeld = true;
        lastJumpPressedTime = jumpBuffer; // keep your coyote/buffer

        // Disable jump/flight transitions while in water (swim handles vertical)
        if (_inWater) return;

        bool groundedNow = IsGrounded();

        // First press while grounded: ALWAYS jump
        if (groundedNow && !isFlying && !hideHeld && !crawlHeld)
        {
            Jump();
            jumpedSinceGround = true;    // we’ve done our grounded jump
            lastJumpPressedTime = 0f;
            if (debugLogs) Debug.Log("Jump: grounded");
            return;
        }

        // Second press while airborne (before landing): enter flight
        if (!groundedNow && jumpedSinceGround && !isFlying)
        {
            EnterFlight();
            if (debugLogs) Debug.Log("Flight: entered on airborne second press");
            return;
        }
    }

    void Update()
    {
        bool wasGrounded = grounded;
        grounded = IsGrounded();

        // Start the double-tap window when we actually leave the ground
        if (!wasGrounded && grounded)
        {
            jumpedSinceGround = false;   // allow the next cycle to be “press once jump, press again flight”
            animator?.ResetTrigger(JumpHash); // optional safety
        }

        // timers: coyote & buffer
        if (grounded) lastGroundedTime = coyoteTime; else lastGroundedTime -= Time.deltaTime;
        if (lastJumpPressedTime > 0f) lastJumpPressedTime -= Time.deltaTime;

        // buffered/coyote jump (disabled while in water)
        if (!_inWater && lastGroundedTime > 0f && lastJumpPressedTime > 0f && !isFlying && !hideHeld && !crawlHeld)
        {
            Jump();
            lastGroundedTime = 0f;
            lastJumpPressedTime = 0f;
            if (debugLogs) Debug.Log("Jump: buffered/coyote");
        }

        // variable jump height (cut on release)
        if (!_inWater && !jumpHeld && !isFlying && rb.linearVelocity.y > 0f)
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y * jumpCutMultiplier);

        // sprite flip
        if (sprite && Mathf.Abs(moveInput.x) > 0.01f)
            sprite.flipX = moveInput.x < 0f;

        // Gravity handling
        if (_inWater)
        {
            rb.gravityScale = waterGravityScale;
        }
        else if (isFlying)
        {
            if (jumpHeld)
            {
                // glide (slow fall when holding)
                rb.gravityScale = glideGravityScale;
                if (rb.linearVelocity.y > 0f)
                    rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f); // kill upward motion while held
                if (rb.linearVelocity.y < maxGlideFallSpeed)
                    rb.linearVelocity = new Vector2(rb.linearVelocity.x, maxGlideFallSpeed);
            }
            else
            {
                rb.gravityScale = flyGravityScale;
            }
        }
        else
        {
            rb.gravityScale = normalGravityScale;
        }

        // exit flight when grounding (land/air only)
        if (!_inWater && grounded && isFlying)
        {
            isFlying = false;
            rb.gravityScale = normalGravityScale;
            animator?.SetBool(IsFlyingHash, false);
            if (debugLogs) Debug.Log("Flight: exited (grounded)");
        }

        // locomotion calc (land/air booleans)
        bool moving = Mathf.Abs(moveInput.x) > 0.01f;
        bool down = moveInput.y < -0.5f;
        bool horiz = Mathf.Abs(moveInput.x) > 0.1f;

        hideHeld  = down && !horiz;   // down only
        crawlHeld = down && horiz;    // down + horiz
        bool running = runHeld && grounded && moving;

        // Animator params (always set)
        if (animator)
        {
            animator.SetBool(IsGroundedHash, grounded && !_inWater); // treat water as not grounded
            animator.SetBool(IsMovingHash, moving);
            animator.SetBool(IsRunningHash, running);
            animator.SetBool(IsCrawlingHash, !_inWater && crawlHeld);
            animator.SetBool(IsHidingHash,  !_inWater && hideHeld);
            animator.SetFloat(SpeedHash,  Mathf.Abs(rb.linearVelocity.x), 0.06f, Time.deltaTime);
            animator.SetFloat(VSpeedHash, rb.linearVelocity.y);

            // --- Water-specific animator params ---
            animator.SetBool(IsInWaterHash, _inWater);
            animator.SetBool(IsAtSurfaceHash, _isAtSurface);

            // Force Side at surface; only allow Up/Down when NOT at surface
            int swimDir = 0; // Side/Idle by default
            if (_inWater && !_isAtSurface)
            {
                if (moveInput.y >  0.1f)      swimDir = 1;   // Up
                else if (moveInput.y < -0.1f) swimDir = -1;  // Down
            }
            animator.SetInteger(SwimDirHash, swimDir);

            if (kickPressed)   { animator.SetTrigger("Kick");   kickPressed = false; }
            if (attackPressed) { animator.SetTrigger("Attack"); attackPressed = false; }
        }
    }

    void FixedUpdate()
    {
        // WATER: handle swimming movement and exit
        if (_inWater)
        {
            SwimStep();
            return;
        }

        // --- Original ground/air horizontal movement ---
        float maxSpd;

        if (crawlHeld)
        {
            maxSpd = runHeld ? walkSpeed : crawlSpeed;
        }
        else
        {
            maxSpd = runHeld ? runSpeed : walkSpeed;
        }

        float targetSpeed = moveInput.x * maxSpd;
        if (Mathf.Abs(moveInput.x) < 0.01f) targetSpeed = 0f; // no tiny drift

        float speedDelta = targetSpeed - rb.linearVelocity.x;
        float accel = grounded ? (Mathf.Abs(targetSpeed) > 0.01f ? acceleration : deceleration)
                               : acceleration * airControlFactor;

        float change = Mathf.Clamp(speedDelta, -accel * Time.fixedDeltaTime, accel * Time.fixedDeltaTime);
        rb.linearVelocity = new Vector2(rb.linearVelocity.x + change, rb.linearVelocity.y);
    }

    // ----- Water movement step -----
    void SwimStep()
    {
        // Full 2D swim when there is input; otherwise sink slowly
        float targetX = moveInput.x * swimSpeed;
        float targetY = (Mathf.Abs(moveInput.y) > 0.01f) ? moveInput.y * swimVerticalSpeed : -sinkSpeed;

        // Surface cap
        _isAtSurface = false;
        if (_waterCollider != null)
        {
            UpdateWaterSurface();

            // If we're at/above the cap, don't allow upward movement and clamp velocity.
            if (transform.position.y >= _waterSurfaceY - surfaceStickEpsilon)
            {
                if (targetY > 0f) targetY = 0f;             // ignore upward input
                if (rb.linearVelocity.y > 0f)               // prevent popping out
                    rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);

                // Snap slightly inside if we drift above the cap due to integration
                if (transform.position.y > _waterSurfaceY)
                    transform.position = new Vector3(transform.position.x, _waterSurfaceY, transform.position.z);

                _isAtSurface = true;
            }
        }

        Vector2 targetVel = new Vector2(targetX, targetY);
        rb.linearVelocity = Vector2.MoveTowards(
            rb.linearVelocity, targetVel, swimAccel * Time.fixedDeltaTime);

        // Face by horizontal motion
        if (sprite && Mathf.Abs(rb.linearVelocity.x) > 0.01f)
            sprite.flipX = rb.linearVelocity.x < 0f;
    }

    void Jump()
    {
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        if (animator) animator.SetTrigger(JumpHash);
    }

    void Flap()
    {
        float newVy = rb.linearVelocity.y + flyImpulse;      // add a small burst
        if (newVy > maxFlapUpSpeed) newVy = maxFlapUpSpeed;  // cap upward speed
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, newVy);
    }

    void EnterFlight()
    {
        isFlying = true;
        rb.gravityScale = flyGravityScale;
        // give a one-off upward impulse (but don't reduce existing upward velocity)
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, Mathf.Min(rb.linearVelocity.y, 0f)); // no upward on entry
        if (animator) animator.SetBool(IsFlyingHash, true);
    }

    bool IsGrounded()
    {
        if (!groundCheck) return false;
        return Physics2D.OverlapCircle(groundCheck.position, groundRadius, groundLayer) != null;
    }

    // ----- Water triggers -----
    void OnTriggerEnter2D(Collider2D other)
    {
        if (IsWater(other.gameObject.layer))
        {
            _waterCollider = other;
            EnterWater();
        }
    }

    void OnTriggerStay2D(Collider2D other)
    {
        if (_inWater && other == _waterCollider) UpdateWaterSurface();
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other == _waterCollider) ExitWater();
    }
    bool IsWater(int layer) => (waterLayer.value & (1 << layer)) != 0;

    void EnterWater()
    {
        _inWater = true;
        isFlying = false; // cancel flight in water
        rb.gravityScale  = waterGravityScale;
        rb.linearDamping = waterLinearDamping;
        if (animator)
        {
            animator.SetBool(IsFlyingHash, false);
            animator.SetBool(IsInWaterHash, true);
        }
        UpdateWaterSurface();
        if (debugLogs) Debug.Log("Entered water");
    }

    void ExitWater()
    {
        _inWater = false;
        _isAtSurface = false;
        _waterCollider = null;
        rb.gravityScale  = _origGravity;
        rb.linearDamping = _origLinearDamping;
        if (animator) animator.SetBool(IsInWaterHash, false);
        if (debugLogs) Debug.Log("Exited water");
    }

    void UpdateWaterSurface()
    {
        if (_waterCollider != null)
            _waterSurfaceY = _waterCollider.bounds.max.y - surfacePadding;
    }

    void OnDrawGizmosSelected()
    {
        if (!groundCheck) return;
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(groundCheck.position, groundRadius);
    }
}
