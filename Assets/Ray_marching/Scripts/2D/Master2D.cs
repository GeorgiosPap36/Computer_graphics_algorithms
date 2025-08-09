using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class Master2D : MonoBehaviour {

    public ComputeShader rayMarching2d;

    private RenderTexture target;
    private List<Circle> circles = new List<Circle>();
    private List<Square> squares = new List<Square>();

    private Transform sceneObjects;

    private Camera cam;

    struct Circle {
        public Vector2 center;
        public float radius;

        public static int GetSize() {
            return sizeof(float) * 3;
        }
    }

    struct Square {
        public Vector2 center;
        public Vector2 size;

        public static int GetSize() {
            return sizeof(float) * 4;
        }
    }

    private void Awake() {
        cam = Camera.current;
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination) {
        cam = Camera.current;

        InitRenderTexture(source);
        Scene();

        rayMarching2d.SetTexture(0, "Source", source);
        rayMarching2d.SetTexture(0, "Destination", target);

        rayMarching2d.SetVector("color", new Vector4(0, 0, 0, 1));

        ComputeBuffer circlesBuffer = new ComputeBuffer(circles.Count, Circle.GetSize());
        circlesBuffer.SetData(circles);
        rayMarching2d.SetBuffer(0, "circles", circlesBuffer);
        rayMarching2d.SetInt("numbOfCircles", circles.Count);

        ComputeBuffer squaresBuffer = new ComputeBuffer(squares.Count, Square.GetSize());
        squaresBuffer.SetData(squares);
        rayMarching2d.SetBuffer(0, "squares", squaresBuffer);
        rayMarching2d.SetInt("numbOfSquares", squares.Count);

        // The compute shader CSMain expects 8x8x1 thread blocks so 
        // we divide the dimensions of the source by 8 to find how many blocks we need
        int threadGroupsX = Mathf.CeilToInt(source.width / 8.0f);
        int threadGroupsY = Mathf.CeilToInt(source.height / 8.0f);
        rayMarching2d.Dispatch(0, threadGroupsX, threadGroupsY, 1);

        circlesBuffer.Release();
        squaresBuffer.Release();

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
        circles.Clear();
        squares.Clear();

        sceneObjects = GameObject.Find("SceneObjects").transform;

        Transform circlesObj = sceneObjects.Find("Circles");
        for (int i = 0; i < circlesObj.childCount; i++) {
            Transform circleObj = circlesObj.GetChild(i);

            Vector3 viewportPos = cam.WorldToViewportPoint(circleObj.position);
            viewportPos = viewportPos * 2 - Vector3.one; // to [-1,1]
            viewportPos.x *= cam.aspect;

            Circle c = new Circle() {
                center = new Vector2(viewportPos.x, viewportPos.y),
                radius = circleObj.localScale.x // this may need scaling as well
            };
            circles.Add(c);
        } 

        Transform squaresObj = sceneObjects.Find("Squares");
        for (int i = 0; i < squaresObj.childCount; i++) {
            Transform squareObj = squaresObj.GetChild(i);
            
            Vector3 viewportPos = cam.WorldToViewportPoint(squareObj.position);
            viewportPos = viewportPos * 2 - Vector3.one; // to [-1,1]
            viewportPos.x *= cam.aspect; // match HLSL aspect correction

            Square sq = new Square() {
                center = new Vector2(viewportPos.x, viewportPos.y),
                size = new Vector2(squareObj.localScale.x, squareObj.localScale.y)
            };
            squares.Add(sq);
        }
    }
}
