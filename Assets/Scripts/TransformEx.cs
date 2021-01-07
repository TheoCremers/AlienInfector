using UnityEngine;

public static class TransformEx {

    public static Transform FindTransform (this Transform parent, string name) {
        if (parent.name.Equals(name)) return parent;
        foreach (Transform child in parent) {
            Transform result = child.FindTransform(name);
            if (result != null) return result;
        }
        return null;
    }
}
