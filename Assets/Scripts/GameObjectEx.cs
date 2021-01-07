using UnityEngine;

public static class GameObjectEx {
    public static void DrawCircle (this GameObject container, float radius, float lineWidth) {
        var segments = 180;
        var line = container.GetComponent<LineRenderer>();
        line.enabled = true;
        line.useWorldSpace = false;
        line.startWidth = lineWidth;
        line.endWidth = lineWidth;
        line.positionCount = segments + 1;

        var pointCount = segments + 1; // add extra point to make startpoint and endpoint the same to close the circle
        var points = new Vector3[pointCount];

        for (int i = 0; i < pointCount; i++) {
            var rad = Mathf.Deg2Rad * (i * 360f / segments);
            points[i] = new Vector3(Mathf.Sin(rad) * radius, 0, Mathf.Cos(rad) * radius);
        }

        line.SetPositions(points);
    }

    public static void DrawLine (this GameObject container, float length, float lineWidth) {
        var line = container.GetComponent<LineRenderer>();
        line.enabled = true;
        line.useWorldSpace = false;
        line.startWidth = lineWidth;
        line.endWidth = lineWidth;
        line.positionCount = 2;
        
        line.SetPosition(0, new Vector3(length / 2f, 0, 0));
        line.SetPosition(1, new Vector3(-length / 2f, 0, 0));
    }

    public static bool UpdateLine (this GameObject container, Vector3 casterPosition, float maxWidth, LayerMask wallMask) {
        var line = container.GetComponent<LineRenderer>();
        Vector3 targetPosition = container.transform.position;
        float halfWidth = maxWidth * 0.5f;
        // First rotate towards player
        Vector3 direction = (casterPosition - targetPosition).normalized;
        container.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);

        // Then fire raycasts sideways with half the maxwidth
        Vector3 sideDirection = new Vector3(direction.z, 0f, -direction.x);
        RaycastHit hit;
        bool hitLeft = Physics.Raycast(targetPosition, sideDirection, out hit, halfWidth, wallMask);
        // left
        if (hitLeft) {
            line.SetPosition(0, new Vector3(hit.distance, 0, 0)); ;
        }
        else {
            line.SetPosition(0, new Vector3(halfWidth, 0, 0));
        }
        // right
        bool hitRight = Physics.Raycast(targetPosition, -sideDirection, out hit, halfWidth, wallMask);
        if (hitRight) {
            line.SetPosition(1, new Vector3(-hit.distance, 0, 0)); ;
        }
        else {
            line.SetPosition(1, new Vector3(-halfWidth, 0, 0));
        }
        
        // Return true if valid
        if (hitLeft && hitRight) {
            return true;
        }
        else {
            return false;
        }
    }
}