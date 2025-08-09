using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Geometry : MonoBehaviour {

    public enum Type {
        Sphere,
        Cube
    }
    public Type type;

    public enum BlendType {
        Normal,
        Blend
    }
    public BlendType blendType;


    public Vector3 color;


    public Vector3 position {
        get {
            return transform.position;
        }
    }
    
    public Vector3 scale {
        get {
            return transform.localScale;
        }
    }

}
