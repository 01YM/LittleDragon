using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class DragonCombat : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] Animator animator;           // Visual's Animator
    [SerializeField] SpriteRenderer sprite;       // Visual's SpriteRenderer (optional flash)

    [Header("Input")]
    public bool enableInput = true;               // disable after death
    InputSystem_Actions input;

    [Header("Attack Settings")]
    [SerializeField] float attackCooldown = 0.35f;
    [SerializeField] float kickCooldown   = 0.45f;

    float _nextAttackTime, _nextKickTime;

    // Hashes
    static readonly int AttackHash = Animator.StringToHash("Attack");
    static readonly int KickHash   = Animator.StringToHash("Kick");
    static readonly int HurtHash   = Animator.StringToHash("Hurt");
    static readonly int DeathHash  = Animator.StringToHash("Death");
    static readonly int IsAliveHash= Animator.StringToHash("IsAlive");

    void Awake()
    {
        if (!animator) animator = GetComponentInChildren<Animator>(true);
        if (!sprite)   sprite   = GetComponentInChildren<SpriteRenderer>(true);
        if (!animator) Debug.LogError("DragonCombat: Animator not found. Assign Visual's Animator.");
    }

    void OnEnable()
    {
        input = new InputSystem_Actions();
        input.Enable();

        if (input.Player.Attack != null)
            input.Player.Attack.performed += OnAttack;
        if (input.Player.Kick != null)
            input.Player.Kick.performed   += OnKick;
    }

    void OnDisable()
    {
        if (input != null)
        {
            if (input.Player.Attack != null)
                input.Player.Attack.performed -= OnAttack;
            if (input.Player.Kick != null)
                input.Player.Kick.performed   -= OnKick;
            input.Disable();
        }
    }

    void OnAttack(InputAction.CallbackContext _)
    {
        if (!enableInput || Time.time < _nextAttackTime) return;
        _nextAttackTime = Time.time + attackCooldown;
        animator?.SetTrigger(AttackHash);
    }

    void OnKick(InputAction.CallbackContext _)
    {
        if (!enableInput || Time.time < _nextKickTime) return;
        _nextKickTime = Time.time + kickCooldown;
        animator?.SetTrigger(KickHash);
    }

    // --- Public API for other systems ---
    public void TakeHurt()
    {
        if (!enableInput) return;
        animator?.SetTrigger(HurtHash);
        // (optional) brief input lock, i-frames, flash, etc.
    }

    public void Die()
    {
        if (!enableInput) return;
        enableInput = false;
        animator?.SetBool(IsAliveHash, false);
        animator?.SetTrigger(DeathHash);
        // (optional) disable mover / collider here
        var mover = GetComponent<DragonMover>();
        if (mover) mover.enabled = false;
    }
}
