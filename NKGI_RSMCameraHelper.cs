using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NKGI_RSMCameraHelper : MonoBehaviour {

    public static RenderTexture vplPositionWS;
    public static RenderTexture vplNormalWS;
    public static RenderTexture flux;

    public RenderTexture dummyDepth;

    public RenderTexture DebugFlux;

    public RenderBuffer defaultColorBuffer;
    public RenderBuffer defaultDepthBuffer;

    public RenderBuffer[] RSMBuffer;

    public Material materialRSM;
    public Shader shaderRSM;

    public Camera localCam;

    void OnEnable()
    {
        shaderRSM = Shader.Find("NKGI/Reflective Shadow Map");
        materialRSM = new Material(shaderRSM);

        RenderTextureDescriptor RODesc = new RenderTextureDescriptor(Camera.main.pixelWidth, Camera.main.pixelHeight, RenderTextureFormat.ARGBFloat, 32);
        RODesc.enableRandomWrite = true;
        RODesc.useMipMap = false;

        vplPositionWS = new RenderTexture(RODesc);
        vplNormalWS = new RenderTexture(RODesc);
        flux = new RenderTexture(RODesc);
        dummyDepth = new RenderTexture(RODesc);
        vplPositionWS.wrapMode = TextureWrapMode.Clamp;
        vplNormalWS.wrapMode = TextureWrapMode.Clamp;
        flux.wrapMode = TextureWrapMode.Clamp;
        dummyDepth.wrapMode = TextureWrapMode.Clamp;

        RSMBuffer = new RenderBuffer[3];
        localCam = GetComponent<Camera>();

        DebugFlux = flux;


        localCam.SetReplacementShader(shaderRSM, "");
        

        //RSMBuffer[0] = vplPositionWS.colorBuffer;
        //RSMBuffer[1] = vplNormalWS.colorBuffer;
        //RSMBuffer[2] = flux.colorBuffer;

        //localCam.SetTargetBuffers(flux.colorBuffer, dummyDepth.depthBuffer);
    }

    void OnDisable()
    {
        if (vplPositionWS.IsCreated()) vplPositionWS.Release();
        if (vplNormalWS.IsCreated()) vplPositionWS.Release();
        if (flux.IsCreated()) vplPositionWS.Release();
    }
}
