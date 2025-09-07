using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using static Unity.VisualScripting.Member;

public class Master_MarchingCubes : MonoBehaviour {

    [Header("Compute Shaders")]

    [SerializeField]
    private ComputeShader marchingCubes;
    [SerializeField]
    private ComputeShader noiseShader;
    [SerializeField]
    private ComputeShader gridDebugShader;
    [SerializeField]
    private ComputeShader noiseDebugShader;

    [Header("Mesh Objects")]

    [SerializeField]
    private MeshFilter meshObject;

    private List<GameObject> spheres = new List<GameObject>();

    private RenderTexture noiseTexture;

    [Header("Render sizes")]

    [SerializeField]
    private Vector3 gridSize;
    private Vector3 currentGridSize;

    [SerializeField]
    private Vector3 areaSize;
    private Vector3 crntAreaSize;

    [SerializeField]
    private float isoLevel;
    private float crntIsoLevel;

    [Header("Debug")]

    [SerializeField]
    private bool useSetNoise;
    private bool crntUseSetNoise;

    [SerializeField]
    private bool drawDebugSpheres;
    private bool crntDrawDebugSpheres;

    [SerializeField]
    private string gridValues;

    [SerializeField]
    private GameObject sphere;

    [SerializeField]
    private Material whiteMat, blackMat;

    struct Triangle {
        public Vector3 point1;
        public Vector3 point2;
        public Vector3 point3;

        public Vector3 normal1;
        public Vector3 normal2;
        public Vector3 normal3;

        public static int GetSize() {
            return 18 * sizeof(float);
        }
    }

    struct GridPoint {
        public Vector3 point;
        public float value;

        public static int GetSize() {
            return 4 * sizeof(float);
        }
    }

    private void Awake() {
        currentGridSize = Vector3.zero;
    }

    private void Update() {
        // Generate noise and mesh for the first frame and then only when the grid changes
        if (gridSize != currentGridSize ||
            areaSize != crntAreaSize ||
            drawDebugSpheres != crntDrawDebugSpheres ||
            useSetNoise != crntUseSetNoise ||
            isoLevel != crntIsoLevel) {

            GenerateMesh();

            currentGridSize = gridSize;
            crntAreaSize = areaSize;
            crntUseSetNoise = useSetNoise;
            crntDrawDebugSpheres = drawDebugSpheres;
            crntIsoLevel = isoLevel;
        }
    }

    private void GenerateMesh() {
        DispatchNoiseShader();

        spheres.ForEach(s => Destroy(s));
        spheres.Clear();

        if (drawDebugSpheres) {
            DispatchGridDebugShader();
        }

        DispatchMarchingCubeShader();
    }

    private void DispatchNoiseShader() {
        // The compute shader CSMain expects 8x8x8 thread blocks so 
        // we divide the dimensions of the source by 8 to find how many blocks we need
        int threadGroupsX = Mathf.CeilToInt(gridSize.x / 8.0f);
        int threadGroupsY = Mathf.CeilToInt(gridSize.y / 8.0f);
        int threadGroupsZ = Mathf.CeilToInt(gridSize.z / 8.0f);

        InitRenderTexture3D(ref noiseTexture, (int) gridSize.x, (int) gridSize.y, (int) gridSize.z);
        
        if (!useSetNoise) {
            int noiseKernel = noiseShader.FindKernel("CSMain");
            noiseShader.SetTexture(noiseKernel, "Result", noiseTexture);
            noiseShader.Dispatch(noiseKernel, threadGroupsX, threadGroupsY, threadGroupsZ);
        } else {
            int noiseKernel = noiseDebugShader.FindKernel("CSMain");

            noiseDebugShader.SetInt("width", (int) gridSize.x);
            noiseDebugShader.SetInt("height", (int) gridSize.y);

            float[] noiseValues = StringArrayToFloat();
            ComputeBuffer noiseValuesBuffer = new ComputeBuffer(noiseValues.Length, sizeof(float));
            noiseValuesBuffer.SetData(noiseValues);
            noiseDebugShader.SetBuffer(noiseKernel, "noiseValues", noiseValuesBuffer);

            noiseDebugShader.SetTexture(noiseKernel, "Result", noiseTexture);
            noiseDebugShader.Dispatch(noiseKernel, threadGroupsX, threadGroupsY, threadGroupsZ);

            noiseValuesBuffer.Release();
        }
    }

    private void DispatchGridDebugShader() {
        int threadGroupsX = Mathf.CeilToInt(gridSize.x / 8.0f);
        int threadGroupsY = Mathf.CeilToInt(gridSize.y / 8.0f);
        int threadGroupsZ = Mathf.CeilToInt(gridSize.z / 8.0f);

        int marchingSquaresKernel = marchingCubes.FindKernel("CSMain");

        gridDebugShader.SetVector("gridSize", gridSize);
        gridDebugShader.SetVector("areaSize", areaSize);

        int gridPointsNumber = (int) (gridSize.x * gridSize.y * gridSize.z);
        ComputeBuffer gridPointsBuffer = new ComputeBuffer(gridPointsNumber, GridPoint.GetSize(), ComputeBufferType.Append);
        gridDebugShader.SetBuffer(marchingSquaresKernel, "gridPoints", gridPointsBuffer);
        gridPointsBuffer.SetCounterValue(0);

        gridDebugShader.SetTexture(marchingSquaresKernel, "Noise", noiseTexture);
        gridDebugShader.Dispatch(marchingSquaresKernel, threadGroupsX, threadGroupsY, threadGroupsZ);

        GridPoint[] gridPoints = new GridPoint[gridPointsNumber];
        gridPointsBuffer.GetData(gridPoints);
        ShowGridSpheres(gridPoints);

        gridPointsBuffer.Release();
    }

    private void DispatchMarchingCubeShader() {

        Vector3 numOfCells = gridSize - Vector3.one;

        int threadGroupsX = Mathf.CeilToInt(numOfCells.x);
        int threadGroupsY = Mathf.CeilToInt(numOfCells.y);
        int threadGroupsZ = Mathf.CeilToInt(numOfCells.z);

        int marchingSquaresKernel = marchingCubes.FindKernel("CSMain");

        marchingCubes.SetVector("gridSize", gridSize);
        marchingCubes.SetVector("areaSize", areaSize);
        marchingCubes.SetFloat("isoLevel", isoLevel);

        int numberOfTriangles = (int) (numOfCells.x * numOfCells.y * numOfCells.z) * 5;

        // Worst case scenario is for the surface to have 5 triangles
        ComputeBuffer trianglesBuffer = new ComputeBuffer(numberOfTriangles, Triangle.GetSize(), ComputeBufferType.Append);
        marchingCubes.SetBuffer(marchingSquaresKernel, "triangles", trianglesBuffer);
        trianglesBuffer.SetCounterValue(0);

        ComputeBuffer trianglesCounterBuffer = new ComputeBuffer(1, sizeof(uint), ComputeBufferType.Raw);

        marchingCubes.SetTexture(marchingSquaresKernel, "Noise", noiseTexture);
        marchingCubes.Dispatch(marchingSquaresKernel, threadGroupsX, threadGroupsY, threadGroupsZ);

        ComputeBuffer.CopyCount(trianglesBuffer, trianglesCounterBuffer, 0);

        uint[] trianglesCounter = new uint[1] { 0 };
        trianglesCounterBuffer.GetData(trianglesCounter);
        uint numOfTriangles = trianglesCounter[0];

        Debug.Log("Number of triangles:" + numOfTriangles);

        Triangle[] meshTriangles = new Triangle[numOfTriangles];
        trianglesBuffer.GetData(meshTriangles);
        AssignMeshToMeshObject(meshTriangles);

        trianglesBuffer.Release();
        trianglesCounterBuffer.Release();
    }

    private void AssignMeshToMeshObject(Triangle[] triangles) {
        Mesh mesh = new Mesh();
        mesh.name = "Marching Cubes Mesh";

        Vector3[] triangleVertices = new Vector3[triangles.Length * 3];
        Vector3[] triangleNormals = new Vector3[triangles.Length * 3];
        int[] triangleIds = new int[triangles.Length * 3];

        Dictionary<Vector3, Vector3> averagedVerticeNormals = new Dictionary<Vector3, Vector3>();

        int index = 0;
        for (int i = 0; i < triangles.Length; i++) {
            index = 3 * i;

            triangleVertices[index] = triangles[i].point1;
            triangleVertices[index + 1] = triangles[i].point2;
            triangleVertices[index + 2] = triangles[i].point3;

            triangleNormals[index] = triangles[i].normal1;
            triangleNormals[index + 1] = triangles[i].normal1;
            triangleNormals[index + 2] = triangles[i].normal1;

            triangleIds[index] = index;
            triangleIds[index + 1] = index + 1;
            triangleIds[index + 2] = index + 2;

            AddOrSum(averagedVerticeNormals, triangleVertices[index], triangleNormals[index]);
            AddOrSum(averagedVerticeNormals, triangleVertices[index + 1], triangleNormals[index + 1]);
            AddOrSum(averagedVerticeNormals, triangleVertices[index + 2], triangleNormals[index + 2]);
        }

        for (int i = 0; i < triangles.Length; i++) {
            index = 3 * i;

            Vector3 tempNormal;

            averagedVerticeNormals.TryGetValue(triangleVertices[index], out tempNormal);
            triangleNormals[index] = tempNormal.normalized;

            averagedVerticeNormals.TryGetValue(triangleVertices[index + 1], out tempNormal);
            triangleNormals[index + 1] = tempNormal.normalized;

            averagedVerticeNormals.TryGetValue(triangleVertices[index + 2], out tempNormal);
            triangleNormals[index + 2] = tempNormal.normalized;
        }

        mesh.vertices = triangleVertices;
        mesh.triangles = triangleIds;
        mesh.normals = triangleNormals;

        meshObject.mesh = mesh;
    }

    private void ShowGridSpheres(GridPoint[] gridPoints) {
        for (int i = 0; i < gridPoints.Length; i++) {
            GameObject s = Instantiate(sphere, gridPoints[i].point, Quaternion.identity);
            s.name = "Sphere " + i + " - " + gridPoints[i].value;

            if (gridPoints[i].value > isoLevel) {
                s.GetComponent<MeshRenderer>().material = whiteMat;
            } else {
                s.GetComponent<MeshRenderer>().material = blackMat;
            }

            spheres.Add(s);
        }
    }

    private float[] StringArrayToFloat() {
        int gridPointsNumber = Mathf.RoundToInt((gridSize.x + 1) * (gridSize.y + 1) * (gridSize.z + 1));
        float[] vals = new float[gridPointsNumber];

        for (int i = 0; i < vals.Length; i++) {
            vals[i] = (float) ((gridValues.Length > i) ? char.GetNumericValue(gridValues[i]) : 1);
            //Debug.Log(vals[i]);
        }

        return vals;

    }

    void InitRenderTexture3D(ref RenderTexture texture, int width, int height, int depth) {
        if (texture == null || texture.width != width || texture.height != height || texture.volumeDepth != depth) {
            if (texture != null) {
                texture.Release();
            }

            texture = new RenderTexture(width, height, 0); // depthBuffer = 0
            texture.dimension = TextureDimension.Tex3D;
            texture.volumeDepth = depth;                   // third dimension
            texture.enableRandomWrite = true;
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Repeat;     // important for noise
            texture.graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R32_SFloat;
            texture.Create();
        }
    }

    void AddOrSum(Dictionary<Vector3, Vector3> dict, Vector3 key, Vector3 valueToAdd) {
        if (dict.ContainsKey(key))
            dict[key] += valueToAdd;
        else
            dict[key] = valueToAdd;
    }
}
