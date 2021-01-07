using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class HiddenInterior : MonoBehaviour
{
    int overlappingUnits = 0;
    Collider revealCollider;
    List<Transform> children = new List<Transform>();

    void Start() {
        revealCollider = this.GetComponent<Collider>();
        // Get all children gameobjects
        for (int i = 0; i < this.transform.childCount; i++) {
            children.Add(this.transform.GetChild(i));
        }

        foreach (Transform child in children) {
            child.gameObject.SetActive(true);
        }
    }
    
    private void OnTriggerEnter (Collider other) {
        overlappingUnits++;
        foreach (Transform child in children) {
            child.gameObject.SetActive(false);
        }
    }

    private void OnTriggerExit (Collider other) {
        overlappingUnits--;
        if (overlappingUnits == 0) {
            foreach (Transform child in children) {
                child.gameObject.SetActive(true);
            }
        }
    }
}
