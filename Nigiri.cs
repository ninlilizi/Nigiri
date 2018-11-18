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
    [Range(0.01f, 8)]
    public float AmbientAttribution = 1.0f;
    [Range(0, 32)]
    public float BouncedAttribution = 1.0f;

	[Header("Voxelization Settings")]
    public LayerMask emissiveLayer;
    public float worldVolumeBoundary = 100.0f;
	public int highestVoxelResolution = 256;
	public Vector2Int injectionTextureResolution = new Vector2Int(1280, 720);

	[Header("Cone Trace Settings")]
    [Range(1, 64)]
	public int maximumIterations = 8;
    [Range(0.01f, 2)]
    public float coneLength = 0.1f;
    [Range(0.01f, 12)]
    public float coneWidth = 6;
    private bool stochasticSampling = true;

    [Header("Debug Settings")]
    public bool VisualiseGI = false;
    public bool VisualizeVoxels = false;
    public DebugVoxelGrid debugVoxelGrid = DebugVoxelGrid.GRID_1;

    public Texture2D[] blueNoise;

    //[Header("Shaders")]
    private Shader tracingShader;
    private Shader blitGBufferShader;
    private Shader fxaaShader;
    private ComputeShader nigiri_VoxelEntry;
    private ComputeShader nigiri_InjectionCompute;
    private ComputeShader clearComputeCache;
    private ComputeShader transferIntsCompute;

    //[Header("Materials")]
    private Material pvgiMaterial;
    private Material blitGBufferMaterial;
    private Material fxaaMaterial;

    [Header("Render Textures")]

    private RenderTextureDescriptor voxelGridDescriptorFloat4;

    public static RenderTexture voxelInjectionGrid;
    public static RenderTexture voxelGrid1;
    public static RenderTexture voxelGrid2;
    public static RenderTexture voxelGrid3;
    public static RenderTexture voxelGrid4;
    public static RenderTexture voxelGrid5;

    private RenderTexture lightingTexture;
    private RenderTexture lightingTexture2;
    public RenderTexture positionTexture;

    private RenderTexture blur;
    public RenderTexture gi;


    private PathCacheBuffer pathCacheBuffer;
    private ComputeBuffer voxelUpdateCounter;
    private int tracedTexture1UpdateCount;

    private float lengthOfCone = 0.0f;

    int frameSwitch = 0;
    int emmisiveSlice = 0;

    GameObject emissiveCameraGO;
    Camera emissiveCamera;

    // Use this for initialization
    void OnEnable () {

        clearComputeCache = Resources.Load("SEGIClear_Cache") as ComputeShader;
        transferIntsCompute = Resources.Load("SEGITransferInts_C") as ComputeShader;
        fxaaShader = Shader.Find("Hidden/Nigiri_BilateralBlur");
        blitGBufferShader = Shader.Find("Hidden/Nigiri_Blit_gBuffer0");
        fxaaShader = Shader.Find("Hidden/Nigiri_FXAA");
        fxaaMaterial = new Material(fxaaShader);

        nigiri_VoxelEntry = Resources.Load("nigiri_VoxelEntry") as ComputeShader;
        nigiri_InjectionCompute = Resources.Load("Nigiri_Injection") as ComputeShader;

        Screen.SetResolution(resolution.x, resolution.y, true);

		GetComponent<Camera>().depthTextureMode = DepthTextureMode.Depth | DepthTextureMode.DepthNormals;

		if (tracingShader == null)  tracingShader = Shader.Find("Hidden/Nigiri_Tracing");
        pvgiMaterial = new Material(tracingShader);

        if (blitGBufferMaterial == null) blitGBufferMaterial = new Material(blitGBufferShader);

        InitializeVoxelGrid();

		lightingTexture = new RenderTexture (injectionTextureResolution.x, injectionTextureResolution.y, 32, RenderTextureFormat.ARGBHalf);
        lightingTexture2 = new RenderTexture(injectionTextureResolution.x, injectionTextureResolution.y, 32, RenderTextureFormat.ARGBHalf);
        positionTexture = new RenderTexture(injectionTextureResolution.x, injectionTextureResolution.y, 32, RenderTextureFormat.ARGBFloat);
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
            emissiveCameraGO.transform.localEulerAngles = new Vector3(90, 0, 0);
            emissiveCameraGO.hideFlags = HideFlags.DontSave;
            emissiveCamera = emissiveCameraGO.AddComponent<Camera>();
            emissiveCamera.CopyFrom(GetComponent<Camera>());
            emissiveCameraGO.AddComponent<Nigiri_EmissiveCameraHelper>();
            emissiveCameraGO.transform.localPosition = new Vector3(0, 0, 0);
            emissiveCamera.orthographicSize = (int)(worldVolumeBoundary * 0.125);
            emissiveCamera.farClipPlane = (int)(worldVolumeBoundary * 0.5);
            emissiveCamera.enabled = false;
            Nigiri_EmissiveCameraHelper.injectionResolution = injectionTextureResolution;
            
        }
    }

	// Function to initialize the voxel grid data
	private void InitializeVoxelGrid() {

		voxelGridDescriptorFloat4 = new RenderTextureDescriptor ();
		voxelGridDescriptorFloat4.bindMS = false;
		voxelGridDescriptorFloat4.colorFormat = RenderTextureFormat.ARGBFloat;
		voxelGridDescriptorFloat4.depthBufferBits = 0;
		voxelGridDescriptorFloat4.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
		voxelGridDescriptorFloat4.enableRandomWrite = true;
		voxelGridDescriptorFloat4.width = highestVoxelResolution;
		voxelGridDescriptorFloat4.height = highestVoxelResolution;
		voxelGridDescriptorFloat4.volumeDepth = highestVoxelResolution;
		voxelGridDescriptorFloat4.msaaSamples = 1;
		voxelGridDescriptorFloat4.sRGB = true;

        voxelInjectionGrid = new RenderTexture(voxelGridDescriptorFloat4);

        voxelGrid1 = new RenderTexture (voxelGridDescriptorFloat4);

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

        voxelGrid1.filterMode = FilterMode.Bilinear;
		voxelGrid2.filterMode = FilterMode.Bilinear;
		voxelGrid3.filterMode = FilterMode.Bilinear;
		voxelGrid4.filterMode = FilterMode.Bilinear;
		voxelGrid5.filterMode = FilterMode.Bilinear;

        voxelInjectionGrid.Create();

        voxelGrid1.Create ();
		voxelGrid2.Create ();
		voxelGrid3.Create ();
		voxelGrid4.Create ();
		voxelGrid5.Create ();

	}

	// Function to update data in the voxel grid
	private void UpdateVoxelGrid ()
    {
        Nigiri_EmissiveCameraHelper.DoRender();

        if (Nigiri_EmissiveCameraHelper.lightMapBuffer == null) return;


        
        //int sliceOffset = (emmisiveSlice + 1) * 4096;



        // Kernel index for the entry point in compute shader
        int kernelHandle = nigiri_InjectionCompute.FindKernel("CSMain");

        
        nigiri_InjectionCompute.SetBuffer(0, "lightMapBuffer", Nigiri_EmissiveCameraHelper.lightMapBuffer);
        nigiri_InjectionCompute.SetTexture(0, "voxelGrid", voxelInjectionGrid);
        nigiri_InjectionCompute.SetInt("offsetStart", emmisiveSlice * 1048576);
        nigiri_InjectionCompute.SetFloat ("worldVolumeBoundary", worldVolumeBoundary);
        nigiri_InjectionCompute.Dispatch(0, 4096, 1, 1);
        emmisiveSlice = (emmisiveSlice + 1) % (15);

        // These apply to all grids
        nigiri_VoxelEntry.SetMatrix("InverseViewMatrix", GetComponent<Camera>().cameraToWorldMatrix);
        nigiri_VoxelEntry.SetMatrix("InverseProjectionMatrix", GetComponent<Camera>().projectionMatrix.inverse);
        nigiri_VoxelEntry.SetBuffer(kernelHandle, "voxelUpdateCounter", voxelUpdateCounter);
        nigiri_VoxelEntry.SetTexture(kernelHandle, "lightingTexture", lightingTexture);
        nigiri_VoxelEntry.SetTexture(kernelHandle, "lightingTexture2", lightingTexture2);
        nigiri_VoxelEntry.SetTexture(kernelHandle, "positionTexture", positionTexture);
        nigiri_VoxelEntry.SetTexture(kernelHandle, "voxelInjectionGrid", voxelInjectionGrid);
        nigiri_VoxelEntry.SetInt("injectionTextureResolutionX", injectionTextureResolution.x);

        //Updating voxel grid 1
        nigiri_VoxelEntry.SetTexture(kernelHandle, "voxelGrid", voxelGrid1);
        nigiri_VoxelEntry.SetInt("voxelResolution", highestVoxelResolution);
        nigiri_VoxelEntry.SetFloat("worldVolumeBoundary", worldVolumeBoundary);
        nigiri_VoxelEntry.Dispatch(kernelHandle, injectionTextureResolution.x / 16, injectionTextureResolution.y / 16, 1);

        //Updating voxel grid 2
        nigiri_VoxelEntry.SetTexture(kernelHandle, "voxelGrid", voxelGrid2);
        nigiri_VoxelEntry.SetInt("voxelResolution", highestVoxelResolution / 2);
        nigiri_VoxelEntry.SetFloat ("worldVolumeBoundary", worldVolumeBoundary);
        nigiri_VoxelEntry.Dispatch(kernelHandle, injectionTextureResolution.x / 16, injectionTextureResolution.y / 16, 1);

        // Updating voxel grid 3
        nigiri_VoxelEntry.SetTexture(kernelHandle, "voxelGrid", voxelGrid3);
        nigiri_VoxelEntry.SetInt("voxelResolution", highestVoxelResolution / 4);
        nigiri_VoxelEntry.SetFloat ("worldVolumeBoundary", worldVolumeBoundary);
        nigiri_VoxelEntry.Dispatch(kernelHandle, injectionTextureResolution.x / 16, injectionTextureResolution.y / 16, 1);

        // Updating voxel grid 4
        nigiri_VoxelEntry.SetTexture(kernelHandle, "voxelGrid", voxelGrid4);
        nigiri_VoxelEntry.SetInt("voxelResolution", highestVoxelResolution / 8);
        nigiri_VoxelEntry.SetFloat ("worldVolumeBoundary", worldVolumeBoundary);
        nigiri_VoxelEntry.Dispatch(kernelHandle, injectionTextureResolution.x / 16, injectionTextureResolution.y / 16, 1);

        // Updating voxel grid 5
        nigiri_VoxelEntry.SetTexture(kernelHandle, "voxelGrid", voxelGrid5);
        nigiri_VoxelEntry.SetInt("voxelResolution", highestVoxelResolution / 16);
        nigiri_VoxelEntry.SetFloat ("worldVolumeBoundary", worldVolumeBoundary);
        nigiri_VoxelEntry.Dispatch(kernelHandle, injectionTextureResolution.x / 16, injectionTextureResolution.y / 16, 1);
    }

	// This is called once per frame after the scene is rendered
	void OnRenderImage (RenderTexture source, RenderTexture destination)
    {

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


        lengthOfCone = (32.0f * worldVolumeBoundary) / (highestVoxelResolution * Mathf.Tan (Mathf.PI / 6.0f));

        pvgiMaterial.SetMatrix ("InverseViewMatrix", GetComponent<Camera>().cameraToWorldMatrix);
		pvgiMaterial.SetMatrix ("InverseProjectionMatrix", GetComponent<Camera>().projectionMatrix.inverse);
		Shader.SetGlobalFloat("worldVolumeBoundary", worldVolumeBoundary);
		pvgiMaterial.SetFloat ("maximumIterations", maximumIterations);
		pvgiMaterial.SetFloat ("indirectLightingStrength", AmbientAttribution);
        pvgiMaterial.SetFloat("SunlightInjection", BouncedAttribution);
        pvgiMaterial.SetFloat ("lengthOfCone", lengthOfCone);
        pvgiMaterial.SetFloat("coneLength", coneLength);
        pvgiMaterial.SetFloat("coneWidth", coneWidth);
        Shader.SetGlobalInt("highestVoxelResolution", highestVoxelResolution);
        pvgiMaterial.SetInt("StochasticSampling", stochasticSampling ? 1 : 0);
        pvgiMaterial.SetInt("VisualiseGI", VisualiseGI ? 1 : 0);
        pvgiMaterial.SetFloat("coneLength", coneLength);

        Graphics.Blit(source, lightingTexture);
        Graphics.Blit(null, lightingTexture2, blitGBufferMaterial);
        Graphics.Blit(source, positionTexture, pvgiMaterial, 0);

        // Configure emissive camera
        emissiveCamera.cullingMask = emissiveLayer;
        emissiveCameraGO.transform.localPosition = new Vector3(0, 0, -(int)(emissiveCamera.farClipPlane * 0.5));

        UpdateVoxelGrid();

        pvgiMaterial.SetTexture("voxelGrid1", voxelGrid1);
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
        if (gi != null) gi.Release();
        if (blur != null) blur.Release();

        if (voxelUpdateCounter != null) voxelUpdateCounter.Release();
        if (emissiveCameraGO != null) GameObject.DestroyImmediate(emissiveCameraGO);

        if (voxelInjectionGrid != null) voxelInjectionGrid.Release();

        if (voxelGrid1 != null) voxelGrid1.Release();
        if (voxelGrid2 != null) voxelGrid2.Release();
        if (voxelGrid3 != null) voxelGrid3.Release();
        if (voxelGrid4 != null) voxelGrid4.Release();
        if (voxelGrid5 != null) voxelGrid5.Release();
    }

    public void UpdateForceGI()
    {
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