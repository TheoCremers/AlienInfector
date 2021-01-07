using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WebScript : HealthSystem
{
    public bool webActive = true;
    Renderer webRenderer;
    float webDestroyTime = 1f;

    protected override void Start () {
        base.Start();
        OnDeath += DestroyThis;
        webRenderer = GetComponent<Renderer>();
    }
    
    public override void TakeDamage(float damageAmount) {
        base.TakeDamage(damageAmount);
        webRenderer.material.color = new Color(0f, 0f, currentHealth/startingHealth);
    }

    private void DestroyThis() {
        webActive = false;
        Destroy(gameObject, webDestroyTime);
    }
}
