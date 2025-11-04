using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Animator))]
public class MushroomAI : MonoBehaviour
{
    public enum State { PatrolWalk, PatrolIdle, Chase }

    [Header("Move")]
    public float patrolSpeed = 1.5f;
    public float chaseSpeed = 2.75f;
    public float walkTime = 2.0f;   // seconds walking before idle
    public float idleTime = 1.0f;   // seconds idling before flip
    public Transform leftEdge;
    public Transform rightEdge;
    public LayerMask groundMask;
    public Transform groundCheck;
    public float groundCheckDist = 0.25f;

    [Header("Detect / Attack")]
    public Transform player;              // auto-found by "Player" tag if null
    public float detectRadius = 4f;
    public float attackRange = 0.8f;
    public float attackCooldown = 0.8f;
    public int contactDamage = 1;
    public LayerMask playerMask;

    Rigidbody2D rb;
    Animator anim;

    // internals
    State state = State.PatrolWalk;
    bool facingRight = true;
    float timer;               // counts time in current state
    float cooldown;            // attack cd
    float targetXSpeed;        // applied in FixedUpdate

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        if (!groundCheck)
        {
            groundCheck = new GameObject("GroundCheck").transform;
            groundCheck.SetParent(transform);
            groundCheck.localPosition = new Vector3(0, -0.5f, 0);
        }
    }

    void Start()
    {
        if (!player)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p) player = p.transform;
        }
        EnterState(State.PatrolWalk);
    }

    void Update()
    {
        cooldown -= Time.deltaTime;
        timer += Time.deltaTime;

        // reacquire player if missing
        if (!player)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p) player = p.transform;
        }

        // Chase detection gate (unless you want stun/dead, etc.)
        bool canSeePlayer = player && Vector2.Distance(transform.position, player.position) <= detectRadius;

        switch (state)
        {
            case State.PatrolWalk:
                if (canSeePlayer) { EnterState(State.Chase); break; }

                // walk forward; flip if edge or bounds reached
                targetXSpeed = (facingRight ? 1f : -1f) * patrolSpeed;

                bool noGroundAhead = !Physics2D.Raycast(groundCheck.position, Vector2.down, groundCheckDist, groundMask);
                bool pastRight = rightEdge && transform.position.x > rightEdge.position.x;
                bool pastLeft  = leftEdge  && transform.position.x < leftEdge.position.x;
                if (noGroundAhead || pastRight || pastLeft) { Flip(); EnterState(State.PatrolIdle); break; }

                if (timer >= walkTime) { EnterState(State.PatrolIdle); }
                break;

            case State.PatrolIdle:
                if (canSeePlayer) { EnterState(State.Chase); break; }
                // stand still
                targetXSpeed = 0f;
                if (timer >= idleTime) { Flip(); EnterState(State.PatrolWalk); }
                break;

            case State.Chase:
                if (!canSeePlayer) { EnterState(State.PatrolIdle); break; }
                float dir = Mathf.Sign(player.position.x - transform.position.x);
                targetXSpeed = dir * chaseSpeed;
                if ((dir > 0 && !facingRight) || (dir < 0 && facingRight)) Flip();

                // attack if close (optional, keeps your animator trigger)
                if (Vector2.Distance(transform.position, player.position) <= attackRange && cooldown <= 0f)
                {
                    anim.SetTrigger("Attack");
                    cooldown = attackCooldown;
                }
                break;
        }

        // drive animator flag strictly from intent (not physics jitter)
        anim.SetBool("IsMoving", Mathf.Abs(targetXSpeed) > 0.01f);
    }

    void FixedUpdate()
    {
        // apply horizontal speed; keep current vertical
        rb.linearVelocity = new Vector2(targetXSpeed, rb.linearVelocity.y);

        // if idling, hard-stop tiny drift
        if (state == State.PatrolIdle && Mathf.Abs(rb.linearVelocity.x) > 0.001f)
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
    }

    void EnterState(State s)
    {
        state = s;
        timer = 0f;
    }

    void Flip()
    {
        facingRight = !facingRight;
        var s = transform.localScale; s.x *= -1f; transform.localScale = s;
    }

    // Animation Event on Attack hit frame
    public void DoAttackHit()
    {
        Vector2 center = (Vector2)transform.position + new Vector2(facingRight ? 0.6f : -0.6f, 0.1f);
        var hits = Physics2D.OverlapCircleAll(center, attackRange * 0.75f, playerMask);
        foreach (var h in hits)
        {
            var hp = h.GetComponent<PlayerHealth>();
            if (hp) hp.TakeDamage(contactDamage, (h.transform.position - transform.position).normalized * 6f);

            var prb = h.attachedRigidbody;
            if (prb) prb.linearVelocity = new Vector2((h.transform.position.x > transform.position.x ? 1 : -1) * 6f, 4f);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, detectRadius);
        Gizmos.color = Color.yellow;
        Vector3 gc = groundCheck ? groundCheck.position : transform.position + Vector3.down * 0.5f;
        Gizmos.DrawLine(gc, gc + Vector3.down * groundCheckDist);
    }
}
