﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
    }

    private void OnDisable()
    {
        if (positionTexture != null) positionTexture.Release();
        if (lightingTexture != null) lightingTexture.Release();
        if (lightingDepthTexture != null) lightingDepthTexture.Release();

        if (positionTextureDebug != null) positionTextureDebug.Release();
        if (lightingTextureDebug != null) lightingTextureDebug.Release();
        if (lightingDepthTextureDebug != null) lightingDepthTextureDebug.Release();

        //if (lightMapBuffer != null) lightMapBuffer.Release();
        //if (positionBuffer != null) positionBuffer.Release();
    }

    public static void DoRender()
    {
        if (lightingTexture != null)
        {
            Graphics.SetRandomWriteTarget(5, Nigiri.voxelUpdateBuffer);
            Graphics.SetRandomWriteTarget(6, Nigiri.voxelGrid1);
            cam.SetTargetBuffers(_rb, lightingDepthTexture.depthBuffer);
            cam.RenderWithShader(emissiveShader, "");
            Graphics.ClearRandomWriteTargets();
        }
    }

    private void OnpostRender()
    {
        //lightingTextureDebug = lightingTexture;
    }
}
