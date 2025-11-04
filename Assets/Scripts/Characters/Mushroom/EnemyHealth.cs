using UnityEngine;

[RequireComponent(typeof(Animator))]
public class EnemyHealth : MonoBehaviour, IDamageable
{
    public int maxHP = 3;
    public float stunTime = 0.35f;

    int hp;
    Animator anim;
    bool dead;

    void Awake() { hp = maxHP; anim = GetComponent<Animator>(); }

    public void TakeDamage(int amount, Vector2 knockback)
    {
        if (dead) return;
        hp -= amount;
        anim.SetTrigger("Hit");
        if (hp <= 0)
        {
            dead = true;
            anim.SetTrigger("Die");
            // Optional: disable collisions so it falls through or stops interacting
            foreach (var c in GetComponents<Collider2D>()) c.enabled = false;
            Destroy(gameObject, 1.2f);
        }
        else
        {
            StartCoroutine(StunRoutine());
        }
    }

    System.Collections.IEnumerator StunRoutine()
    {
        anim.SetBool("IsStunned", true);
        yield return new WaitForSeconds(stunTime);
        anim.SetBool("IsStunned", false);
    }
}
