using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
[ImageEffectAllowedInSceneView]
public class Nigiri : MonoBehaviour {

	public enum DebugVoxelGrid {
		GRID_1,
		GRID_2,
		GRID_3,
		GRID_4,
		GRID_5
	};

    [Header("General Settings")]
    public Vector2Int resolution = new Vector2Int(256, 256);
    [Range(0.05f, 8)]
    public float AmbientStrength = 1.0f;
    [Range(0.0f, 8)]
    public float EmissiveIntensity = 1.0f;
    [Range(0.01f, 16)]
    public float EmissiveAttribution = 1.0f;

    [Header("Voxelization Settings")]
    public LayerMask emissiveLayer;

    public float GIAreaSize = 50;
	public int highestVoxelResolution = 256;
    [Range(0, 1)]
    public float shadowBoostPower;

    [Header("Light Propagation Settings")]
    public bool propagateLight;
    [Range(1, 8)]
    public int LPVIterations = 1;
    [Range(0, 128)]
    public float LPVInverseFalloff = 1;
    [Range(1, 4)]
    public int updateSpeedFactor = 1;
    [Range(4, 5)]
    public int fastResolveFactor = 1;
    private Vector2Int injectionTextureResolution = new Vector2Int(1024, 1024);


	[Header("Cone Trace Settings")]
    [Range(1, 32)]
	public int maximumIterations = 8;
    [Range(0.01f, 2)]
    public float coneLength = 1;
    [Range(0.01f, 12)]
    public float coneWidth = 6;
    [Range(0.1f, 4)]
    public float GIGain = 1;
    [Range(0.1f, 4)]
    public float NearLightGain = 1.14f;
    [Range(0.1f, 1)]
    public float OcclusionStrength = 0.15f;
    [Range(0.1f, 1)]
    public float NearOcclusionStrength = 0.5f;
    [Range(0.1f, 1)]
    public float FarOcclusionStrength = 1;
    [Range(0.1f, 1)]
    public float OcclusionPower = 0.65f;

    private bool stochasticSampling = true;

    [Header("Reflection Settings")]
    public bool traceReflections = true;
    //public int downsample = 2;
    [Range(0.1f, 0.25f)]
    public float rayOffset = 0.1f;
    [Range(0.1f, 0.25f)]
    public float rayStep = 0.1f;
    [Range(0.1f, 4)]
    public float BalanceGain = 1;
    [Range(1, 32)]
    public int maximumIterationsReflection = 16;

    [Header("Debug Settings")]
    public bool VisualiseGI = false;
    public bool VisualizeVoxels = false;
    public DebugVoxelGrid debugVoxelGrid = DebugVoxelGrid.GRID_1;

    public Texture2D[] blueNoise;
    public bool forceImmediateRefresh = false;

    //[Header("Shaders")]
    private Shader tracingShader;
    private Shader blitGBufferShader;
    private Shader fxaaShader;
    private Shader positionShader;
    private ComputeShader nigiri_VoxelEntry;
    private ComputeShader nigiri_InjectionCompute;
    private ComputeShader clearComputeCache;
    private ComputeShader transferIntsCompute;
    private ComputeShader mipFilterCompute;
    private ComputeShader lightPropagateCompute;

    //[Header("Materials")]
    private Material pvgiMaterial;
    private Material blitGBufferMaterial;
    private Material fxaaMaterial;
    private Material positionMaterial;

    [Header("Render Textures")]

    private RenderTextureDescriptor voxelGridDescriptorFloat4;

    public static RenderTexture voxelInjectionGrid;
    public static RenderTexture voxelPropagationGrid;
    public static RenderTexture voxelPropagatedGrid;
    public static RenderTexture voxelGrid1;
    public static RenderTexture voxelGrid2;
    public static RenderTexture voxelGrid3;
    public static RenderTexture voxelGrid4;
    public static RenderTexture voxelGrid5;

    public RenderTexture lightingTexture;
    public RenderTexture lightingTexture2;
    public RenderTexture positionTexture;
    public RenderTexture orthographicPositionTexture;

    private RenderTexture blur;
    public RenderTexture gi;


    private PathCacheBuffer pathCacheBuffer;
    private ComputeBuffer voxelUpdateCounter;
    private int tracedTexture1UpdateCount;

    private float lengthOfCone = 0.0f;

    int voxelizationSliceOffset;
    int voxelizationSliceDispatch;

    int frameSwitch = 0;
    int voxelizationSlice = 0;
    int mipSwitch = 0;
    int lpvSwitch = 0;
    bool fastResolveSwitch = true;

    bool prevPropagateLight = false;

    GameObject emissiveCameraGO;
    Camera emissiveCamera;

    private void Start()
    {
        UpdateForceGI();
    }

    // Use this for initialization
    void OnEnable () {

        clearComputeCache = Resources.Load("SEGIClear_Cache") as ComputeShader;
        transferIntsCompute = Resources.Load("SEGITransferInts_C") as ComputeShader;
        mipFilterCompute = Resources.Load("Nigiri_MipFilter") as ComputeShader;
        lightPropagateCompute = Resources.Load("Nigiri_LightPropagation") as ComputeShader;
        fxaaShader = Shader.Find("Hidden/Nigiri_BilateralBlur");
        blitGBufferShader = Shader.Find("Hidden/Nigiri_Blit_gBuffer0");
        fxaaShader = Shader.Find("Hidden/Nigiri_FXAA");
        positionShader = Shader.Find("Hidden/Nigiri_Blit_Position_LastCameraDepthTexture");
        fxaaMaterial = new Material(fxaaShader);

        nigiri_VoxelEntry = Resources.Load("nigiri_VoxelEntry") as ComputeShader;
        nigiri_InjectionCompute = Resources.Load("Nigiri_Injection") as ComputeShader;

        Screen.SetResolution(resolution.x, resolution.y, true);

		GetComponent<Camera>().depthTextureMode = DepthTextureMode.Depth | DepthTextureMode.DepthNormals;

		if (tracingShader == null)  tracingShader = Shader.Find("Hidden/Nigiri_Tracing");
        pvgiMaterial = new Material(tracingShader);

        if (blitGBufferMaterial == null) blitGBufferMaterial = new Material(blitGBufferShader);

        if (positionMaterial == null) positionMaterial = new Material(positionShader);

        InitializeVoxelGrid();

		lightingTexture = new RenderTexture (injectionTextureResolution.x, injectionTextureResolution.y, 32, RenderTextureFormat.ARGBHalf);
        lightingTexture2 = new RenderTexture(injectionTextureResolution.x, injectionTextureResolution.y, 32, RenderTextureFormat.ARGBHalf);
        positionTexture = new RenderTexture(injectionTextureResolution.x, injectionTextureResolution.y, 32, RenderTextureFormat.ARGBHalf);
        orthographicPositionTexture = new RenderTexture(1024, 1024, 16, RenderTextureFormat.ARGBHalf);
        gi = new RenderTexture(injectionTextureResolution.x, injectionTextureResolution.y, 0, RenderTextureFormat.ARGBHalf);
        blur = new RenderTexture(injectionTextureResolution.x, injectionTextureResolution.y, 0, RenderTextureFormat.ARGBHalf);
        lightingTexture.filterMode = FilterMode.Bilinear;
        lightingTexture2.filterMode = FilterMode.Bilinear;
        blur.filterMode = FilterMode.Bilinear;
        gi.filterMode = FilterMode.Bilinear;

        lightingTexture.Create();
        lightingTexture2.Create();
        blur.Create();
        positionTexture.Create();
        orthographicPositionTexture.Create();
        gi.Create();

        voxelUpdateCounter = new ComputeBuffer((injectionTextureResolution.x / 4) * (injectionTextureResolution.y / 4), 4, ComputeBufferType.Default);

        //Get blue noise textures
        blueNoise = new Texture2D[64];
        for (int i = 0; i < 64; i++)
        {
            string fileName = "LDR_RGBA_" + i.ToString();
            Texture2D blueNoiseTexture = Resources.Load("Noise Textures/" + fileName) as Texture2D;

            if (blueNoiseTexture == null)
            {
                Debug.LogWarning("Unable to find noise texture \"Assets/SEGI/Resources/Noise Textures/" + fileName + "\" for SEGI!");
            }

            blueNoise[i] = blueNoiseTexture;

        }

        pathCacheBuffer = new PathCacheBuffer();
        pathCacheBuffer.Init(256);

        if (!emissiveCameraGO)
        {
            emissiveCameraGO = new GameObject("NKGI_EMISSIVECAMERA");
            emissiveCameraGO.transform.parent = GetComponent<Camera>().transform;
            //emissiveCameraGO.transform.localEulerAngles = new Vector3(90, 0, 0);
            emissiveCameraGO.transform.localEulerAngles = new Vector3(0, 0, 0);
            emissiveCameraGO.hideFlags = HideFlags.DontSave;
            emissiveCamera = emissiveCameraGO.AddComponent<Camera>();
            emissiveCamera.CopyFrom(GetComponent<Camera>());
            emissiveCameraGO.AddComponent<Nigiri_EmissiveCameraHelper>();
            emissiveCameraGO.transform.localPosition = new Vector3(0, 0, 0);
            emissiveCamera.orthographicSize = (int)(GIAreaSize * 0.25);
            emissiveCamera.farClipPlane = (int)(GIAreaSize * 0.25);
            emissiveCamera.enabled = false;
            Nigiri_EmissiveCameraHelper.injectionResolution = injectionTextureResolution;
            
        }
        UpdateForceGI();
    }

	// Function to initialize the voxel grid data
	private void InitializeVoxelGrid() {

		voxelGridDescriptorFloat4 = new RenderTextureDescriptor ();
		voxelGridDescriptorFloat4.bindMS = false;
		voxelGridDescriptorFloat4.colorFormat = RenderTextureFormat.ARGBHalf;
		voxelGridDescriptorFloat4.depthBufferBits = 0;
		voxelGridDescriptorFloat4.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
		voxelGridDescriptorFloat4.enableRandomWrite = true;
		voxelGridDescriptorFloat4.width = highestVoxelResolution;
		voxelGridDescriptorFloat4.height = highestVoxelResolution;
		voxelGridDescriptorFloat4.volumeDepth = highestVoxelResolution;
		voxelGridDescriptorFloat4.msaaSamples = 1;
		voxelGridDescriptorFloat4.sRGB = true;

        voxelInjectionGrid = new RenderTexture(voxelGridDescriptorFloat4);

        voxelPropagationGrid = new RenderTexture (voxelGridDescriptorFloat4);
        voxelPropagatedGrid = new RenderTexture(voxelGridDescriptorFloat4);
        voxelGrid1 = new RenderTexture(voxelGridDescriptorFloat4);

        voxelGridDescriptorFloat4.width = highestVoxelResolution / 2;
		voxelGridDescriptorFloat4.height = highestVoxelResolution / 2;
		voxelGridDescriptorFloat4.volumeDepth = highestVoxelResolution / 2;

		voxelGrid2 = new RenderTexture (voxelGridDescriptorFloat4);

		voxelGridDescriptorFloat4.width = highestVoxelResolution / 4;
		voxelGridDescriptorFloat4.height = highestVoxelResolution / 4;
		voxelGridDescriptorFloat4.volumeDepth = highestVoxelResolution / 4;

		voxelGrid3 = new RenderTexture (voxelGridDescriptorFloat4);

		voxelGridDescriptorFloat4.width = highestVoxelResolution / 8;
		voxelGridDescriptorFloat4.height = highestVoxelResolution / 8;
		voxelGridDescriptorFloat4.volumeDepth = highestVoxelResolution / 8;

		voxelGrid4 = new RenderTexture (voxelGridDescriptorFloat4);

		voxelGridDescriptorFloat4.width = highestVoxelResolution / 16;
		voxelGridDescriptorFloat4.height = highestVoxelResolution / 16;
		voxelGridDescriptorFloat4.volumeDepth = highestVoxelResolution / 16;

		voxelGrid5 = new RenderTexture (voxelGridDescriptorFloat4);

        voxelInjectionGrid.filterMode = FilterMode.Bilinear;

        voxelPropagationGrid.filterMode = FilterMode.Bilinear;
        voxelPropagatedGrid.filterMode = FilterMode.Bilinear;
        voxelGrid1.filterMode = FilterMode.Bilinear;
        voxelGrid2.filterMode = FilterMode.Bilinear;
		voxelGrid3.filterMode = FilterMode.Bilinear;
		voxelGrid4.filterMode = FilterMode.Bilinear;
		voxelGrid5.filterMode = FilterMode.Bilinear;

        voxelInjectionGrid.Create();

        voxelPropagationGrid.Create();
        voxelPropagatedGrid.Create();
        voxelGrid1.Create();
        voxelGrid2.Create();
		voxelGrid3.Create();
		voxelGrid4.Create();
		voxelGrid5.Create();

	}

	// Function to update data in the voxel grid
	private void UpdateVoxelGrid ()
    {
        Nigiri_EmissiveCameraHelper.DoRender();

        //orthographicPositionTexture = Nigiri_EmissiveCameraHelper.positionTexture;

        if (Nigiri_EmissiveCameraHelper.lightMapBuffer == null) return;

        if (fastResolveSwitch)
        {
            if (fastResolveFactor == 5)
            {
                voxelizationSlice = (voxelizationSlice + 1) % (4);
                voxelizationSliceOffset = 4194304;
                voxelizationSliceDispatch = 16384;

                if (voxelizationSlice == 3 && lpvSwitch == 2) fastResolveSwitch = false;
            }
            if (fastResolveFactor == 4)
            {
                voxelizationSlice = (voxelizationSlice + 1) % (8);
                voxelizationSliceOffset = 2097152;
                voxelizationSliceDispatch = 8192;

                if (voxelizationSlice == 7 && lpvSwitch == 2) fastResolveSwitch = false;
            }
        }
        else
        {
            if (updateSpeedFactor == 4)
            {
                voxelizationSlice = (voxelizationSlice + 1) % (8);
                voxelizationSliceOffset = 2097152;
                voxelizationSliceDispatch = 8192;
            }
            else if (updateSpeedFactor == 3)
            {
                voxelizationSlice = (voxelizationSlice + 1) % (16);
                voxelizationSliceOffset = 1048576;
                voxelizationSliceDispatch = 4096;
            }
            else if (updateSpeedFactor == 2)
            {
                voxelizationSlice = (voxelizationSlice + 1) % (32);
                voxelizationSliceOffset = 524288;
                voxelizationSliceDispatch = 2048;
            }
            else if (updateSpeedFactor == 1)
            {
                voxelizationSlice = (voxelizationSlice + 1) % (64);
                voxelizationSliceOffset = 262144;
                voxelizationSliceDispatch = 1024;
            }
        }
        if (voxelizationSlice == 0) lpvSwitch = (lpvSwitch + 1) % (3);

        // Kernel index for the entry point in compute shader
        int kernelHandle = nigiri_InjectionCompute.FindKernel("CSMain");

        
        nigiri_InjectionCompute.SetBuffer(0, "lightMapBuffer", Nigiri_EmissiveCameraHelper.lightMapBuffer);
        nigiri_InjectionCompute.SetTexture(0, "voxelGrid", voxelInjectionGrid);
        nigiri_InjectionCompute.SetInt("offsetStart", voxelizationSlice * voxelizationSliceOffset);
        nigiri_InjectionCompute.SetFloat ("worldVolumeBoundary", GIAreaSize);
        nigiri_InjectionCompute.Dispatch(0, voxelizationSliceDispatch, 1, 1);

        // These apply to all grids
        nigiri_VoxelEntry.SetTexture(0, "NoiseTexture", blueNoise[frameSwitch % 64]);
        nigiri_VoxelEntry.SetMatrix("InverseViewMatrix", GetComponent<Camera>().cameraToWorldMatrix);
        nigiri_VoxelEntry.SetMatrix("InverseProjectionMatrix", GetComponent<Camera>().projectionMatrix.inverse);
        nigiri_VoxelEntry.SetBuffer(kernelHandle, "voxelUpdateCounter", voxelUpdateCounter);
        nigiri_VoxelEntry.SetTexture(kernelHandle, "lightingTexture", lightingTexture);
        nigiri_VoxelEntry.SetTexture(kernelHandle, "lightingTexture2", lightingTexture2);
        nigiri_VoxelEntry.SetTexture(kernelHandle, "positionTexture", positionTexture);
        nigiri_VoxelEntry.SetTexture(kernelHandle, "voxelInjectionGrid", voxelInjectionGrid);
        nigiri_VoxelEntry.SetInt("injectionTextureResolutionX", injectionTextureResolution.x);
        nigiri_VoxelEntry.SetFloat("shadowStrength", shadowBoostPower);

        // Voxelize main cam
        nigiri_VoxelEntry.SetTexture(kernelHandle, "voxelGrid", voxelGrid1);
        nigiri_VoxelEntry.SetInt("voxelResolution", highestVoxelResolution);
        nigiri_VoxelEntry.SetFloat("worldVolumeBoundary", GIAreaSize);
        nigiri_VoxelEntry.Dispatch(kernelHandle, injectionTextureResolution.x / 16, injectionTextureResolution.y / 16, 1);

        // Prepare to voxelize secondary cam
        nigiri_VoxelEntry.SetMatrix("InverseViewMatrix", emissiveCamera.cameraToWorldMatrix);
        nigiri_VoxelEntry.SetMatrix("InverseProjectionMatrix", emissiveCamera.projectionMatrix.inverse);
        nigiri_VoxelEntry.SetTexture(kernelHandle, "positionTexture", orthographicPositionTexture);
        nigiri_VoxelEntry.SetTexture(kernelHandle, "lightingTexture", Nigiri_EmissiveCameraHelper.lightingTexture);

        // Voxelize secondary cam
        nigiri_VoxelEntry.SetTexture(kernelHandle, "voxelGrid", voxelGrid1);
        nigiri_VoxelEntry.SetTexture(kernelHandle, "voxelPropagatedGrid", voxelPropagationGrid);
        nigiri_VoxelEntry.SetInt("voxelResolution", highestVoxelResolution);
        nigiri_VoxelEntry.SetFloat("worldVolumeBoundary", GIAreaSize);
        nigiri_VoxelEntry.Dispatch(kernelHandle, injectionTextureResolution.x / 16, injectionTextureResolution.y / 16, 1);

        // Do light propagation
        if (propagateLight)
        {
            if (lpvSwitch == 0)
            {
                lightPropagateCompute.SetTexture(0, "RG0", voxelGrid1);
                lightPropagateCompute.SetTexture(0, "RG1", voxelPropagationGrid);
                lightPropagateCompute.SetInt("offsetStart", voxelizationSlice * voxelizationSliceOffset);
                lightPropagateCompute.SetInt("Resolution", highestVoxelResolution);
                lightPropagateCompute.Dispatch(0, voxelizationSliceDispatch, 1, 1);
            }
            else if (lpvSwitch == 1)
            {
                for (int i = 0; i < LPVIterations; i++)
                {
                    lightPropagateCompute.SetFloat("LPVInverseFalloff", (LPVInverseFalloff * 0.001f));
                    lightPropagateCompute.SetTexture(1, "RG0", voxelPropagationGrid);
                    lightPropagateCompute.SetInt("offsetStart", voxelizationSlice * voxelizationSliceOffset);
                    lightPropagateCompute.SetInt("Resolution", highestVoxelResolution);
                    lightPropagateCompute.Dispatch(1, voxelizationSliceDispatch, 1, 1);
                }
            }
            else if (lpvSwitch == 2)
            {
                lightPropagateCompute.SetTexture(0, "RG0", voxelPropagationGrid);
                lightPropagateCompute.SetTexture(0, "RG1", voxelPropagatedGrid);
                lightPropagateCompute.SetInt("offsetStart", voxelizationSlice * voxelizationSliceOffset);
                lightPropagateCompute.SetInt("Resolution", highestVoxelResolution);
                lightPropagateCompute.Dispatch(0, voxelizationSliceDispatch, 1, 1);
            }
        }



        if (mipSwitch == 0 && propagateLight)
        {
            int destinationRes = (int)highestVoxelResolution / 2;
            mipFilterCompute.SetInt("destinationRes", destinationRes);
            if (fastResolveSwitch && lpvSwitch == 0) mipFilterCompute.SetTexture(0, "Source", voxelGrid1);
            else if (fastResolveSwitch && lpvSwitch > 0) mipFilterCompute.SetTexture(0, "Source", voxelPropagationGrid);
            else mipFilterCompute.SetTexture(0, "Source", voxelPropagatedGrid);
            mipFilterCompute.SetTexture(0, "Destination", voxelGrid2);
            mipFilterCompute.Dispatch(0, destinationRes / 8, destinationRes / 8, 1);
        }
        else if (mipSwitch == 0 && !propagateLight)
        {
            int destinationRes = (int)highestVoxelResolution / 2;
            mipFilterCompute.SetInt("destinationRes", destinationRes);
            mipFilterCompute.SetTexture(0, "Source", voxelGrid1);
            mipFilterCompute.SetTexture(0, "Destination", voxelGrid2);
            mipFilterCompute.Dispatch(0, destinationRes / 8, destinationRes / 8, 1);
        }
        else if (mipSwitch == 1)
        {
            int destinationRes = (int)highestVoxelResolution / 4;
            mipFilterCompute.SetInt("destinationRes", destinationRes);
            mipFilterCompute.SetTexture(1, "Source", voxelGrid2);
            mipFilterCompute.SetTexture(1, "Destination", voxelGrid3);
            mipFilterCompute.Dispatch(1, destinationRes / 8, destinationRes / 8, 1);
        }
        else if (mipSwitch == 2)
        {
            int destinationRes = (int)highestVoxelResolution / 8;
            mipFilterCompute.SetInt("destinationRes", destinationRes);
            mipFilterCompute.SetTexture(1, "Source", voxelGrid3);
            mipFilterCompute.SetTexture(1, "Destination", voxelGrid4);
            mipFilterCompute.Dispatch(1, destinationRes / 8, destinationRes / 8, 1);
        }
        else if (mipSwitch == 3)
        {
            int destinationRes = (int)highestVoxelResolution / 16;
            mipFilterCompute.SetInt("destinationRes", destinationRes);
            mipFilterCompute.SetTexture(1, "Source", voxelGrid4);
            mipFilterCompute.SetTexture(1, "Destination", voxelGrid5);
            mipFilterCompute.Dispatch(1, destinationRes / 8, destinationRes / 8, 1);
        }
        mipSwitch = (mipSwitch + 1) % (4);
    }

	// This is called once per frame after the scene is rendered
	void OnRenderImage (RenderTexture source, RenderTexture destination)
    {
        if (forceImmediateRefresh || prevPropagateLight != propagateLight)
        {
            forceImmediateRefresh = false;
            UpdateForceGI();
        }

        Camera localCam = GetComponent<Camera>();

        if (pathCacheBuffer == null)
        {
            Debug.Log("<SEGI> Creating path cache buffers");
            pathCacheBuffer = new PathCacheBuffer();
            pathCacheBuffer.Init(256);
        }

        if (pathCacheBuffer.front == null || pathCacheBuffer.back == null)
        {
            Debug.Log("<SEGI> Recreating patch cache buffers");
            pathCacheBuffer.Init(256);
        }


        lengthOfCone = (32.0f * GIAreaSize) / (highestVoxelResolution * Mathf.Tan (Mathf.PI / 6.0f));

        pvgiMaterial.SetMatrix ("InverseViewMatrix", GetComponent<Camera>().cameraToWorldMatrix);
		pvgiMaterial.SetMatrix ("InverseProjectionMatrix", GetComponent<Camera>().projectionMatrix.inverse);
		Shader.SetGlobalFloat("worldVolumeBoundary", GIAreaSize);
		pvgiMaterial.SetFloat ("maximumIterations", maximumIterations);
		pvgiMaterial.SetFloat ("indirectLightingStrength", AmbientStrength);
        Shader.SetGlobalFloat("EmissiveStrength", EmissiveIntensity);
        Shader.SetGlobalFloat("EmissiveAttribution", EmissiveAttribution); 
        pvgiMaterial.SetFloat ("lengthOfCone", lengthOfCone);
        pvgiMaterial.SetFloat("coneLength", coneLength);
        pvgiMaterial.SetFloat("coneWidth", coneWidth);
   
        pvgiMaterial.SetFloat("rayStep", rayStep);
        pvgiMaterial.SetFloat("rayOffset", rayOffset);
        pvgiMaterial.SetFloat("BalanceGain", BalanceGain * 10);
        pvgiMaterial.SetFloat("maximumIterationsReflection", (float)maximumIterationsReflection);
        pvgiMaterial.SetVector("mainCameraPosition", GetComponent<Camera>().transform.position);
        pvgiMaterial.SetInt("DoReflections", traceReflections ? 1 : 0);

        Shader.SetGlobalInt("highestVoxelResolution", highestVoxelResolution);
        pvgiMaterial.SetInt("StochasticSampling", stochasticSampling ? 1 : 0);
        pvgiMaterial.SetInt("VisualiseGI", VisualiseGI ? 1 : 0);
        pvgiMaterial.SetFloat("coneLength", coneLength);
        pvgiMaterial.SetFloat("GIGain", GIGain);
        pvgiMaterial.SetFloat("NearLightGain", NearLightGain);
        pvgiMaterial.SetFloat("OcclusionStrength", OcclusionStrength);
        pvgiMaterial.SetFloat("NearOcclusionStrength", NearOcclusionStrength);
        pvgiMaterial.SetFloat("FarOcclusionStrength", FarOcclusionStrength);
        pvgiMaterial.SetFloat("OcclusionPower", OcclusionPower);

        Graphics.Blit(source, lightingTexture);
        Graphics.Blit(null, lightingTexture2, blitGBufferMaterial);
        Graphics.Blit(source, positionTexture, pvgiMaterial, 0);

        // Configure emissive camera
        emissiveCamera.cullingMask = emissiveLayer;
        //emissiveCameraGO.transform.localPosition = new Vector3(0, 0, -(int)(emissiveCamera.farClipPlane * 0.5));

        UpdateVoxelGrid();

        if (fastResolveSwitch && lpvSwitch > 0  && propagateLight) pvgiMaterial.SetTexture("voxelGrid1", voxelPropagationGrid);
        else if (propagateLight) pvgiMaterial.SetTexture("voxelGrid1", voxelPropagatedGrid);
        else pvgiMaterial.SetTexture("voxelGrid1", voxelGrid1);
        pvgiMaterial.SetTexture("voxelGrid2", voxelGrid2);
        pvgiMaterial.SetTexture("voxelGrid3", voxelGrid3);
        pvgiMaterial.SetTexture("voxelGrid4", voxelGrid4);
        pvgiMaterial.SetTexture("voxelGrid5", voxelGrid5);

        if (VisualizeVoxels) {
			if (debugVoxelGrid == DebugVoxelGrid.GRID_1) {
				pvgiMaterial.EnableKeyword ("GRID_1");
				pvgiMaterial.DisableKeyword ("GRID_2");
				pvgiMaterial.DisableKeyword ("GRID_3");
				pvgiMaterial.DisableKeyword ("GRID_4");
				pvgiMaterial.DisableKeyword ("GRID_5");
			} else if (debugVoxelGrid == DebugVoxelGrid.GRID_2) {
				pvgiMaterial.DisableKeyword ("GRID_1");
				pvgiMaterial.EnableKeyword ("GRID_2");
				pvgiMaterial.DisableKeyword ("GRID_3");
				pvgiMaterial.DisableKeyword ("GRID_4");
				pvgiMaterial.DisableKeyword ("GRID_5");
			} else if (debugVoxelGrid == DebugVoxelGrid.GRID_3) {
				pvgiMaterial.DisableKeyword ("GRID_1");
				pvgiMaterial.DisableKeyword ("GRID_2");
				pvgiMaterial.EnableKeyword ("GRID_3");
				pvgiMaterial.DisableKeyword ("GRID_4");
				pvgiMaterial.DisableKeyword ("GRID_5");
			} else if (debugVoxelGrid == DebugVoxelGrid.GRID_4) {
				pvgiMaterial.DisableKeyword ("GRID_1");
				pvgiMaterial.DisableKeyword ("GRID_2");
				pvgiMaterial.DisableKeyword ("GRID_3");
				pvgiMaterial.EnableKeyword ("GRID_4");
				pvgiMaterial.DisableKeyword ("GRID_5");
			} else {
				pvgiMaterial.DisableKeyword ("GRID_1");
				pvgiMaterial.DisableKeyword ("GRID_2");
				pvgiMaterial.DisableKeyword ("GRID_3");
				pvgiMaterial.DisableKeyword ("GRID_4");
				pvgiMaterial.EnableKeyword ("GRID_5");
			}

			Graphics.Blit (source, destination, pvgiMaterial, 1);
		} else {

            if (tracedTexture1UpdateCount > 48)
            {
                clearComputeCache.SetInt("Resolution", 256);
                clearComputeCache.SetBuffer(1, "RG1", pathCacheBuffer.back);
                clearComputeCache.SetInt("zStagger", tracedTexture1UpdateCount - 48);
                clearComputeCache.Dispatch(1, 256 / 16, 256 / 16, 1);
            }
            else if (tracedTexture1UpdateCount > 32)
            {
                transferIntsCompute.SetBuffer(3, "ResultBuffer", pathCacheBuffer.front);
                transferIntsCompute.SetBuffer(3, "InputBuffer", pathCacheBuffer.back);
                transferIntsCompute.SetInt("zStagger", tracedTexture1UpdateCount - 32);
                transferIntsCompute.SetInt("Resolution", 256);
                transferIntsCompute.Dispatch(3, 256 / 16, 256 / 16, 1);
            }
            tracedTexture1UpdateCount = (tracedTexture1UpdateCount + 1) % (65);


            Shader.SetGlobalTexture("NoiseTexture", blueNoise[frameSwitch % 64]);
            Shader.SetGlobalBuffer("tracedBuffer0", pathCacheBuffer.front);
            Graphics.SetRandomWriteTarget(1, pathCacheBuffer.back);
            Graphics.Blit (source, gi, pvgiMaterial, 2);
            Graphics.ClearRandomWriteTargets();


            Graphics.Blit(gi, blur, fxaaMaterial, 0);
            Graphics.Blit(blur, gi, fxaaMaterial, 1);

             Graphics.Blit(gi, destination);

            //Advance the frame counter
            frameSwitch = (frameSwitch + 1) % (64);

        }

	}

    private void OnDisable()
    {

        if (pathCacheBuffer != null) pathCacheBuffer.Cleanup();
        if (lightingTexture != null) lightingTexture.Release();
        if (lightingTexture2 != null) lightingTexture2.Release();
        if (positionTexture != null) positionTexture.Release();
        if (orthographicPositionTexture != null) orthographicPositionTexture.Release();
        if (gi != null) gi.Release();
        if (blur != null) blur.Release();

        if (voxelUpdateCounter != null) voxelUpdateCounter.Release();
        if (emissiveCameraGO != null) GameObject.DestroyImmediate(emissiveCameraGO);

        if (voxelInjectionGrid != null) voxelInjectionGrid.Release();

        if (voxelPropagationGrid != null) voxelPropagationGrid.Release();
        if (voxelPropagatedGrid != null) voxelPropagatedGrid.Release();
        if (voxelGrid1 != null) voxelGrid1.Release();
        if (voxelGrid2 != null) voxelGrid2.Release();
        if (voxelGrid3 != null) voxelGrid3.Release();
        if (voxelGrid4 != null) voxelGrid4.Release();
        if (voxelGrid5 != null) voxelGrid5.Release();
    }

    public void UpdateForceGI()
    {
        /*clearComputeCache.SetTexture(0, "RG0", voxelPropagationGrid);
        clearComputeCache.SetInt("Res", 256);
        clearComputeCache.Dispatch(0, 256 / 16, 256 / 16, 1);*/

        /*clearComputeCache.SetTexture(0, "RG0", voxelPropagatedGrid);
        clearComputeCache.SetInt("Res", 256);
        clearComputeCache.Dispatch(0, 256 / 16, 256 / 16, 1);*/

        clearComputeCache.SetTexture(0, "RG0", voxelGrid1);
        clearComputeCache.SetInt("Res", 256);
        clearComputeCache.Dispatch(0, 256 / 16, 256 / 16, 1);

        clearComputeCache.SetTexture(0, "RG0", voxelGrid2);
        clearComputeCache.SetInt("Res", 256);
        clearComputeCache.Dispatch(0, 256 / 16, 256 / 16, 1);

        clearComputeCache.SetTexture(0, "RG0", voxelGrid3);
        clearComputeCache.SetInt("Res", 256);
        clearComputeCache.Dispatch(0, 256 / 16, 256 / 16, 1);

        clearComputeCache.SetTexture(0, "RG0", voxelGrid4);
        clearComputeCache.SetInt("Res", 256);
        clearComputeCache.Dispatch(0, 256 / 16, 256 / 16, 1);

        clearComputeCache.SetTexture(0, "RG0", voxelGrid5);
        clearComputeCache.SetInt("Res", 256);
        clearComputeCache.Dispatch(0, 256 / 16, 256 / 16, 1);

        lpvSwitch = 0;
        mipSwitch = 0;
        voxelizationSlice = 0;
        fastResolveSwitch = true;
        prevPropagateLight = propagateLight;
    }


    class PathCacheBuffer
    {
        int size;
        readonly int stride = sizeof(float) * 4;
        public ComputeBuffer front;
        public ComputeBuffer back;

        public void Init(int resolution)
        {
            size = resolution * resolution * resolution;

            if (front != null) front.Dispose();
            if (back != null) back.Dispose();
            front = new ComputeBuffer(size, stride, ComputeBufferType.Default);
            back = new ComputeBuffer(size, stride, ComputeBufferType.Default);
        }

        public void Cleanup()
        {
            if (front != null) front.Dispose();
            if (back != null) back.Dispose();
        }
    }

}