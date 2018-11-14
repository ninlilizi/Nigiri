using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class PVGI : MonoBehaviour {

	public enum DebugVoxelGrid {
		GRID_1,
		GRID_2,
		GRID_3,
		GRID_4,
		GRID_5
	};

    private struct LPVCascade
    {
        public RenderTexture lpvRedSH;
        public RenderTexture lpvGreenSH;
        public RenderTexture lpvBlueSH;
        public RenderTexture lpvLuminance;
        public RenderTexture lpvRedSHBackBuffer;
        public RenderTexture lpvGreenSHBackBuffer;
        public RenderTexture lpvBlueSHBackBuffer;
        public RenderTexture lpvLuminanceBackBuffer;
        public RenderTexture lpvRedPropagationBuffer;
        public RenderTexture lpvGreenPropagationBuffer;
        public RenderTexture lpvBluePropagationBuffer;
    };

    [Header("LPV")]
    public ComputeShader lpvCleanupShader = null;
    public ComputeShader lpvInjectionShader = null;
    public ComputeShader lpvPropagationShader = null;
    public ComputeShader lpvPropagationCompositionShader = null;
    public Shader lpvRenderShader = null;
    [Header("LPV Settings")]
    public LayerMask emissiveLayer;
    public bool backBuffering = true;
    public bool rsmVPLInjection = true;
    public bool screenSpaceVPLInjection = false;
    private Vector2Int screenSpaceVPLTextureResolution = new Vector2Int(128, 128);
    private readonly int lpvDimension = 8;
    public int propagationSteps = 14;
    public float firstCascadeBoundary = 50.0f;
    public float secondCascadeBoundary = 100.0f;
    public float thirdCascadeBoundary = 200.0f;

    private Material lpvRenderMaterial = null;

    private LPVCascade firstCascade;
    private LPVCascade secondCascade;
    private LPVCascade thirdCascade;

    private RenderTextureDescriptor lpvTextureDescriptorSH;
    private RenderTextureDescriptor lpvTextureDescriptorLuminance;

    private bool bDisplayBackBuffer = false;
    private int currentPropagationStep = 0;

    public int lpvStageSwitch;


    [Header("Debug Settings")]
	public bool debugMode = false;
	public DebugVoxelGrid debugVoxelGrid = DebugVoxelGrid.GRID_1;

	[Header("Shaders")]
	public Shader pvgiShader;
    public Shader emissiveShader;
    public Shader gaussianShader;
    public Shader lightMapShader;
    public Shader blurShader;
    public ComputeShader voxelGridEntryShader;

    [Header("General Settings")]
    public Vector2Int resolution = new Vector2Int(256, 256);
	public float indirectLightingStrength = 1.0f;

	[Header("Voxelization Settings")]
	public float worldVolumeBoundary = 100.0f;
	public int highestVoxelResolution = 256;
	public Vector2Int injectionTextureResolution = new Vector2Int(1280, 720);

	[Header("Cone Trace Settings")]
	public float maximumIterations = 8.0f;
    public float coneLength = 1;

	public RenderTexture lightingTexture;
	public RenderTexture positionTexture;
    public RenderTexture normalTexture;
    public RenderTexture lightMapTexture;
    public RenderTexture lpvTexture;
    public RenderTexture lpvBlendTexture;
    public RenderTexture lpvBounceTexture;
    private RenderTexture blur;
    public RenderTexture gi;

    private Material pvgiMaterial;
    private Material gaussianMaterial;
    private Material lightMapMaterial;
    private Material blurMaterial;

    private RenderTextureDescriptor voxelGridDescriptorFloat4;

	private RenderTexture voxelGrid1;
	private RenderTexture voxelGrid2;
	private RenderTexture voxelGrid3;
	private RenderTexture voxelGrid4;
	private RenderTexture voxelGrid5;

    public ComputeShader clearComputeCache;
    public ComputeShader transferIntsCompute;

    public Texture2D[] blueNoise;

    PathCacheBuffer pathCacheBuffer;
    public int tracedTexture1UpdateCount;

    private float lengthOfCone = 0.0f;

    int frameSwitch = 0;

    GameObject emissiveCameraGO;
    Camera emissiveCamera;

    // Use this for initialization
    void OnEnable () {

        clearComputeCache = Resources.Load("SEGIClear_Cache") as ComputeShader;
        transferIntsCompute = Resources.Load("SEGITransferInts_C") as ComputeShader;
        lpvCleanupShader = Resources.Load("LPVCleanupShader") as ComputeShader;
        lpvInjectionShader = Resources.Load("LPVInjectionShader") as ComputeShader;
        lpvPropagationShader = Resources.Load("LPVPropagationShader") as ComputeShader;
        lpvPropagationCompositionShader = Resources.Load("LPVPropagationCompositionShader") as ComputeShader;
        lpvRenderShader = Shader.Find("Hidden/LPVRenderShader");
        blurShader = Shader.Find("Hidden/BilateralBlur");
        emissiveShader = Shader.Find("NKGI/Emissive Glow");
        gaussianShader = Shader.Find("Hidden/SEGI Gaussian Blur Filter");
        gaussianMaterial = new Material(gaussianShader);
        blurMaterial = new Material(blurShader);

        voxelGridEntryShader = Resources.Load("VoxelGridEntry") as ComputeShader;

        Screen.SetResolution (resolution.x, resolution.y, true);

		GetComponent<Camera> ().depthTextureMode = DepthTextureMode.Depth | DepthTextureMode.DepthNormals;

		if (pvgiShader == null)
        {
            pvgiShader = Shader.Find("Hidden/PVGIShader");
		}
        pvgiMaterial = new Material(pvgiShader);

        if (lightMapShader == null)
        {
            lightMapShader = Shader.Find("Hidden/NKGI-Blit-lightMap");
        }
        lightMapMaterial = new Material(lightMapShader);

        if (lpvRenderShader != null)
        {
            lpvRenderMaterial = new Material(lpvRenderShader);
        }

        InitializeLPVTextures();

        InitializeVoxelGrid();

		lightingTexture = new RenderTexture (injectionTextureResolution.x, injectionTextureResolution.y, 0, RenderTextureFormat.ARGBHalf);
		positionTexture = new RenderTexture (injectionTextureResolution.x, injectionTextureResolution.y, 0, RenderTextureFormat.ARGBHalf);
        normalTexture = new RenderTexture(injectionTextureResolution.x, injectionTextureResolution.y, 0, RenderTextureFormat.ARGBHalf);
        lightMapTexture = new RenderTexture(injectionTextureResolution.x, injectionTextureResolution.y, 16, RenderTextureFormat.ARGBHalf);
        lpvTexture = new RenderTexture(injectionTextureResolution.x, injectionTextureResolution.y, 0, RenderTextureFormat.ARGBHalf);
        lpvBlendTexture = new RenderTexture(injectionTextureResolution.x, injectionTextureResolution.y, 0, RenderTextureFormat.ARGBHalf);
        lpvBounceTexture = new RenderTexture(injectionTextureResolution.x, injectionTextureResolution.y, 0, RenderTextureFormat.ARGBHalf);
        gi = new RenderTexture(injectionTextureResolution.x, injectionTextureResolution.y, 0, RenderTextureFormat.ARGBHalf);
        blur = new RenderTexture(injectionTextureResolution.x, injectionTextureResolution.y, 0, RenderTextureFormat.ARGBHalf);
        lightingTexture.filterMode = FilterMode.Bilinear;
        lpvTexture.filterMode = FilterMode.Bilinear;
        lpvBlendTexture.filterMode = FilterMode.Bilinear;
        lpvBounceTexture.filterMode = FilterMode.Bilinear;
        blur.filterMode = FilterMode.Bilinear;
        gi.filterMode = FilterMode.Bilinear;

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
            emissiveCamera = emissiveCameraGO.AddComponent<Camera>();
            emissiveCamera.hideFlags = HideFlags.DontSave;
            emissiveCamera.enabled = false;
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

		voxelGrid1.filterMode = FilterMode.Bilinear;
		voxelGrid2.filterMode = FilterMode.Bilinear;
		voxelGrid3.filterMode = FilterMode.Bilinear;
		voxelGrid4.filterMode = FilterMode.Bilinear;
		voxelGrid5.filterMode = FilterMode.Bilinear;

		voxelGrid1.Create ();
		voxelGrid2.Create ();
		voxelGrid3.Create ();
		voxelGrid4.Create ();
		voxelGrid5.Create ();

	}

	// Function to update data in the voxel grid
	private void UpdateVoxelGrid () {

		// Kernel index for the entry point in compute shader
		int kernelHandle = voxelGridEntryShader.FindKernel("CSMain");

        // Updating voxel grid 1
        voxelGridEntryShader.SetTexture(kernelHandle, "lightMapTexture", lightMapTexture);
        voxelGridEntryShader.SetTexture(kernelHandle, "NoiseTexture", blueNoise[frameSwitch % 64]);
        voxelGridEntryShader.SetTexture(kernelHandle, "voxelGrid", voxelGrid1);
		voxelGridEntryShader.SetInt("voxelResolution", highestVoxelResolution);
		voxelGridEntryShader.SetFloat ("worldVolumeBoundary", worldVolumeBoundary);
		voxelGridEntryShader.SetTexture(kernelHandle, "lightingTexture", lightingTexture);
		voxelGridEntryShader.SetTexture(kernelHandle, "positionTexture", positionTexture);

		voxelGridEntryShader.Dispatch(kernelHandle, injectionTextureResolution.x / 16, injectionTextureResolution.y / 16, 1);

		// Updating voxel grid 2
		voxelGridEntryShader.SetTexture(kernelHandle, "voxelGrid", voxelGrid2);
		voxelGridEntryShader.SetInt("voxelResolution", highestVoxelResolution / 2);
		voxelGridEntryShader.SetFloat ("worldVolumeBoundary", worldVolumeBoundary);
		voxelGridEntryShader.SetTexture(kernelHandle, "lightingTexture", lightingTexture);
		voxelGridEntryShader.SetTexture(kernelHandle, "positionTexture", positionTexture);

		voxelGridEntryShader.Dispatch(kernelHandle, injectionTextureResolution.x / 16, injectionTextureResolution.y / 16, 1);

		// Updating voxel grid 3
		voxelGridEntryShader.SetTexture(kernelHandle, "voxelGrid", voxelGrid3);
		voxelGridEntryShader.SetInt("voxelResolution", highestVoxelResolution / 4);
		voxelGridEntryShader.SetFloat ("worldVolumeBoundary", worldVolumeBoundary);
		voxelGridEntryShader.SetTexture(kernelHandle, "lightingTexture", lightingTexture);
		voxelGridEntryShader.SetTexture(kernelHandle, "positionTexture", positionTexture);

		voxelGridEntryShader.Dispatch(kernelHandle, injectionTextureResolution.x / 16, injectionTextureResolution.y / 16, 1);

		// Updating voxel grid 4
		voxelGridEntryShader.SetTexture(kernelHandle, "voxelGrid", voxelGrid4);
		voxelGridEntryShader.SetInt("voxelResolution", highestVoxelResolution / 8);
		voxelGridEntryShader.SetFloat ("worldVolumeBoundary", worldVolumeBoundary);
		voxelGridEntryShader.SetTexture(kernelHandle, "lightingTexture", lightingTexture);
		voxelGridEntryShader.SetTexture(kernelHandle, "positionTexture", positionTexture);

		voxelGridEntryShader.Dispatch(kernelHandle, injectionTextureResolution.x / 16, injectionTextureResolution.y / 16, 1);

		// Updating voxel grid 5
		voxelGridEntryShader.SetTexture(kernelHandle, "voxelGrid", voxelGrid5);
		voxelGridEntryShader.SetInt("voxelResolution", highestVoxelResolution / 16);
		voxelGridEntryShader.SetFloat ("worldVolumeBoundary", worldVolumeBoundary);
		voxelGridEntryShader.SetTexture(kernelHandle, "lightingTexture", lightingTexture);
		voxelGridEntryShader.SetTexture(kernelHandle, "positionTexture", positionTexture);

		voxelGridEntryShader.Dispatch(kernelHandle, injectionTextureResolution.x / 16, injectionTextureResolution.y / 16, 1);
	}

	// This is called once per frame after the scene is rendered
	void OnRenderImage (RenderTexture source, RenderTexture destination) {

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
		pvgiMaterial.SetFloat ("worldVolumeBoundary", worldVolumeBoundary);
		pvgiMaterial.SetFloat ("maximumIterations", maximumIterations);
		pvgiMaterial.SetFloat ("indirectLightingStrength", indirectLightingStrength);
		pvgiMaterial.SetFloat ("lengthOfCone", lengthOfCone);
		pvgiMaterial.SetInt ("highestVoxelResolution", highestVoxelResolution);
        pvgiMaterial.SetFloat("coneLength", coneLength);

		Graphics.Blit(source, lightingTexture);
		Graphics.Blit(source, positionTexture, pvgiMaterial, 0);
        Graphics.Blit(source, normalTexture, pvgiMaterial, 4);
        Graphics.Blit(source, lightMapTexture, lightMapMaterial, 0);

        UpdateVoxelGrid();

		pvgiMaterial.SetTexture("voxelGrid1", voxelGrid1);
		pvgiMaterial.SetTexture("voxelGrid2", voxelGrid2);
		pvgiMaterial.SetTexture("voxelGrid3", voxelGrid3);
		pvgiMaterial.SetTexture("voxelGrid4", voxelGrid4);
		pvgiMaterial.SetTexture("voxelGrid5", voxelGrid5);

		if (debugMode) {
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


            Shader.SetGlobalVector("Kernel", new Vector2(0.0f, 1.0f));
            Graphics.Blit(gi, blur, gaussianMaterial, 1);
            Shader.SetGlobalVector("Kernel", new Vector2(1.0f, 0.0f));
            Graphics.Blit(blur, gi, gaussianMaterial, 2);


            ///////////////////////////////// LPV


                if (backBuffering)
                {
                    if (lpvStageSwitch == 2)
                    {

                        ++currentPropagationStep;

                        if (currentPropagationStep >= propagationSteps)
                        {
                            currentPropagationStep = 0;
                            bDisplayBackBuffer = !bDisplayBackBuffer;
                            LPVGridCleanup(ref firstCascade);
                            LPVGridCleanup(ref secondCascade);
                            LPVGridCleanup(ref thirdCascade);
                        }
                    }

                }
                else
                {

                    currentPropagationStep = 0;
                    bDisplayBackBuffer = false;
                    if (lpvStageSwitch == 0) LPVGridCleanup(ref firstCascade);
                    if (lpvStageSwitch == 1) LPVGridCleanup(ref secondCascade);
                    if (lpvStageSwitch == 2) LPVGridCleanup(ref thirdCascade);
                }


            lpvRenderMaterial.SetMatrix("InverseViewMatrix", localCam.cameraToWorldMatrix);
            lpvRenderMaterial.SetMatrix("InverseProjectionMatrix", localCam.projectionMatrix.inverse);
            lpvRenderMaterial.SetFloat("firstCascadeBoundary", firstCascadeBoundary);
            lpvRenderMaterial.SetFloat("secondCascadeBoundary", secondCascadeBoundary);
            lpvRenderMaterial.SetFloat("thirdCascadeBoundary", thirdCascadeBoundary);
            lpvRenderMaterial.SetFloat("lpvDimension", lpvDimension);
            lpvRenderMaterial.SetVector("playerPosition", this.transform.position);

            emissiveCamera.CopyFrom(localCam);
            emissiveCamera.transform.SetPositionAndRotation(localCam.transform.position, localCam.transform.rotation);
            emissiveCamera.renderingPath = RenderingPath.DeferredShading;
            emissiveCamera.SetReplacementShader(emissiveShader, "");
            emissiveCamera.targetTexture = lightMapTexture;
            emissiveCamera.cullingMask = emissiveLayer;
            //emissiveCamera.Render();
            //Graphics.Blit(source, lightMapTexture, lightMapMaterial, 0);

            // bilateral blur at full res
            Graphics.Blit(lightMapTexture, blur, blurMaterial, 8);
            Graphics.Blit(blur, lightMapTexture, blurMaterial, 9);

            if (lpvStageSwitch == 0) LPVGridInjection(ref firstCascade, firstCascadeBoundary);
            if (lpvStageSwitch == 1) LPVGridInjection(ref secondCascade, secondCascadeBoundary);
            if (lpvStageSwitch == 2) LPVGridInjection(ref thirdCascade, thirdCascadeBoundary);

            if (backBuffering)
            {
                if (lpvStageSwitch == 0) LPVGridPropagation(ref firstCascade);
                if (lpvStageSwitch == 1) LPVGridPropagation(ref secondCascade);
                if (lpvStageSwitch == 2) LPVGridPropagation(ref thirdCascade);
            }
            else
            {

                for (int i = 0; i < propagationSteps; ++i)
                {
                    if (lpvStageSwitch == 0) LPVGridPropagation(ref firstCascade);
                    if (lpvStageSwitch == 1) LPVGridPropagation(ref secondCascade);
                    if (lpvStageSwitch == 2) LPVGridPropagation(ref thirdCascade);
                }

            }
            lpvStageSwitch = (lpvStageSwitch + 1) % (3);

            if (bDisplayBackBuffer)
            {
                lpvRenderMaterial.SetTexture("lpvRedSHFirstCascade", firstCascade.lpvRedSHBackBuffer);
                lpvRenderMaterial.SetTexture("lpvGreenSHFirstCascade", firstCascade.lpvGreenSHBackBuffer);
                lpvRenderMaterial.SetTexture("lpvBlueSHFirstCascade", firstCascade.lpvBlueSHBackBuffer);

                lpvRenderMaterial.SetTexture("lpvRedSHSecondCascade", secondCascade.lpvRedSHBackBuffer);
                lpvRenderMaterial.SetTexture("lpvGreenSHSecondCascade", secondCascade.lpvGreenSHBackBuffer);
                lpvRenderMaterial.SetTexture("lpvBlueSHSecondCascade", secondCascade.lpvBlueSHBackBuffer);

                lpvRenderMaterial.SetTexture("lpvRedSHThirdCascade", thirdCascade.lpvRedSHBackBuffer);
                lpvRenderMaterial.SetTexture("lpvGreenSHThirdCascade", thirdCascade.lpvGreenSHBackBuffer);
                lpvRenderMaterial.SetTexture("lpvBlueSHThirdCascade", thirdCascade.lpvBlueSHBackBuffer);
            }
            else
            {
                lpvRenderMaterial.SetTexture("lpvRedSHFirstCascade", firstCascade.lpvRedSH);
                lpvRenderMaterial.SetTexture("lpvGreenSHFirstCascade", firstCascade.lpvGreenSH);
                lpvRenderMaterial.SetTexture("lpvBlueSHFirstCascade", firstCascade.lpvBlueSH);

                lpvRenderMaterial.SetTexture("lpvRedSHSecondCascade", secondCascade.lpvRedSH);
                lpvRenderMaterial.SetTexture("lpvGreenSHSecondCascade", secondCascade.lpvGreenSH);
                lpvRenderMaterial.SetTexture("lpvBlueSHSecondCascade", secondCascade.lpvBlueSH);

                lpvRenderMaterial.SetTexture("lpvRedSHThirdCascade", thirdCascade.lpvRedSH);
                lpvRenderMaterial.SetTexture("lpvGreenSHThirdCascade", thirdCascade.lpvGreenSH);
                lpvRenderMaterial.SetTexture("lpvBlueSHThirdCascade", thirdCascade.lpvBlueSH);
            }
            ///////////////////////////////// LPV

            // bilateral blur at full res
            //Graphics.Blit(gi, blur, blurMaterial, 0);
            //Graphics.Blit(blur, gi, blurMaterial, 1);

            Shader.SetGlobalTexture("gi", gi);
            Shader.SetGlobalTexture("lpv", lpvTexture);
            Graphics.Blit(source, lpvTexture, lpvRenderMaterial, 2);

            lpvRenderMaterial.SetTexture("lpvBlendTexture", lpvBlendTexture);
            Graphics.Blit(lpvTexture, lpvBlendTexture, lpvRenderMaterial, 2);

            // bilateral blur at full res
            //Graphics.Blit(lpvTexture, blur, blurMaterial, 0);
            //Graphics.Blit(blur, lpvTexture, blurMaterial, 1);

            Shader.SetGlobalTexture("gi", gi);
            Shader.SetGlobalTexture("lpv", lpvTexture);
            Graphics.Blit(source, destination, pvgiMaterial, 3);




            //Debug line... bad for performance.
            //Graphics.Blit(source, lpvTexture, lpvRenderMaterial, 2);

            //Advance the frame counter
            frameSwitch = (frameSwitch + 1) % (64);
        }

	}

    private void OnDisable()
    {
        if (pathCacheBuffer != null) pathCacheBuffer.Cleanup();
        if (lightingTexture != null) lightingTexture.Release();
        if (positionTexture != null) positionTexture.Release();
        if (normalTexture != null) normalTexture.Release();
        if (lightMapTexture != null) lightMapTexture.Release();
        if (lpvTexture != null) lpvTexture.Release();
        if (lpvBlendTexture != null) lpvBlendTexture.Release();
        if (lpvBounceTexture != null) lpvBounceTexture.Release();
        if (gi != null) gi.Release();
        if (blur != null) blur.Release();

        if (emissiveCameraGO != null) GameObject.DestroyImmediate(emissiveCameraGO);
    }

    // Function to inject the vpl data into LPV grid as spherical harmonics
    private void LPVGridInjection(ref LPVCascade cascade, float cascadeBoundary)
    {

        int kernelHandle = lpvInjectionShader.FindKernel("CSMain");

        if (rsmVPLInjection)
        {
            if (backBuffering)
            {
                if (bDisplayBackBuffer)
                {
                    lpvInjectionShader.SetTexture(kernelHandle, "lpvRedSH", cascade.lpvRedSH);
                    lpvInjectionShader.SetTexture(kernelHandle, "lpvGreenSH", cascade.lpvGreenSH);
                    lpvInjectionShader.SetTexture(kernelHandle, "lpvBlueSH", cascade.lpvBlueSH);
                    lpvInjectionShader.SetTexture(kernelHandle, "lpvLuminance", cascade.lpvLuminance);
                }
                else
                {
                    lpvInjectionShader.SetTexture(kernelHandle, "lpvRedSH", cascade.lpvRedSHBackBuffer);
                    lpvInjectionShader.SetTexture(kernelHandle, "lpvGreenSH", cascade.lpvGreenSHBackBuffer);
                    lpvInjectionShader.SetTexture(kernelHandle, "lpvBlueSH", cascade.lpvBlueSHBackBuffer);
                    lpvInjectionShader.SetTexture(kernelHandle, "lpvLuminance", cascade.lpvLuminanceBackBuffer);
                }
            }
            else
            {
                lpvInjectionShader.SetTexture(kernelHandle, "lpvRedSH", cascade.lpvRedSH);
                lpvInjectionShader.SetTexture(kernelHandle, "lpvGreenSH", cascade.lpvGreenSH);
                lpvInjectionShader.SetTexture(kernelHandle, "lpvBlueSH", cascade.lpvBlueSH);
                lpvInjectionShader.SetTexture(kernelHandle, "lpvLuminance", cascade.lpvLuminance);
            }

            lpvInjectionShader.SetInt("lpvDimension", lpvDimension);
            lpvInjectionShader.SetFloat("worldVolumeBoundary", cascadeBoundary);
            lpvInjectionShader.SetTexture(kernelHandle, "lightingTexture", lightMapTexture);
            lpvInjectionShader.SetTexture(kernelHandle, "positionTexture", positionTexture);
            lpvInjectionShader.SetTexture(kernelHandle, "normalTexture", normalTexture);
            lpvInjectionShader.Dispatch(kernelHandle, lightMapTexture.width, lightMapTexture.height, 1);


        }

        if (screenSpaceVPLInjection)
        {

            // Screen textures injection
            // RSM textures injection
            if (backBuffering)
            {
                if (bDisplayBackBuffer)
                {
                    lpvInjectionShader.SetTexture(kernelHandle, "lpvRedSH", cascade.lpvRedSH);
                    lpvInjectionShader.SetTexture(kernelHandle, "lpvGreenSH", cascade.lpvGreenSH);
                    lpvInjectionShader.SetTexture(kernelHandle, "lpvBlueSH", cascade.lpvBlueSH);
                    lpvInjectionShader.SetTexture(kernelHandle, "lpvLuminance", cascade.lpvLuminance);
                }
                else
                {
                    lpvInjectionShader.SetTexture(kernelHandle, "lpvRedSH", cascade.lpvRedSHBackBuffer);
                    lpvInjectionShader.SetTexture(kernelHandle, "lpvGreenSH", cascade.lpvGreenSHBackBuffer);
                    lpvInjectionShader.SetTexture(kernelHandle, "lpvBlueSH", cascade.lpvBlueSHBackBuffer);
                    lpvInjectionShader.SetTexture(kernelHandle, "lpvLuminance", cascade.lpvLuminanceBackBuffer);
                }
            }
            else
            {
                lpvInjectionShader.SetTexture(kernelHandle, "lpvRedSH", cascade.lpvRedSH);
                lpvInjectionShader.SetTexture(kernelHandle, "lpvGreenSH", cascade.lpvGreenSH);
                lpvInjectionShader.SetTexture(kernelHandle, "lpvBlueSH", cascade.lpvBlueSH);
                lpvInjectionShader.SetTexture(kernelHandle, "lpvLuminance", cascade.lpvLuminance);
            }

            lpvInjectionShader.SetInt("lpvDimension", lpvDimension);
            lpvInjectionShader.SetFloat("cascadeBoundary", cascadeBoundary);
            lpvInjectionShader.SetTexture(kernelHandle, "lightingTexture", lightMapTexture);
            lpvInjectionShader.SetTexture(kernelHandle, "positionTexture", positionTexture);
            lpvInjectionShader.SetTexture(kernelHandle, "normalTexture", normalTexture);
            lpvInjectionShader.Dispatch(kernelHandle, injectionTextureResolution.x, injectionTextureResolution.y, 1);

        }

    }

    // Function to propagate the lighting stored as spherical harmonics in the LPV grid to its neightbouring cells
    private void LPVGridPropagation(ref LPVCascade cascade)
    {

        int kernelHandle = lpvPropagationShader.FindKernel("CSMain");

        lpvPropagationShader.SetTexture(kernelHandle, "lpvRedSHOutput", cascade.lpvRedPropagationBuffer);
        lpvPropagationShader.SetTexture(kernelHandle, "lpvGreenSHOutput", cascade.lpvGreenPropagationBuffer);
        lpvPropagationShader.SetTexture(kernelHandle, "lpvBlueSHOutput", cascade.lpvBluePropagationBuffer);

        if (backBuffering)
        {
            if (bDisplayBackBuffer)
            {
                lpvPropagationShader.SetTexture(kernelHandle, "lpvRedSHInput", cascade.lpvRedSH);
                lpvPropagationShader.SetTexture(kernelHandle, "lpvGreenSHInput", cascade.lpvGreenSH);
                lpvPropagationShader.SetTexture(kernelHandle, "lpvBlueSHInput", cascade.lpvBlueSH);
            }
            else
            {
                lpvPropagationShader.SetTexture(kernelHandle, "lpvRedSHInput", cascade.lpvRedSHBackBuffer);
                lpvPropagationShader.SetTexture(kernelHandle, "lpvGreenSHInput", cascade.lpvGreenSHBackBuffer);
                lpvPropagationShader.SetTexture(kernelHandle, "lpvBlueSHInput", cascade.lpvBlueSHBackBuffer);
            }
        }
        else
        {
            lpvPropagationShader.SetTexture(kernelHandle, "lpvRedSHInput", cascade.lpvRedSH);
            lpvPropagationShader.SetTexture(kernelHandle, "lpvGreenSHInput", cascade.lpvGreenSH);
            lpvPropagationShader.SetTexture(kernelHandle, "lpvBlueSHInput", cascade.lpvBlueSH);
        }

        lpvPropagationShader.SetInt("lpvDimension", lpvDimension);
        lpvPropagationShader.Dispatch(kernelHandle, lpvDimension, lpvDimension, lpvDimension);

        kernelHandle = lpvPropagationCompositionShader.FindKernel("CSMain");

        lpvPropagationCompositionShader.SetTexture(kernelHandle, "lpvRedSHInput", cascade.lpvRedPropagationBuffer);
        lpvPropagationCompositionShader.SetTexture(kernelHandle, "lpvGreenSHInput", cascade.lpvGreenPropagationBuffer);
        lpvPropagationCompositionShader.SetTexture(kernelHandle, "lpvBlueSHInput", cascade.lpvBluePropagationBuffer);

        if (backBuffering)
        {
            if (bDisplayBackBuffer)
            {
                lpvPropagationCompositionShader.SetTexture(kernelHandle, "lpvRedSHOutput", cascade.lpvRedSH);
                lpvPropagationCompositionShader.SetTexture(kernelHandle, "lpvGreenSHOutput", cascade.lpvGreenSH);
                lpvPropagationCompositionShader.SetTexture(kernelHandle, "lpvBlueSHOutput", cascade.lpvBlueSH);
            }
            else
            {
                lpvPropagationCompositionShader.SetTexture(kernelHandle, "lpvRedSHOutput", cascade.lpvRedSHBackBuffer);
                lpvPropagationCompositionShader.SetTexture(kernelHandle, "lpvGreenSHOutput", cascade.lpvGreenSHBackBuffer);
                lpvPropagationCompositionShader.SetTexture(kernelHandle, "lpvBlueSHOutput", cascade.lpvBlueSHBackBuffer);
            }
        }
        else
        {
            lpvPropagationCompositionShader.SetTexture(kernelHandle, "lpvRedSHOutput", cascade.lpvRedSH);
            lpvPropagationCompositionShader.SetTexture(kernelHandle, "lpvGreenSHOutput", cascade.lpvGreenSH);
            lpvPropagationCompositionShader.SetTexture(kernelHandle, "lpvBlueSHOutput", cascade.lpvBlueSH);
        }

        lpvPropagationCompositionShader.Dispatch(kernelHandle, lpvDimension, lpvDimension, lpvDimension);

    }

    // Function to cleanup all the data stored in the LPV grid
    private void LPVGridCleanup(ref LPVCascade cascade)
    {

        int kernelHandle = lpvCleanupShader.FindKernel("CSMain");

        if (backBuffering)
        {
            if (bDisplayBackBuffer)
            {
                lpvCleanupShader.SetTexture(kernelHandle, "lpvRedSH", cascade.lpvRedSH);
                lpvCleanupShader.SetTexture(kernelHandle, "lpvGreenSH", cascade.lpvGreenSH);
                lpvCleanupShader.SetTexture(kernelHandle, "lpvBlueSH", cascade.lpvBlueSH);
                lpvCleanupShader.SetTexture(kernelHandle, "lpvLuminance", cascade.lpvLuminance);
            }
            else
            {
                lpvCleanupShader.SetTexture(kernelHandle, "lpvRedSH", cascade.lpvRedSHBackBuffer);
                lpvCleanupShader.SetTexture(kernelHandle, "lpvGreenSH", cascade.lpvGreenSHBackBuffer);
                lpvCleanupShader.SetTexture(kernelHandle, "lpvBlueSH", cascade.lpvBlueSHBackBuffer);
                lpvCleanupShader.SetTexture(kernelHandle, "lpvLuminance", cascade.lpvLuminanceBackBuffer);
            }
        }
        else
        {
            lpvCleanupShader.SetTexture(kernelHandle, "lpvRedSH", cascade.lpvRedSH);
            lpvCleanupShader.SetTexture(kernelHandle, "lpvGreenSH", cascade.lpvGreenSH);
            lpvCleanupShader.SetTexture(kernelHandle, "lpvBlueSH", cascade.lpvBlueSH);
            lpvCleanupShader.SetTexture(kernelHandle, "lpvLuminance", cascade.lpvLuminance);
        }

        lpvCleanupShader.Dispatch(kernelHandle, lpvDimension, lpvDimension, lpvDimension);

    }

    // Function to create the 3D LPV Textures
    private void InitializeLPVTextures()
    {

        lpvTextureDescriptorSH = new RenderTextureDescriptor();
        lpvTextureDescriptorSH.bindMS = false;
        lpvTextureDescriptorSH.colorFormat = RenderTextureFormat.ARGBFloat;
        lpvTextureDescriptorSH.depthBufferBits = 0;
        lpvTextureDescriptorSH.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
        lpvTextureDescriptorSH.enableRandomWrite = true;
        lpvTextureDescriptorSH.height = lpvDimension;
        lpvTextureDescriptorSH.msaaSamples = 1;
        lpvTextureDescriptorSH.volumeDepth = lpvDimension;
        lpvTextureDescriptorSH.width = lpvDimension;
        lpvTextureDescriptorSH.sRGB = true;

        lpvTextureDescriptorLuminance = new RenderTextureDescriptor();
        lpvTextureDescriptorLuminance.bindMS = false;
        lpvTextureDescriptorLuminance.colorFormat = RenderTextureFormat.RFloat;
        lpvTextureDescriptorLuminance.depthBufferBits = 0;
        lpvTextureDescriptorLuminance.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
        lpvTextureDescriptorLuminance.enableRandomWrite = true;
        lpvTextureDescriptorLuminance.height = lpvDimension;
        lpvTextureDescriptorLuminance.msaaSamples = 1;
        lpvTextureDescriptorLuminance.volumeDepth = lpvDimension;
        lpvTextureDescriptorLuminance.width = lpvDimension;
        lpvTextureDescriptorLuminance.sRGB = true;

        InitializeLPVCascade(ref firstCascade);
        InitializeLPVCascade(ref secondCascade);
        InitializeLPVCascade(ref thirdCascade);

        LPVGridCleanup(ref firstCascade);
        LPVGridCleanup(ref secondCascade);
        LPVGridCleanup(ref thirdCascade);
    }

    // Function to initialize an LPV cascade
    private void InitializeLPVCascade(ref LPVCascade cascade)
    {

        cascade.lpvRedSH = new RenderTexture(lpvTextureDescriptorSH);
        cascade.lpvGreenSH = new RenderTexture(lpvTextureDescriptorSH);
        cascade.lpvBlueSH = new RenderTexture(lpvTextureDescriptorSH);
        cascade.lpvLuminance = new RenderTexture(lpvTextureDescriptorLuminance);

        cascade.lpvRedPropagationBuffer = new RenderTexture(lpvTextureDescriptorSH);
        cascade.lpvGreenPropagationBuffer = new RenderTexture(lpvTextureDescriptorSH);
        cascade.lpvBluePropagationBuffer = new RenderTexture(lpvTextureDescriptorSH);

        cascade.lpvRedSHBackBuffer = new RenderTexture(lpvTextureDescriptorSH);
        cascade.lpvGreenSHBackBuffer = new RenderTexture(lpvTextureDescriptorSH);
        cascade.lpvBlueSHBackBuffer = new RenderTexture(lpvTextureDescriptorSH);
        cascade.lpvLuminanceBackBuffer = new RenderTexture(lpvTextureDescriptorLuminance);

        cascade.lpvRedSH.filterMode = FilterMode.Trilinear;
        cascade.lpvGreenSH.filterMode = FilterMode.Trilinear;
        cascade.lpvBlueSH.filterMode = FilterMode.Trilinear;
        cascade.lpvLuminance.filterMode = FilterMode.Trilinear;

        cascade.lpvRedPropagationBuffer.filterMode = FilterMode.Trilinear;
        cascade.lpvGreenPropagationBuffer.filterMode = FilterMode.Trilinear;
        cascade.lpvBluePropagationBuffer.filterMode = FilterMode.Trilinear;

        cascade.lpvRedSHBackBuffer.filterMode = FilterMode.Trilinear;
        cascade.lpvGreenSHBackBuffer.filterMode = FilterMode.Trilinear;
        cascade.lpvBlueSHBackBuffer.filterMode = FilterMode.Trilinear;
        cascade.lpvLuminanceBackBuffer.filterMode = FilterMode.Trilinear;

        cascade.lpvRedSH.Create();
        cascade.lpvGreenSH.Create();
        cascade.lpvBlueSH.Create();
        cascade.lpvLuminance.Create();

        cascade.lpvRedPropagationBuffer.Create();
        cascade.lpvGreenPropagationBuffer.Create();
        cascade.lpvBluePropagationBuffer.Create();

        cascade.lpvRedSHBackBuffer.Create();
        cascade.lpvGreenSHBackBuffer.Create();
        cascade.lpvBlueSHBackBuffer.Create();
        cascade.lpvLuminanceBackBuffer.Create();

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