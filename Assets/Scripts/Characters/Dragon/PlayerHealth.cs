using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    public int maxHP = 3;
    public float invuln = 0.5f; // brief grace period
    int hp;
    float t;

    void Awake() { hp = maxHP; }

    public void TakeDamage(int amount, Vector2 knockback)
    {
        if (Time.time < t) return;
        hp -= amount;
        t = Time.time + invuln;

        // simple feedback
        var rb = GetComponent<Rigidbody2D>();
        if (rb) rb.linearVelocity = new Vector2(knockback.x, Mathf.Max(knockback.y, rb.linearVelocity.y));

        if (hp <= 0) { Debug.Log("Player died"); /* call your respawn here */ }
    }
}
