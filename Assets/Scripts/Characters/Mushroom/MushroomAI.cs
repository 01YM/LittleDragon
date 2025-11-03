using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Animator))]
public class MushroomAI : MonoBehaviour
{
    [Header("Move")]
    public float patrolSpeed = 1.5f;
    public float chaseSpeed = 2.25f;
    public Transform leftEdge;   // optional waypoint
    public Transform rightEdge;  // optional waypoint
    public LayerMask groundMask;
    public Transform groundCheck;    // empty under feet
    public float groundCheckDist = 0.2f;

    [Header("Detect / Attack")]
    public Transform player;
    public float detectRadius = 4f;
    public float attackRange = 0.8f;
    public float attackCooldown = 0.8f;
    public int contactDamage = 1;

    Rigidbody2D rb;
    Animator anim;
    bool facingRight = true;
    float cooldown;

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
        // AttackRange trigger uses same radius as attackRange
        var trg = GetComponents<Collider2D>();
        foreach (var c in trg) if (c.isTrigger) c.offset = Vector2.zero;
    }

    void Update()
    {
        cooldown -= Time.deltaTime;
        bool hasPlayer = player != null;

        // Acquire player if missing (cheap search)
        if (!hasPlayer)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p) { player = p.transform; hasPlayer = true; }
        }

        float speed = patrolSpeed;
        Vector2 v = rb.linearVelocity;
        bool moving = Mathf.Abs(v.x) > 0.02f;
        anim.SetBool("IsMoving", moving);

        if (hasPlayer && Vector2.Distance(transform.position, player.position) <= detectRadius)
        {
            // Chase
            speed = chaseSpeed;
            float dir = Mathf.Sign(player.position.x - transform.position.x);
            Move(dir * speed);

            // Face target
            if ((dir > 0 && !facingRight) || (dir < 0 && facingRight)) Flip();

            // Try attack
            if (Vector2.Distance(transform.position, player.position) <= attackRange && cooldown <= 0f)
            {
                anim.SetTrigger("Attack");
                cooldown = attackCooldown;
            }
            return;
        }

        // Patrol between edges if provided; else tiny left-right loop on ledge
        float dirPatrol = facingRight ? 1f : -1f;
        Move(dirPatrol * speed);

        // Turn at edges or walls
        bool noGroundAhead = !Physics2D.Raycast(groundCheck.position, Vector2.down, groundCheckDist, groundMask);
        bool pastRight = rightEdge && transform.position.x > rightEdge.position.x;
        bool pastLeft  = leftEdge  && transform.position.x < leftEdge.position.x;
        if (noGroundAhead || pastRight || pastLeft) Flip();
    }

    void Move(float xSpeed)
    {
        rb.linearVelocity = new Vector2(xSpeed, rb.linearVelocity.y);
    }

    void Flip()
    {
        facingRight = !facingRight;
        Vector3 s = transform.localScale; s.x *= -1f; transform.localScale = s;
    }

    // Called by Attack animation via Animation Event at the hit frame.
    public void DoAttackHit()
    {
        // Damage anything with IDamageable in a small circle
        var hits = Physics2D.OverlapCircleAll(transform.position, attackRange, ~0);
        foreach (var h in hits)
        {
            if (h.attachedRigidbody && h.attachedRigidbody.gameObject == gameObject) continue;
            var dmg = h.GetComponent<IDamageable>();
            if (dmg != null) dmg.TakeDamage(contactDamage, (h.transform.position - transform.position).normalized * 5f);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireSphere(transform.position, detectRadius);
        Gizmos.DrawWireSphere(transform.position, attackRange);
        if (groundCheck) Gizmos.DrawLine(groundCheck.position, groundCheck.position + Vector3.down * groundCheckDist);
    }
}
