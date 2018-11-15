using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
[ImageEffectAllowedInSceneView]
public class Nigiri_EmissiveCameraHelper : MonoBehaviour {
   
    public static Camera cam;

    public Shader pvgiShader;
    public Shader lightMapShader;
    private Material pvgiMaterial;
    private Material lightMapMaterial;

    public static RenderTexture positionTexture;
    public static RenderTexture lightMapTexture;
    public static RenderTexture lightingTexture;

    public RenderTexture positionTextureDebug;
    public RenderTexture lightMapTextureDebug;
    public RenderTexture lightingTextureDebug;

    public int emissiveCamAngleCound;

    //public RenderTexture dummyCubeMap;

    // Use this for initialization
    void OnEnable ()
    {
        cam = GetComponent<Camera>();
        if (pvgiShader == null) pvgiShader = Shader.Find("Hidden/PVGIShader");
        if (pvgiMaterial == null) pvgiMaterial = new Material(pvgiShader);
        if (lightMapShader == null) lightMapShader = Shader.Find("Hidden/NKGI-Blit-lightMap");
        if (lightMapMaterial == null) lightMapMaterial = new Material(lightMapShader);

        positionTexture = new RenderTexture(1280, 720, 32, RenderTextureFormat.ARGBFloat);
        lightMapTexture = new RenderTexture(1280, 720, 32, RenderTextureFormat.ARGBFloat);
        lightingTexture = new RenderTexture(1280, 720, 32, RenderTextureFormat.ARGBFloat);

        positionTextureDebug = new RenderTexture(1280, 720, 32, RenderTextureFormat.ARGBFloat);
        lightMapTextureDebug = new RenderTexture(1280, 720, 32, RenderTextureFormat.ARGBFloat);
        lightingTextureDebug = new RenderTexture(1280, 720, 32, RenderTextureFormat.ARGBFloat);

        positionTexture.Create();
        lightMapTexture.Create();
        lightingTexture.Create();


        /*dummyCubeMap = new RenderTexture(512, 512, 32, RenderTextureFormat.ARGBFloat);
        dummyCubeMap.dimension = UnityEngine.Rendering.TextureDimension.Cube;
        dummyCubeMap.Create();*/

        cam.depthTextureMode = DepthTextureMode.Depth;

    }

    private void OnDisable()
    {
        if (Nigiri_EmissiveCameraHelper.positionTexture != null) Nigiri_EmissiveCameraHelper.positionTexture.Release();
        if (Nigiri_EmissiveCameraHelper.lightMapTexture != null) Nigiri_EmissiveCameraHelper.lightMapTexture.Release();
        if (Nigiri_EmissiveCameraHelper.lightingTexture != null) Nigiri_EmissiveCameraHelper.lightingTexture.Release();

        if (positionTextureDebug != null) positionTextureDebug.Release();
        if (lightMapTextureDebug != null) lightMapTextureDebug.Release();
        if (lightingTextureDebug != null) lightingTextureDebug.Release();

        //if (dummyCubeMap != null) dummyCubeMap.Release();
    }

    private void Update()
    {
        // We sample the secondary emissive camera from multipe angles
        /*if (emissiveCamAngleCound == 0) cam.transform.localEulerAngles = new Vector3(0, 180, 0);
        if (emissiveCamAngleCound == 1) cam.transform.localEulerAngles = new Vector3(-45, 180, 0);
        if (emissiveCamAngleCound == 2) cam.transform.localEulerAngles = new Vector3(45, 180, 0);
        if (emissiveCamAngleCound == 3) cam.transform.localEulerAngles = new Vector3(0, 0, 0);
        if (emissiveCamAngleCound == 4) cam.transform.localEulerAngles = new Vector3(-45, 0, 0);
        if (emissiveCamAngleCound == 5) cam.transform.localEulerAngles = new Vector3(45, 0, 0);
        emissiveCamAngleCound = (emissiveCamAngleCound + 1) % (5);*/

        //cam.targetTexture = lightMapTexture;
        cam.Render();

        //cam.transform.localEulerAngles = new Vector3(0, 180, 0);
        //cam.transform.localPosition = new Vector3(1000000, 1000000, 1000000);
        //cam.Render();

        //cam.transform.localPosition = new Vector3(0, 0, 0);
    }

    void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        Graphics.Blit(source, Nigiri_EmissiveCameraHelper.lightingTexture);
        Graphics.Blit(source, Nigiri_EmissiveCameraHelper.positionTexture, pvgiMaterial, 0);
        Graphics.Blit(source, Nigiri_EmissiveCameraHelper.lightMapTexture, lightMapMaterial, 0);

        Graphics.Blit(Nigiri_EmissiveCameraHelper.lightingTexture, lightingTextureDebug);
        Graphics.Blit(Nigiri_EmissiveCameraHelper.positionTexture, positionTextureDebug);
        Graphics.Blit(Nigiri_EmissiveCameraHelper.lightMapTexture, lightMapTextureDebug);


        Graphics.Blit(source, destination);
    }
}
