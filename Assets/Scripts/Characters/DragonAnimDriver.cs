using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Animator))]
public class DragonAnimDriver : MonoBehaviour
{
    Rigidbody2D rb;
    Animator anim;
    const string SpeedParam = "Speed";

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
    }

    void Update()
    {
        float speed = Mathf.Abs(rb.linearVelocity.x);
        anim.SetFloat(SpeedParam, speed);
    }
}
