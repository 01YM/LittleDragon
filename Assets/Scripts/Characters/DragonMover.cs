using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class DragonMover : MonoBehaviour
{
    [Header("Move")]
    public float maxSpeed = 6f;
    public float acceleration = 60f;
    public float deceleration = 70f;
    public float airControlFactor = 0.5f;   // less control in air

    [Header("Jump")]
    public float jumpForce = 12f;
    public float coyoteTime = 0.1f;         // grace after leaving ground
    public float jumpBuffer = 0.1f;         // press jump slightly early
    public float jumpCutMultiplier = 0.5f;  // variable jump height

    [Header("Ground Check")]
    public Transform groundCheck;           // assign: Dragon/GroundCheck
    public float groundRadius = 0.15f;
    public LayerMask groundLayer;           // set to Ground layer

    [Header("Visuals")]
    public SpriteRenderer sprite;           // assign Dragon SpriteRenderer
    public Animator animator;               // assign Animator (with Speed param)

    Rigidbody2D rb;
    InputSystem_Actions input;
    Vector2 moveInput;
    bool jumpPressed;
    bool jumpHeld;

    float lastGroundedTime;
    float lastJumpPressedTime;

    const string SpeedParam = "Speed";

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        input = new InputSystem_Actions();
    }

    void OnEnable()
    {
        input.Enable();
        input.Player.Move.performed += OnMove;
        input.Player.Move.canceled  += OnMove;
        input.Player.Jump.performed += OnJumpPerformed;
        input.Player.Jump.canceled  += OnJumpCanceled;
    }

    void OnDisable()
    {
        input.Player.Move.performed -= OnMove;
        input.Player.Move.canceled  -= OnMove;
        input.Player.Jump.performed -= OnJumpPerformed;
        input.Player.Jump.canceled  -= OnJumpCanceled;
        input.Disable();
    }

    void OnMove(InputAction.CallbackContext ctx)  => moveInput = ctx.ReadValue<Vector2>();
    void OnJumpPerformed(InputAction.CallbackContext ctx) { jumpPressed = true; jumpHeld = true; lastJumpPressedTime = jumpBuffer; }
    void OnJumpCanceled (InputAction.CallbackContext ctx) { jumpHeld = false; }

    void Update()
    {
        // Timers for coyote & buffer
        if (IsGrounded()) lastGroundedTime = coyoteTime;
        else              lastGroundedTime -= Time.deltaTime;

        if (lastJumpPressedTime > 0f) lastJumpPressedTime -= Time.deltaTime;

        // Try to jump when allowed
        if (lastGroundedTime > 0f && lastJumpPressedTime > 0f)
        {
            Jump();
            lastJumpPressedTime = 0f;
            lastGroundedTime = 0f;
        }

        // Variable jump height (cut if released early)
        if (!jumpHeld && rb.linearVelocity.y > 0f)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y * jumpCutMultiplier);
        }

        // Flip sprite by move direction
        if (sprite != null && Mathf.Abs(moveInput.x) > 0.01f)
            sprite.flipX = moveInput.x < 0f;

        // --- Animator params ---
        if (animator != null)
        {
            // INPUT-BASED for transitions (prevents Idle flash on direction change)
            float inputMag = Mathf.Abs(moveInput.x); // 0..1 from Input System
            animator.SetFloat("Speed", inputMag, 0.06f, Time.deltaTime);

            // VELOCITY-BASED for playback speed (no foot sliding)
            float velMag     = Mathf.Abs(rb.linearVelocity.x);
            float normalized = Mathf.Clamp01(velMag / maxSpeed);
            animator.SetFloat("AnimSpeed", Mathf.Max(0.3f, normalized));
        }
    }
    void FixedUpdate()
    {
        float targetSpeed = moveInput.x * maxSpeed;
        float speedDelta = targetSpeed - rb.linearVelocity.x;

        float accel = IsGrounded()
            ? (Mathf.Abs(targetSpeed) > 0.01f ? acceleration : deceleration)
            : acceleration * airControlFactor;

        float movement = Mathf.Clamp(speedDelta, -accel * Time.fixedDeltaTime, accel * Time.fixedDeltaTime);
        rb.linearVelocity = new Vector2(rb.linearVelocity.x + movement, rb.linearVelocity.y);
    }

    void Jump()
    {
        // set Y velocity directly for a crisp jump
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
    }

    bool IsGrounded()
    {
        if (groundCheck == null) return false;
        return Physics2D.OverlapCircle(groundCheck.position, groundRadius, groundLayer) != null;
    }

    void OnDrawGizmosSelected()
    {
        if (groundCheck == null) return;
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(groundCheck.position, groundRadius);
    }
}
