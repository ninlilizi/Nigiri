using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NKGI : MonoBehaviour {

    public Shader shaderRSM;
    public Shader shaderRSMPost;
    public Shader shaderLightInjection;
    public Material materialRSM;
    public Material materialRSMPost;
    public Material materialLightInjection;

    ComputeShader shaderLightPropagation;

    RenderTexture lightVolumeR;
    RenderTexture lightVolumeG;
    RenderTexture lightVolumeB;

    RenderBuffer[] injectionBuffer;

    // Use this for initialization
    void OnEnable () {
        shaderRSM = Shader.Find("NKGI/Reflective Shadow Map");
        shaderRSMPost = Shader.Find("Hidden/NKGI_RSM_Post");
        shaderLightInjection = Shader.Find("Hidden/NKGI-LightInjection");
        materialRSM = new Material(shaderRSM);
        materialRSMPost = new Material(shaderRSMPost);
        materialLightInjection = new Material(shaderLightInjection);

        shaderLightPropagation = Resources.Load("BKGI-LightPropagation") as ComputeShader;

        lightVolumeR = new RenderTexture(32, 32, 0, RenderTextureFormat.ARGBFloat);
        lightVolumeR.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
        lightVolumeR.enableRandomWrite = true;
        lightVolumeR.volumeDepth = 32;
        lightVolumeR.hideFlags = HideFlags.HideAndDontSave;
        lightVolumeR.Create();

        lightVolumeG = new RenderTexture(32, 32, 0, RenderTextureFormat.ARGBFloat);
        lightVolumeG.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
        lightVolumeG.enableRandomWrite = true;
        lightVolumeG.volumeDepth = 32;
        lightVolumeG.hideFlags = HideFlags.HideAndDontSave;
        lightVolumeG.Create();

        lightVolumeB = new RenderTexture(32, 32, 0, RenderTextureFormat.ARGBFloat);
        lightVolumeB.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
        lightVolumeB.enableRandomWrite = true;
        lightVolumeB.volumeDepth = 32;
        lightVolumeB.hideFlags = HideFlags.HideAndDontSave;
        lightVolumeB.Create();

        //Create buffer for MRT
        injectionBuffer = new RenderBuffer[3];
    }

    private void OnDisable()
    {
        if (lightVolumeR != null) lightVolumeR.Release();
        if (lightVolumeG != null) lightVolumeG.Release();
        if (lightVolumeB != null) lightVolumeB.Release();
    }

    // Update is called once per frame
    void Update () {
		
	}

    void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        //Create Render Textures
        RenderTexture NKGI_RSM = RenderTexture.GetTemporary(source.width, source.height, 0);

        //Create Dummmy camera
        GameObject NKGI_RSMCameraGO = new GameObject();
        NKGI_RSMCameraGO.hideFlags = HideFlags.HideAndDontSave;
        Camera NKGI_RSMCamera = NKGI_RSMCameraGO.AddComponent<Camera>();
        NKGI_RSMCamera.CopyFrom(Camera.main);
        NKGI_RSMCamera.hideFlags = HideFlags.HideAndDontSave;

        //Render that camera to get the Reflective Shadow Map
        NKGI_RSMCamera.targetTexture = NKGI_RSM;
        NKGI_RSMCamera.SetReplacementShader(shaderRSM, "");
        NKGI_RSMCamera.Render();

        //Perform Light injection
        Shader.SetGlobalTexture("rsmFluxMap", NKGI_RSM);
        injectionBuffer[0] = lightVolumeR.colorBuffer;
        injectionBuffer[1] = lightVolumeG.colorBuffer;
        injectionBuffer[2] = lightVolumeB.colorBuffer;
        Graphics.SetRenderTarget(injectionBuffer, lightVolumeR.depthBuffer);
        Graphics.Blit(null, materialLightInjection, 0);

        //Perform Light propagation
        Graphics.SetRandomWriteTarget(0, lightVolumeR);
        Graphics.SetRandomWriteTarget(1, lightVolumeG);
        Graphics.SetRandomWriteTarget(2, lightVolumeB);
        shaderLightPropagation.SetTexture(0, "lpvR", lightVolumeR);
        shaderLightPropagation.SetTexture(0, "lpvG", lightVolumeG);
        shaderLightPropagation.SetTexture(0, "lpvB", lightVolumeB);
        shaderLightPropagation.Dispatch(0, source.width / 16, source.height / 2, 1);




        //Output /something/
        Graphics.Blit(NKGI_RSM, destination);
        //Graphics.Blit(source, destination, material_RSMPost);

        //Destroy Dummy Camera
        GameObject.DestroyImmediate(NKGI_RSMCameraGO);

        //Release Render Textures
        NKGI_RSM.Release();
    }
}
