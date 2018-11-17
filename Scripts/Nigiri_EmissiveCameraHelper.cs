using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
[ImageEffectAllowedInSceneView]
public class Nigiri_EmissiveCameraHelper : MonoBehaviour {
   
    public static Camera cam;

    public Shader pvgiShader;
    public Shader emissiveShader;
    private Material pvgiMaterial;

    //public RenderTexture positionTexture;
    public static RenderTexture lightingTexture;
    
    public static ComputeBuffer lightMapBuffer;
    //public static ComputeBuffer sampleCountBuffer;

    public ComputeShader clearComputeCache;

    public static Vector2Int injectionResolution;

    private void OnEnable()
    {
        StartCoroutine(DoEnable());
    }

    IEnumerator<int> DoEnable ()
    {
        while (injectionResolution.x == 0)
        {
            yield return 0;
        }

        emissiveShader = Shader.Find("Hidden/Nigiri_Injection");

        clearComputeCache = Resources.Load("SEGIClear_Cache") as ComputeShader;

        cam = GetComponent<Camera>();
        if (pvgiShader == null) pvgiShader = Shader.Find("Hidden/PVGIShader");
        if (pvgiMaterial == null) pvgiMaterial = new Material(pvgiShader);

        lightingTexture = new RenderTexture(1024, 1024, 0, RenderTextureFormat.ARGBFloat);
        lightingTexture.Create();

        lightMapBuffer = new ComputeBuffer(256 * 256 * 256, sizeof(uint), ComputeBufferType.Default);

        cam.depthTextureMode = DepthTextureMode.None;
        cam.clearFlags = CameraClearFlags.Color;
        cam.useOcclusionCulling = false;
        cam.backgroundColor = Color.black;
        cam.renderingPath = RenderingPath.Forward;
        cam.orthographic = true;
        cam.allowHDR = false;
        cam.allowMSAA = true;
        cam.depth = -2;
    }

    private void OnDisable()
    {
        if (lightingTexture != null) lightingTexture.Release();
        if (lightMapBuffer != null) lightMapBuffer.Release();
    }

    private void Update()
    {
        if (lightingTexture != null)
        {
            Shader.SetGlobalTexture("positionTexture", Nigiri.positionTexture);
            Graphics.SetRandomWriteTarget(5, lightMapBuffer);
            cam.targetTexture = lightingTexture;
            cam.RenderWithShader(emissiveShader, "");
        }
    }

    void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        //Graphics.Blit(source, positionTexture, pvgiMaterial, 0);
        Graphics.Blit(source, lightingTexture);
    }
}
