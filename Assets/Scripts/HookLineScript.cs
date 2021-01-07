using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HookLineScript : MonoBehaviour
{
    public Transform player;
    public Transform target;
    public bool splatter;
    public GameObject splatterPrefab;
    //public GameObject trailPrefab;
    //GameObject trailObject;
    GameObject splatterObject;
    private LineRenderer line;

    // Start is called before the first frame update
    void Start()
    {
        line = GetComponent<LineRenderer>();

        if (splatter) {
            Vector3 direction = target.position - transform.position;
            Quaternion rotation = Quaternion.FromToRotation(Vector3.forward, direction);
            splatterObject = Instantiate(splatterPrefab, target.position, rotation);
            Destroy(splatterObject, 1.2f);
            //trailObject = Instantiate(trailPrefab, target);
            //Destroy(trailObject, 0.5f);
        }

        target = target.FindTransform("head");
    }

    // Update is called once per frame
    void Update()
    {
        //line.SetPosition(0, player.position);
        line.SetPosition(1, transform.InverseTransformPoint(target.position));

        /*if (splatter) {
            bloodSplatterObject.transform.position = line.GetPosition(1);
        }*/
    }

}
