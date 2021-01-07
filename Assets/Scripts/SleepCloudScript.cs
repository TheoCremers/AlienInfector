using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SleepCloudScript : MonoBehaviour
{
    public int victimLayerID;
    public float radius;
    public float sleepDuration;
    public LayerMask victimMask;
    public LayerMask obstacleMask;

    // Start is called before the first frame update
    void Start()
    {
        Collider[] victimsInRadius = Physics.OverlapSphere(transform.position, radius, victimMask);

        foreach (Collider victim in victimsInRadius) {
            Vector3 direction = victim.transform.position - transform.position;
            float distance = direction.magnitude;
            if (!Physics.Raycast(transform.position, direction, distance, obstacleMask)) {
                victim.GetComponent<VictimController>().GetSleeped(sleepDuration);
            }
        }

        Destroy(gameObject, 3f);
    }

    private void OnTriggerEnter (Collider collider) {
        if (collider.gameObject.layer == victimLayerID) {
            Vector3 direction = collider.transform.position - transform.position;
            float distance = direction.magnitude;
            if (!Physics.Raycast(transform.position, direction, distance, obstacleMask)) {
                collider.GetComponent<VictimController>().GetSleeped(sleepDuration);
            }
        }
    }
}
