using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteInEditMode]
[ImageEffectAllowedInSceneView]
public class Nigiri_EmissiveCameraHelper : MonoBehaviour {
   
    public static Camera cam;

    public static Shader emissiveShader;
    public Shader emissiveShaderDebug;

    public static RenderTexture lightingTexture;
    public static RenderTexture lightingDepthTexture;
    public RenderTexture lightingTextureDebug;
    public RenderTexture lightingDepthTextureDebug;

    public static RenderTexture positionTexture;
    public RenderTexture positionTextureDebug;

    //public static ComputeBuffer lightMapBuffer;
    //public static ComputeBuffer positionBuffer;

    //public ComputeShader clearComputeCache;

    public static Vector2Int injectionResolution;

    public static RenderBuffer[] _rb;

    // Sample counter
    public static RenderTexture CountRenderTextureCache;
    public static RenderTexture CountRenderTexture;
    public static Texture2D CountTexture2D;
    public static Color32 sampleCountColour;
    public static Rect rectReadPicture;
    ///END Sample counter

    private void OnEnable()
    {
        //StartCoroutine(DoEnable());
        DoEnable();
    }

    private void DoEnable()
    {
        /*while (injectionResolution.x == 0)
        {
            yield return 0;
        }*/

        emissiveShader = Shader.Find("Hidden/Nigiri_Injection");

        emissiveShaderDebug = emissiveShader;

        //clearComputeCache = Resources.Load("SEGIClear_Cache") as ComputeShader;

        cam = GetComponent<Camera>();

        lightingTexture = new RenderTexture(1024, 1024, 0, RenderTextureFormat.ARGBFloat);
        lightingTexture.Create();

        lightingDepthTexture = new RenderTexture(1024, 1024, 16, RenderTextureFormat.Depth);
        lightingDepthTexture.Create();

        positionTexture = new RenderTexture(1024, 1024, 0, RenderTextureFormat.ARGBFloat);
        positionTexture.Create();

        positionTextureDebug = positionTexture;
        lightingTextureDebug = lightingTexture;
        lightingDepthTextureDebug = lightingDepthTexture;
        

        _rb = new RenderBuffer[2];
        _rb[0] = lightingTexture.colorBuffer;
        _rb[1] = positionTexture.colorBuffer;

        //lightMapBuffer = new ComputeBuffer(256 * 256 * 256, sizeof(uint), ComputeBufferType.Default);
        //positionBuffer = new ComputeBuffer(1024 * 1024, sizeof(float) * 4, ComputeBufferType.Default);

        cam.depthTextureMode = DepthTextureMode.Depth;
        cam.clearFlags = CameraClearFlags.Color;
        cam.useOcclusionCulling = false;
        cam.backgroundColor = Color.black;
        cam.renderingPath = RenderingPath.Forward;
        cam.orthographic = true;
        cam.allowHDR = true;
        cam.allowMSAA = false;
        cam.depth = -2;

        // Sample counter
        if (CountRenderTexture != null) DestroyImmediate(CountRenderTexture);
        CountRenderTexture = new RenderTexture(1, 1, 32, RenderTextureFormat.ARGB32);
        CountRenderTexture.enableRandomWrite = true;
        if (CountTexture2D != null) DestroyImmediate(CountTexture2D);
        CountTexture2D = new Texture2D(1, 1, TextureFormat.ARGB32, false);
        rectReadPicture = new Rect(0, 0, 1, 1);
        ///END Sample counter
    }

    private void OnDisable()
    {
        if (positionTexture != null) positionTexture.Release();
        if (lightingTexture != null) lightingTexture.Release();
        if (lightingDepthTexture != null) lightingDepthTexture.Release();

        if (positionTextureDebug != null) positionTextureDebug.Release();
        if (lightingTextureDebug != null) lightingTextureDebug.Release();
        if (lightingDepthTextureDebug != null) lightingDepthTextureDebug.Release();

        if (CountRenderTexture != null) DestroyImmediate(CountRenderTexture);
        if (CountTexture2D != null) DestroyImmediate(CountTexture2D);
    }

    public static void DoRender()
    {
        if (lightingTexture != null)
        {
            Graphics.SetRandomWriteTarget(5, CountRenderTexture);
            Graphics.SetRandomWriteTarget(6, Nigiri.voxelGrid1);
            cam.SetTargetBuffers(_rb, lightingDepthTexture.depthBuffer);
            cam.RenderWithShader(emissiveShader, "");
            Graphics.ClearRandomWriteTargets();

            // Sample counter
            CountRenderTextureCache = RenderTexture.active;
            RenderTexture.active = CountRenderTexture;
            CountTexture2D.ReadPixels(rectReadPicture, 0, 0, false);
            CountTexture2D.Apply();
            GL.Clear(true, true, Color.clear);
            RenderTexture.active = CountRenderTextureCache;
            sampleCountColour = CountTexture2D.GetPixel(0, 0);
            ///END Sample counter
        }
    }

    /*
    private void Update()
    {
        
        //sampleCountDebug = (uint)((sampleCountColour.a << 24) | (sampleCountColour.b << 16) | (sampleCountColour.g << 8) | (sampleCountColour.r << 0));
    }
    */

}
