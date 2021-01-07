using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SceneManagerScript : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        // Only works on standalone application
        Cursor.lockState = CursorLockMode.Confined;
    }

    // Track number of escaped victims

    void Update () {
        // Count down started when first victim escapes
        // Speeds up when more escape
    }
}
