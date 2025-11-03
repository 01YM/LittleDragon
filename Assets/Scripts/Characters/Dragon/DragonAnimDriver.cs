using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))] // Animator is now on a child
public class DragonAnimDriver : MonoBehaviour
{
    [SerializeField] Animator animator;   // drag the Visual’s Animator here (or auto-find)
    Rigidbody2D rb;

    static readonly int SpeedParam = Animator.StringToHash("Speed");

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (!animator) animator = GetComponentInChildren<Animator>(true); // finds Visual’s Animator
    }

    void Update()
    {
        if (!rb || !animator) return;
        animator.SetFloat(SpeedParam, Mathf.Abs(rb.linearVelocity.x));
    }
}