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

    public static RenderTexture positionTexture;
    public RenderTexture positionTextureDebug;

    public static Vector2Int injectionResolution;

    public static RenderBuffer[] _rb;

    // Sample counter
    public static RenderTexture CountRenderTextureCache;
    public static RenderTexture CountRenderTexture;
    public static Texture2D CountTexture2D;
    public static Color32 sampleCountColour;
    public static Rect rectReadPicture;
    ///END Sample counter

    static Queue<AsyncGPUReadbackRequest> _requests = new Queue<AsyncGPUReadbackRequest>();

    public static System.Diagnostics.Stopwatch encodeStopwatch;
    public static double stopwatchEncode;

    public static bool expensiveGPUCounters = false;


    private void OnEnable()
    {
        DoEnable();
    }

    private void DoEnable()
    {
        emissiveShader = Shader.Find("Hidden/Nigiri_Injection");

        emissiveShaderDebug = emissiveShader;

        cam = GetComponent<Camera>();

        lightingTexture = new RenderTexture(1024, 1024, 0, RenderTextureFormat.ARGBFloat);
        lightingTexture.Create();

        lightingDepthTexture = new RenderTexture(1024, 1024, 16, RenderTextureFormat.Depth);
        lightingDepthTexture.Create();

        positionTexture = new RenderTexture(1024, 1024, 0, RenderTextureFormat.ARGBFloat);
        positionTexture.Create();

        positionTextureDebug = positionTexture;
        lightingTextureDebug = lightingTexture;
        

        _rb = new RenderBuffer[2];
        _rb[0] = lightingTexture.colorBuffer;
        _rb[1] = positionTexture.colorBuffer;

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

        encodeStopwatch = new System.Diagnostics.Stopwatch();
    }

    private void OnDisable()
    {
        if (positionTexture != null) positionTexture.Release();
        if (lightingTexture != null) lightingTexture.Release();
        if (lightingDepthTexture != null) lightingDepthTexture.Release();

        if (positionTextureDebug != null) positionTextureDebug.Release();
        if (lightingTextureDebug != null) lightingTextureDebug.Release();

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

            encodeStopwatch.Start();
            cam.RenderWithShader(emissiveShader, "");
            encodeStopwatch.Stop();
            stopwatchEncode = encodeStopwatch.Elapsed.TotalMilliseconds;
            encodeStopwatch.Reset();

            Graphics.ClearRandomWriteTargets();

            // Sample counter
            if (expensiveGPUCounters)
            {
                // Handle completed async read-back
                while (_requests.Count > 0)
                {
                    var req = _requests.Peek();

                    if (req.hasError)
                    {
                        Debug.Log("GPU readback error detected.");
                        _requests.Dequeue();
                    }
                    else if (req.done)
                    {
                        var buffer = req.GetData<Color32>();

                        CountTexture2D.SetPixels32(buffer.ToArray());
                        CountTexture2D.Apply();

                        sampleCountColour = CountTexture2D.GetPixel(0, 0);

                        RenderTexture.active = CountRenderTexture;
                        GL.Clear(true, true, Color.clear);

                        _requests.Dequeue();
                    }
                    else
                    {
                        break;
                    }
                }
                ///END Handle completed async read-back

                RenderTexture.active = CountRenderTexture;
                if (_requests.Count == 0) _requests.Enqueue(AsyncGPUReadback.Request(CountRenderTexture));
            }
            ///END Sample counter
        }
    }
}
