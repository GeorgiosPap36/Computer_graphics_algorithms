using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using static Unity.VisualScripting.Member;

public class Master_MarchingSquares : MonoBehaviour {

    public ComputeShader marchingSquares;
    public ComputeShader noiseShader;

    private RenderTexture marchingCubesTexture;
    private RenderTexture noiseTexture;

    private int threadGroupsX, threadGroupsY, threadGroupsZ;

    [SerializeField]
    private int gridWidth, gridHeight, gridDepth;
    private int gW, gH, gD;

    [SerializeField, Range(0, 100)]
    private int time;

    [SerializeField]
    private bool advanceTime;

    private float timer = 0;

    private void Awake() {
        gH = 0;
        gW = 0;
        gD = 0;
    }

    private void Update() {
        if (advanceTime) {
            if (timer < 0.15f) {
                timer += Time.deltaTime;
            } else {
                timer = 0;
                if (time < gridDepth) {
                    time++;
                }
            }
        }
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination) {

        // The compute shader CSMain expects 8x8x1 thread blocks so 
        // we divide the dimensions of the source by 8 to find how many blocks we need
        threadGroupsX = Mathf.CeilToInt(source.width / 8.0f);
        threadGroupsY = Mathf.CeilToInt(source.height / 8.0f);
        threadGroupsZ = Mathf.CeilToInt(gridDepth / 8.0f);

        // Generate noise for the first frame and
        // then only when size changes
        if (gH != gridHeight || gW != gridWidth || gD != gridDepth) {
            InitRenderTexture3D(ref noiseTexture, gridWidth + 1, gridHeight + 1, gridDepth + 1);
            int noiseKernel = noiseShader.FindKernel("CSMain");
            noiseShader.SetTexture(noiseKernel, "Result", noiseTexture);
            noiseShader.Dispatch(noiseKernel, threadGroupsX, threadGroupsY, threadGroupsZ);

            gH = gridHeight;
            gW = gridWidth;
            gD = gridDepth;
        }

        // Run marching squares compute shader
        InitRenderTexture(ref marchingCubesTexture, source.width, source.height);
        int marchingSquaresKernel = marchingSquares.FindKernel("CSMain");

        marchingSquares.SetInt("gridWidth", gridWidth);
        marchingSquares.SetInt("gridHeight", gridHeight);
        marchingSquares.SetInt("time", time);
        
        marchingSquares.SetTexture(marchingSquaresKernel, "Result", marchingCubesTexture);
        marchingSquares.SetTexture(marchingSquaresKernel, "Noise", noiseTexture);
        marchingSquares.SetVector("backgroundColor", new Vector4(0.5f, 0.5f, 0.5f, 1));
        marchingSquares.Dispatch(marchingSquaresKernel, threadGroupsX, threadGroupsY, 1);

        Graphics.Blit(marchingCubesTexture, destination);
    }

    void InitRenderTexture(ref RenderTexture texture, int width, int height) {
        if (texture == null || texture.width != width || texture.height != height) {
            if (texture != null) {
                texture.Release();
            }

            texture = new RenderTexture(width, height, 0);
            texture.enableRandomWrite = true;
            texture.filterMode = FilterMode.Point;
            texture.Create();
        }
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
}
