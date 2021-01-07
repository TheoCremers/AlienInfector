using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HealthSystem : MonoBehaviour
{
    public float startingHealth;
    protected float currentHealth;
    public bool alive = true;

    public event System.Action OnDeath;

    protected virtual void Start () {
        currentHealth = startingHealth;
    }

    public virtual void TakeDamage(float damageAmount) {
        currentHealth -= damageAmount;
        if (currentHealth <= 0 && alive) {
            Die();
        }
    }

    public void Die() {
        alive = false;
        if (OnDeath != null) {
            OnDeath();
        }
    }
}
