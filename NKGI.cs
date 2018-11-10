using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteInEditMode]
[ImageEffectAllowedInSceneView]
public class NKGI : MonoBehaviour {

    public GameObject NKGI_RSMCameraGO;
    public Camera NKGI_RSMCamera;

    public Shader shaderRSM;
    public Shader shaderRender;
    public Shader shaderRSMPost;
    public Shader shaderLightInjection;
    public Shader shaderBlitGBuffer0;
    public Shader shaderBlitGBuffer1;
    public Shader shaderBlitGBuffer2;
    public Shader shaderBlitGBuffer3;
    public Shader shaderBlitGBuffer4;

    public Material materialRSM;
    public Material materialRender;
    public Material materialRSMPost;
    public Material materialLightInjection;
    public Material materialBlitGBuffer0;
    public Material materialBlitGBuffer1;
    public Material materialBlitGBuffer2;
    public Material materialBlitGBuffer3;
    public Material materialBlitGBuffer4;

    ComputeShader shaderLightPropagation;

    RenderBuffer[] RSMBuffer;
    RenderBuffer[] injectionBuffer;

    CommandBuffer endOfFrameCommands;

    RenderTextureDescriptor RODesc;
    RenderTextureDescriptor ROFluxDesc;
    RenderTextureDescriptor volumeDesc;


    // Use this for initialization
    void OnEnable ()
    {
        Camera localCam = GetComponent<Camera>();

        RODesc = new RenderTextureDescriptor(localCam.pixelWidth, localCam.pixelHeight, RenderTextureFormat.ARGBHalf, 32);
        RODesc.enableRandomWrite = false;
        RODesc.useMipMap = false;

        ROFluxDesc = new RenderTextureDescriptor(512, 512, RenderTextureFormat.ARGBHalf, 0);
        ROFluxDesc.enableRandomWrite = false;
        ROFluxDesc.useMipMap = false;

        volumeDesc = new RenderTextureDescriptor(32, 32, RenderTextureFormat.ARGBFloat, 0);
        volumeDesc.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
        volumeDesc.enableRandomWrite = true;
        volumeDesc.volumeDepth = 32;
        volumeDesc.useMipMap = false;


        shaderRSM = Shader.Find("NKGI/Reflective Shadow Map");
        shaderRender = Shader.Find("Hidden/NKGI-Render");
        shaderRSMPost = Shader.Find("Hidden/NKGI_RSM_Post");
        shaderLightInjection = Shader.Find("Hidden/NKGI-LightInjection");
        shaderBlitGBuffer0 = Shader.Find("Hidden/NKGI-Blit-gBuffer0");
        shaderBlitGBuffer1 = Shader.Find("Hidden/NKGI-Blit-gBuffer1");
        shaderBlitGBuffer2 = Shader.Find("Hidden/NKGI-Blit-gBuffer2");
        shaderBlitGBuffer3 = Shader.Find("Hidden/NKGI-Blit-gBuffer3");
        shaderBlitGBuffer4 = Shader.Find("Hidden/NKGI-Blit-gBuffer4");
        materialRSM = new Material(shaderRSM);
        materialRender = new Material(shaderRender);
        materialRSMPost = new Material(shaderRSMPost);
        materialLightInjection = new Material(shaderLightInjection);
        materialBlitGBuffer0 = new Material(shaderBlitGBuffer0);
        materialBlitGBuffer1 = new Material(shaderBlitGBuffer1);
        materialBlitGBuffer2 = new Material(shaderBlitGBuffer2);
        materialBlitGBuffer3 = new Material(shaderBlitGBuffer3);
        materialBlitGBuffer4 = new Material(shaderBlitGBuffer4);

        shaderLightPropagation = Resources.Load("BKGI-LightPropagation") as ComputeShader;

        //Create buffer for MRT
        injectionBuffer = new RenderBuffer[3];
        RSMBuffer = new RenderBuffer[3];

        endOfFrameCommands = new CommandBuffer();
        endOfFrameCommands.name ="NKGI";
        //endOfFrameCommands.ClearRenderTarget(true, true);
        Camera.main.AddCommandBuffer(CameraEvent.AfterEverything, endOfFrameCommands);

        //Create Dummmy camera
        NKGI_RSMCameraGO = new GameObject();
        NKGI_RSMCameraGO.hideFlags = HideFlags.DontSave;
        NKGI_RSMCamera = NKGI_RSMCameraGO.AddComponent<Camera>();
        //NKGI_RSMCameraGO.AddComponent<NKGI_RSMCameraHelper>();
        NKGI_RSMCamera.enabled = false;
    }

    private void OnDisable()
    {
        Camera.main.RemoveCommandBuffer(CameraEvent.AfterEverything, endOfFrameCommands);
        endOfFrameCommands.Clear();
        endOfFrameCommands.Dispose();

        //Destroy Dummy Camera
        GameObject.DestroyImmediate(NKGI_RSMCameraGO);
    }

    // Update is called once per frame
    void Update () {
		
	}

    void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        //Render Texture Descriptors
        //RenderTextureDescriptor RWDesc = new RenderTextureDescriptor(source.width, source.height, RenderTextureFormat.RInt, 32);
        //RWDesc.enableRandomWrite = true;
        //RWDesc.useMipMap = false;



        //Create Render Textures
        RenderTexture GI = RenderTexture.GetTemporary(RODesc);
        RenderTexture flux = RenderTexture.GetTemporary(ROFluxDesc);
        RenderTexture albedo = RenderTexture.GetTemporary(RODesc);
        RenderTexture occlusion = RenderTexture.GetTemporary(RODesc);
        RenderTexture normal = RenderTexture.GetTemporary(RODesc);
        RenderTexture position = RenderTexture.GetTemporary(RODesc);
        GI.wrapMode = TextureWrapMode.Clamp;
        flux.wrapMode = TextureWrapMode.Clamp;
        albedo.wrapMode = TextureWrapMode.Clamp;
        occlusion.wrapMode = TextureWrapMode.Clamp;
        normal.wrapMode = TextureWrapMode.Clamp;
        position.wrapMode = TextureWrapMode.Clamp;

        RenderTexture lightVolumeR = RenderTexture.GetTemporary(volumeDesc);
        RenderTexture lightVolumeG = RenderTexture.GetTemporary(volumeDesc);
        RenderTexture lightVolumeB = RenderTexture.GetTemporary(volumeDesc);
        lightVolumeR.wrapMode = TextureWrapMode.Clamp;
        lightVolumeG.wrapMode = TextureWrapMode.Clamp;
        lightVolumeB.wrapMode = TextureWrapMode.Clamp;

        RenderTexture DummyDepth = RenderTexture.GetTemporary(32, 32, 32);

        //Copy Initial albedo to use later
        Graphics.Blit(null, albedo, materialBlitGBuffer0);
        Graphics.Blit(null, normal, materialBlitGBuffer2);
        Shader.SetGlobalTexture("albedoMap", albedo);
        Shader.SetGlobalTexture("normalMap", normal);

        //Configure dummmy camera
        NKGI_RSMCamera.CopyFrom(Camera.main);
        NKGI_RSMCamera.targetDisplay = 7;
        NKGI_RSMCameraGO.transform.SetPositionAndRotation(Camera.main.transform.position, Camera.main.transform.rotation);

        //Render that camera to get the Reflective Shadow Map
        //NKGI_RSMCamera.SetReplacementShader(shaderRSM, "");
        //NKGI_RSMCamera.Render();
        NKGI_RSMCamera.RenderWithShader(shaderRSM, "");
        Graphics.Blit(null, flux, materialBlitGBuffer3);
        Shader.SetGlobalTexture("RSMFlux", flux);


        //Perform Light injection
        RenderBuffer oldRTc = RenderTexture.active.colorBuffer;
        RenderBuffer oldRTd = RenderTexture.active.depthBuffer;

        RenderBuffer[] rb = new RenderBuffer[] { lightVolumeR.colorBuffer, lightVolumeG.colorBuffer, lightVolumeB.colorBuffer };

        Graphics.SetRandomWriteTarget(3, lightVolumeR);
        Graphics.SetRandomWriteTarget(4, lightVolumeG);
        Graphics.SetRandomWriteTarget(5, lightVolumeB);
        NKGI_RSMCamera.SetTargetBuffers(rb, DummyDepth.depthBuffer);
        NKGI_RSMCamera.RenderWithShader(shaderLightInjection, "");

        NKGI_RSMCamera.SetTargetBuffers(oldRTc, oldRTd);


        //RenderTexture.active = oldRT;

        //Perform Light propagation
        Graphics.SetRandomWriteTarget(0, lightVolumeR);
        Graphics.SetRandomWriteTarget(1, lightVolumeG);
        Graphics.SetRandomWriteTarget(2, lightVolumeB);
        shaderLightPropagation.SetTexture(0, "lpvR", lightVolumeR);
        shaderLightPropagation.SetTexture(0, "lpvG", lightVolumeG);
        shaderLightPropagation.SetTexture(0, "lpvB", lightVolumeB);
        shaderLightPropagation.Dispatch(0, source.width / 16, source.height / 2, 1);
        Graphics.ClearRandomWriteTargets();

        //Render GI
        materialRender.SetTexture("albedoMap", albedo);
        materialRender.SetTexture("lpvR", lightVolumeR);
        materialRender.SetTexture("lpvG", lightVolumeG);
        materialRender.SetTexture("lpvB", lightVolumeB);
        Graphics.Blit(source, GI, materialRender);

        //Debug.Log(GI.width + " / " + GI.height);


        //Graphics.Blit(GI, destination)
        materialRSMPost.SetTexture("GI", GI);
        materialRSMPost.SetTexture("albedoMap", albedo);
        Graphics.Blit(source, destination, materialRSMPost);



        //Release Render Textures
        GI.Release();
        flux.Release();
        //RSM.Release();
        albedo.Release();
        occlusion.Release();
        position.Release();
        normal.Release();
        DummyDepth.Release();
        lightVolumeR.Release();
        lightVolumeG.Release();
        lightVolumeB.Release();
        //vplPositionWS.Release();
        //vplNormalWS.Release();
        //flux.Release();
    }
}
