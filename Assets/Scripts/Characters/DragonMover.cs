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
    public float flyImpulse = 10f;         // upward boost when entering flight
    public float doubleTapTime = 0.35f;    // time window for the 2nd tap

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
    bool jumpHeld;   // Space held (for cut/glide)
    bool runHeld;    // LeftShift
    bool crawlHeld;  // Down + horiz
    bool hideHeld;   // Down only

    // Attacks
    bool kickPressed;
    bool attackPressed;

    // Grounding
    bool grounded;

    // Flight
    bool isFlying;
    float lastGroundedTime;
    float lastJumpPressedTime;
    float lastJumpTapTime;   // first tap timestamp to detect double tap

    // Animator hashes
    static readonly int JumpHash = Animator.StringToHash("Jump");       // Trigger
    static readonly int IsFlyingHash = Animator.StringToHash("IsFlying");
    static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
    static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    static readonly int IsRunningHash = Animator.StringToHash("IsRunning");
    static readonly int IsCrawlingHash = Animator.StringToHash("IsCrawling");
    static readonly int IsHidingHash = Animator.StringToHash("IsHiding");
    static readonly int SpeedHash = Animator.StringToHash("Speed");
    static readonly int VSpeedHash = Animator.StringToHash("VSpeed");

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = animator ? animator : GetComponent<Animator>();
        sprite = sprite ? sprite : GetComponent<SpriteRenderer>();

        input = new InputSystem_Actions();
        rb.gravityScale = normalGravityScale;
    }

    void OnEnable()
    {
        input.Enable();

        input.Player.Move.performed += OnMove;
        input.Player.Move.canceled  += OnMove;

        input.Player.Jump.performed += OnJumpPerformed;   // MUST be a "Press" interaction
        input.Player.Jump.canceled  += OnJumpCanceled;

        input.Player.Run.performed += OnRunPerformed;
        input.Player.Run.canceled  += OnRunCanceled;

        input.Player.Kick.performed   += OnKickPerformed;
        input.Player.Attack.performed += OnAttackPerformed;
    }

    void OnDisable()
    {
        input.Player.Move.performed -= OnMove;
        input.Player.Move.canceled  -= OnMove;

        input.Player.Jump.performed -= OnJumpPerformed;
        input.Player.Jump.canceled  -= OnJumpCanceled;

        input.Player.Run.performed  -= OnRunPerformed;
        input.Player.Run.canceled   -= OnRunCanceled;

        input.Player.Kick.performed   -= OnKickPerformed;
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
    }

    void OnJumpPerformed(InputAction.CallbackContext ctx)
    {
        jumpHeld = true;
        lastJumpPressedTime = jumpBuffer; // buffer press for Update()

        bool groundedNow = IsGrounded();
        float now = Time.time;

        // 1) Grounded: jump immediately
        if (groundedNow && !isFlying && !hideHeld && !crawlHeld)
        {
            Jump();
            lastJumpPressedTime = 0f;
            lastJumpTapTime = now; // mark first tap
            if (debugLogs) Debug.Log("Jump: grounded first tap");
            return;
        }

        // 2) In air: if second tap within window, enter flight
        if (!groundedNow && !isFlying && (now - lastJumpTapTime) <= doubleTapTime)
        {
            EnterFlight();
            if (debugLogs) Debug.Log("Flight: entered on 2nd tap");
            return;
        }

        // 3) In air but not within window → remember this tap as potential first tap
        if (!groundedNow && !isFlying)
        {
            lastJumpTapTime = now;
            if (debugLogs) Debug.Log("Air tap recorded (waiting for 2nd tap)");
        }
    }

    void Update()
    {
        grounded = IsGrounded();

        // timers: coyote & buffer
        if (grounded) lastGroundedTime = coyoteTime; else lastGroundedTime -= Time.deltaTime;
        if (lastJumpPressedTime > 0f) lastJumpPressedTime -= Time.deltaTime;

        // buffered/coyote jump
        if (lastGroundedTime > 0f && lastJumpPressedTime > 0f && !isFlying && !hideHeld && !crawlHeld)
        {
            Jump();
            lastGroundedTime = 0f;
            lastJumpPressedTime = 0f;
            lastJumpTapTime = Time.time; // treat as first tap
            if (debugLogs) Debug.Log("Jump: buffered/coyote");
        }

        // variable jump height (cut on release)
        if (!jumpHeld && rb.linearVelocity.y > 0f)
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y * jumpCutMultiplier);

        // sprite flip
        if (sprite && Mathf.Abs(moveInput.x) > 0.01f)
            sprite.flipX = moveInput.x < 0f;

        // flight behaviour
        if (isFlying)
        {
            if (jumpHeld)
            {
                // glide (slow fall when holding)
                rb.gravityScale = glideGravityScale;
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

        // exit flight when grounding
        if (grounded && isFlying)
        {
            isFlying = false;
            rb.gravityScale = normalGravityScale;
            animator?.SetBool(IsFlyingHash, false);
            if (debugLogs) Debug.Log("Flight: exited (grounded)");
        }

        // locomotion calc
        bool moving = Mathf.Abs(moveInput.x) > 0.01f;
        bool down = moveInput.y < -0.5f;
        bool horiz = Mathf.Abs(moveInput.x) > 0.1f;

        hideHeld  = down && !horiz;   // down only
        crawlHeld = down && horiz;    // down + horiz
        bool running = runHeld && grounded && moving;

        // animator params
        if (animator)
        {
            animator.SetBool(IsGroundedHash, grounded);
            animator.SetBool(IsMovingHash, moving);
            animator.SetBool(IsRunningHash, running);
            animator.SetBool(IsCrawlingHash, crawlHeld);
            animator.SetBool(IsHidingHash,  hideHeld);
            animator.SetFloat(SpeedHash, Mathf.Abs(rb.linearVelocity.x), 0.06f, Time.deltaTime);
            animator.SetFloat(VSpeedHash, rb.linearVelocity.y);

            if (kickPressed)   { animator.SetTrigger("Kick");   kickPressed = false; }
            if (attackPressed) { animator.SetTrigger("Attack"); attackPressed = false; }
        }
    }

    void FixedUpdate()
    {
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

    void Jump()
    {
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        if (animator) animator.SetTrigger(JumpHash);
    }

    void EnterFlight()
    {
        isFlying = true;
        rb.gravityScale = flyGravityScale;
        // give a one-off upward impulse (but don't reduce existing upward velocity)
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, Mathf.Max(rb.linearVelocity.y, flyImpulse));
        if (animator) animator.SetBool(IsFlyingHash, true);
    }

    bool IsGrounded()
    {
        if (!groundCheck) return false;
        return Physics2D.OverlapCircle(groundCheck.position, groundRadius, groundLayer) != null;
    }

    void OnDrawGizmosSelected()
    {
        if (!groundCheck) return;
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(groundCheck.position, groundRadius);
    }
}
