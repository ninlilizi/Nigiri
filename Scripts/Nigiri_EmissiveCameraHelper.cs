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

    public static RenderTexture positionTexture;
    public static RenderTexture lightingTexture;
    
    public int emissiveCamAngleCound;

    public static ComputeBuffer lightMapBuffer;

    void OnEnable ()
    {
        emissiveShader = Shader.Find("Hidden/Nigiri_Injection");

        cam = GetComponent<Camera>();
        if (pvgiShader == null) pvgiShader = Shader.Find("Hidden/PVGIShader");
        if (pvgiMaterial == null) pvgiMaterial = new Material(pvgiShader);
 
        lightingTexture = new RenderTexture(1024, 1024, 32, RenderTextureFormat.ARGBFloat);
        lightingTexture.Create();

        lightMapBuffer = new ComputeBuffer(256 * 256 * 256, sizeof(float) * 4, ComputeBufferType.Default);

        cam.depthTextureMode = DepthTextureMode.Depth;
        cam.clearFlags = CameraClearFlags.Color;
        cam.backgroundColor = Color.black;
    }

    private void OnDisable()
    {
        if (Nigiri_EmissiveCameraHelper.lightingTexture != null) Nigiri_EmissiveCameraHelper.lightingTexture.Release();
        if (positionTexture != null) positionTexture.Release();
        lightMapBuffer.Release();
    }

    private void Update()
    {
        Shader.SetGlobalTexture("positionTexture", Nigiri_EmissiveCameraHelper.positionTexture);
        Graphics.SetRandomWriteTarget(5, Nigiri_EmissiveCameraHelper.lightMapBuffer, true);
        cam.targetTexture = lightingTexture;
        cam.RenderWithShader(emissiveShader, "");
    }

    void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        Graphics.Blit(source, Nigiri_EmissiveCameraHelper.lightingTexture);
        Graphics.Blit(source, destination);
    }
}
