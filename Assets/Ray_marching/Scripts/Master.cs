using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Master : MonoBehaviour {

    public ComputeShader rayMarching;

    [SerializeField, Range(0.1f, 5)]
    private float blendStrenth;

    private RenderTexture target;
    private List<GeometryData> geometryObjects = new List<GeometryData>();
    private List<GeometryData> extraGeometryObjects = new List<GeometryData>();

    private Camera cam;

    private Light dirLight;

    struct GeometryData {
        public Vector3 center;
        public Vector3 size;
        public Vector3 color;

        public int type;

        public int blendType;

        public static int GetSize() {
            return sizeof(float) * 9 + sizeof(int) * 2;
        }
    }

    private void Awake() {
        cam = Camera.current;
        dirLight = FindObjectOfType<Light>();
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination) {
        cam = Camera.current;

        InitRenderTexture(source);
        Scene();

        //rayMarching.SetTexture(0, "Source", source);
        rayMarching.SetTexture(0, "Destination", target);

        rayMarching.SetMatrix("cameraToWorld", cam.cameraToWorldMatrix);
        rayMarching.SetMatrix("inverseProjection", cam.projectionMatrix.inverse);

        rayMarching.SetVector("backgroundColor", new Vector4(0.5f, 0.5f, 0.5f, 1));

        rayMarching.SetVector("directionToLight", -dirLight.transform.forward);

        ComputeBuffer geometryBuffer = new ComputeBuffer(geometryObjects.Count, GeometryData.GetSize());
        geometryBuffer.SetData(geometryObjects);
        rayMarching.SetBuffer(0, "geometryObjects", geometryBuffer);
        rayMarching.SetInt("numbOfObjects", geometryObjects.Count);

        ComputeBuffer extraGeometryBuffer = new ComputeBuffer(extraGeometryObjects.Count, GeometryData.GetSize());
        extraGeometryBuffer.SetData(extraGeometryObjects);
        rayMarching.SetBuffer(0, "extraGeometryObjects", extraGeometryBuffer);
        rayMarching.SetInt("numbOfExtraObjects", extraGeometryObjects.Count);

        rayMarching.SetFloat("blendStrength", blendStrenth);

        // The compute shader CSMain expects 8x8x1 thread blocks so 
        // we divide the dimensions of the source by 8 to find how many blocks we need
        int threadGroupsX = Mathf.CeilToInt(source.width / 8.0f);
        int threadGroupsY = Mathf.CeilToInt(source.height / 8.0f);
        rayMarching.Dispatch(0, threadGroupsX, threadGroupsY, 1);

        geometryBuffer.Release();
        geometryObjects.Clear();

        extraGeometryBuffer.Release();
        extraGeometryObjects.Clear();

        Graphics.Blit(target, destination);
    }

    void InitRenderTexture(RenderTexture source) {
        if (target == null || target.width != source.width || target.height != source.height) {
            if (target != null) {
                target.Release();
            }

            target = new RenderTexture(source.width, source.height, 0);
            target.enableRandomWrite = true;
            target.filterMode = FilterMode.Point;
            target.Create();
        }
    }

    void Scene() {
        Geometry[] sceneObjects = FindObjectsOfType<Geometry>();

        foreach (Geometry obj in sceneObjects) {
            GeometryData g = new GeometryData() {
                center = obj.position,
                size = obj.scale,
                color = obj.color,
                type = ((int)obj.type),
                blendType = ((int)obj.blendType)
            };

            if (obj.name.Contains("Extra_")) {
                extraGeometryObjects.Add(g);
            } else {
                geometryObjects.Add(g);
            }
        }

        Debug.Log("Geometry count: " + geometryObjects.Count);
        Debug.Log("Extra Geometry count: " + extraGeometryObjects.Count);
    }
}
