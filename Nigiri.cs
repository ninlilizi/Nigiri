using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR;

[ExecuteInEditMode]
[RequireComponent(typeof(Camera))]
public class Nigiri : MonoBehaviour {

    public enum DebugVoxelGrid {
        GRID_1,
        GRID_2,
        GRID_3,
        GRID_4,
        GRID_5
    };

    [Header("General Settings")]
    [Range(0.01f, 8)]
    public float indirectLightingStrength = 1.0f;
    [Range(0.0f, 8)]
    public float EmissiveIntensity = 1.0f;

    [Header("Voxelization Settings")]
    public LayerMask dynamicPlusEmissiveLayer;

    public float GIAreaSize = 50;
    public int highestVoxelResolution = 256;
    [Range(0, 1)]
    public float shadowStrength = 1.0f;
    //[Range(0.0f, 0.999f)]
    //public float temporalStablityVsRefreshRate = 0.975f;
    [Tooltip("A higher speed, but lower quality light propagation")]
    public bool neighbourPropagation = false;
    public bool gaussianMipFiltering = true;
    public bool bilinearFiltering = true;
    public bool primaryVoxelization = true;
    public bool secondaryVoxelization = true;

    [Header("Cone Trace Settings")]
    [Range(1, 16)]
    public int subsamplingRatio = 1;
    [Range(1, 32)]
    public int maximumIterations = 8;
    [Range(0.1f, 4)]
    public float GIGain = 1;
    [Range(0.1f, 4)]
    public float NearLightGain = 1.14f;
    [Range(0.01f, 2)]
    public float coneTraceBias = 1;
    public bool depthStopOptimization = true;
    [Tooltip("Searches nearest neighbours of ray hit for highest value")]
    public bool neighbourSearch = false;
    [Tooltip("Chooses the miplevel with the highest value")]
    public bool mipLevelSearch = false;
    public bool skipFirstMipLevel = false;
    public bool skipLastMipLevel = false;
    public bool stochasticSampling = true;
    [Range(0.1f, 2)]
    public float stochasticFactor = 1;

    [Header("Environment Settings")]
    public bool matchSunColor = true;
    public bool matchSkyColor = true;
    public Color sunColor;
    public Color skyColor;
    public Light sunLight;
    [Range(0, 8)]
    private float sunLightInjection = 1.0f;
    public bool sphericalSunlight = false;

    [Header("Reflection Settings")]
    public bool traceReflections = true;
    //public int downsample = 2;
    [Range(0.1f, 0.25f)]
    public float rayOffset = 0.1f;
    [Range(0.01f, 1.0f)]
    public float rayStep = 0.25f;
    [Range(0.01f, 4)]
    public float reflectionGain = 1;
    [Range(1, 32)]
    public int reflectionSteps = 16;
    [Range(0, 1)]
    public float skyReflectionIntensity;

    [Header("Occlusion Settings")]
    [SerializeField, Range(1, 10)] float _thicknessModifier = 1;

    public float thicknessModifier
    {
        get { return _thicknessModifier; }
        set { _thicknessModifier = value; }
    }

    [SerializeField, Range(0, 2)] float _intensity = 1;

    public float intensity
    {
        get { return _intensity; }
        set { _intensity = value; }
    }

    [Range(0.1f, 2)]
    public float occlusionGain = 0.9f;
    public bool _ambientOnly = false;


    [Header("Color Settings")]


    // White balance.
    [SerializeField, Range(-1, 1)]
    float _colorTemp = 0.0f;
    [SerializeField, Range(-1, 1)] float _colorTint = 0.0f;

    public float colorTemp
    {
        get { return _colorTemp; }
        set { _colorTemp = value; }
    }
    public float colorTint
    {
        get { return _colorTint; }
        set { _colorTint = value; }
    }

    // Tone mapping.
    [SerializeField] bool _toneMapping = false;
    [SerializeField, Range(0, 5)] float _exposure = 1.0f;

    public bool toneMapping
    {
        get { return _toneMapping; }
        set { _toneMapping = value; }
    }
    public float exposure
    {
        get { return _exposure; }
        set { _exposure = value; }
    }

    // Color saturation.
    [SerializeField] float _saturation = 1.0f;

    public float saturation
    {
        get { return _saturation; }
        set { _saturation = value; }
    }

    // Curves.
    [SerializeField] AnimationCurve _rCurve = AnimationCurve.Linear(0, 0, 1, 1);
    [SerializeField] AnimationCurve _gCurve = AnimationCurve.Linear(0, 0, 1, 1);
    [SerializeField] AnimationCurve _bCurve = AnimationCurve.Linear(0, 0, 1, 1);
    [SerializeField] AnimationCurve _cCurve = AnimationCurve.Linear(0, 0, 1, 1);

    public AnimationCurve redCurve
    {
        get { return _rCurve; }
        set { _rCurve = value; UpdateLUT(); }
    }
    public AnimationCurve greenCurve
    {
        get { return _gCurve; }
        set { _gCurve = value; UpdateLUT(); }
    }
    public AnimationCurve blueCurve
    {
        get { return _bCurve; }
        set { _bCurve = value; UpdateLUT(); }
    }
    public AnimationCurve rgbCurve
    {
        get { return _cCurve; }
        set { _cCurve = value; UpdateLUT(); }
    }

    // Dithering.
    public enum DitherMode { Off, Ordered, Triangular }
    [SerializeField] DitherMode _ditherMode = DitherMode.Off;

    public DitherMode ditherMode
    {
        get { return _ditherMode; }
        set { _ditherMode = value; }
    }


    // Reference to the shader.
    Shader colorShader;

    // Temporary objects.
    Material _colorMaterial;
    Texture2D _lutTexture;


    [Header("Volumetric Lighting")]
    public bool renderVolumetricLighting = false;
    public VolumtericResolution Resolution = VolumtericResolution.Half;
    public Texture DefaultSpotCookie;

    [SerializeField] Nigiri_VolumetricLight.rayMarchQualityMain _globalQuality = Nigiri_VolumetricLight.rayMarchQualityMain.high;

    public Nigiri_VolumetricLight.rayMarchQualityMain globalQuality
    {
        get
        {
            switch (_globalQuality)
            {
                case Nigiri_VolumetricLight.rayMarchQualityMain.low:
                    globalRaymarchSamples = 4;
                    break;
                case Nigiri_VolumetricLight.rayMarchQualityMain.medium:
                    globalRaymarchSamples = 8;

                    break;
                case Nigiri_VolumetricLight.rayMarchQualityMain.high:
                    globalRaymarchSamples = 16;

                    break;
                case Nigiri_VolumetricLight.rayMarchQualityMain.ultra:
                    globalRaymarchSamples = 32;

                    break;
                case Nigiri_VolumetricLight.rayMarchQualityMain.overkill:
                    globalRaymarchSamples = 64;

                    break;
                default:
                    break;
            }
            return _globalQuality;

        }
        set { _globalQuality = value; }
    }

    [HideInInspector]
    public int globalRaymarchSamples;
    [Range(0.0f, 1.0f)]
    public float ScatteringCoef = 0.1f;
    [Range(0.0f, 0.1f)]
    public float ExtinctionCoef = 0.01f;
    [Range(0.0f, 1.0f)]
    public float SkyboxExtinctionCoef = 0.33f;
    [Range(0.0f, 0.999f)]
    public float MieG = 0.1f;
    public bool HeightFog = false;
    [Range(0, 0.5f)]
    public float HeightScale = 0.10f;
    public float GroundLevel = 0;

    public bool Noise = false;
    public float NoiseScale = 0.015f;
    public float NoiseIntensity = 1.0f;
    public float NoiseIntensityOffset = 0.3f;
    public Vector2 NoiseVelocity = new Vector2(3.0f, 3.0f);

    // Performance counters
    [Serializable]
    public struct FrameRate
    {
        public double Average;
        public double Last;

        [HideInInspector]
        public double PreciseAverage;
        [HideInInspector]
        public double PreciseLast;
        [HideInInspector]
        public int frameCount;
        [HideInInspector]
        public float dt;
    }
    [HideInInspector]
    public float updateRateSeconds = 4.0F;


    [Serializable]
    public struct RenderTimes
    {
        public double Total;
        public double UpdateTotal;
        public double UpdatePrimaryEncode;
        public double UpdateSecondaryEncode;
        public double UpdateMipMaps;
        public double RenderTotal;
        public double RenderTrace;
        public double RenderVoxelUpdate;
        public double RenderVolumetric;
        public double RenderToneMapping;
        public double RenderFXAA;

        public System.Diagnostics.Stopwatch UpdateStopwatch;
        public System.Diagnostics.Stopwatch RenderStopwatch;
        public System.Diagnostics.Stopwatch TraceStopwatch;
        public System.Diagnostics.Stopwatch VoxelUpdateStopwatch;
        public System.Diagnostics.Stopwatch VolumetricStopwatch;
        public System.Diagnostics.Stopwatch ToneMappingStopwatch;
        public System.Diagnostics.Stopwatch PrimaryVoxelisationStopwatch;
        public System.Diagnostics.Stopwatch SecondaryVoxelisationStopwatch;
        public System.Diagnostics.Stopwatch MipMapStopwatch;
        public System.Diagnostics.Stopwatch FXAAStopwatch;
    }

    // Render counters
    [Serializable]
    public struct RenderCounts
    {
        public int VoxelSamplesPrimary;
        public uint VoxelSamplesSecondary;

        public int[] CounterData;
        public enum Counter
        {
            VoxelisationSamplesPrimary = 0,
            VoxelisationSamplesSecondary = 1
        }
    }
    private readonly int RenderCounterMax = 2;
    public static ComputeBuffer TempCountBuffer;
    public static ComputeBuffer RenderCountBuffer;

    [Header("Performance Counters")]
    public bool expensiveGPUCounters_INCOMPLETE = true;
    public FrameRate frameRate;
    public RenderTimes renderTimes;
    public RenderCounts renderCounts;
    ///END Performance counters

    [Header("Debug Settings")]
    public string vramUsed;
    public bool VisualiseGI = false;
    //private bool VisualiseCache = false;
    public bool VisualizeVoxels = false;
    public bool visualizeDepth = false;
    public bool visualizeOcclusion = false;
    public bool visualizeReflections = false;
    public bool visualizeVolumetricLight = false;
    public DebugVoxelGrid debugVoxelGrid = DebugVoxelGrid.GRID_1;
    public bool forceImmediateRefresh = false;

    private Texture2D[] blueNoise;
    

    //[Header("Shaders")]
    private Shader tracingShader;
    private Shader blitGBufferShader;
    private Shader fxaaShader;
    private Shader depthShader;
    private Shader stereo2MonoShader;
    private ComputeShader nigiri_VoxelEntry;
    private ComputeShader nigiri_InjectionCompute;
    private ComputeShader clearComputeCache;
    private ComputeShader nigiri_VoxelEncodeUpdater;
    private ComputeShader transferIntsCompute;
    private ComputeShader mipFilterCompute;

    //[Header("Materials")]
    private Material tracerMaterial;
    private Material blitGBuffer0Material;
    private Material fxaaMaterial;
    private Material depthMaterial;
    private Material stereo2MonoMaterial;


    //[Header("Render Textures")]
    private RenderTextureDescriptor voxelGridDescriptorFloat4;

    public static RenderTexture voxelGrid1;
    public static RenderTexture voxelGrid2;
    public static RenderTexture voxelGrid3;
    public static RenderTexture voxelGrid4;
    public static RenderTexture voxelGrid5;
    public static RenderTexture voxelGridCascade1;
    public static RenderTexture voxelGridCascade2;

    public RenderTexture lightingTexture;
    public RenderTexture lightingTexture2;
    public RenderTexture lightingTextureMono;
    public RenderTexture lightingTexture2Mono;
    public RenderTexture positionTexture;
    public RenderTexture depthTexture;

    private RenderTexture blur;
    private RenderTexture gi;

    private ComputeBuffer voxelUpdateSampleCount;
    private static ComputeBuffer voxelUpdaterCounter;
    private static ComputeBuffer voxelUpdateSampleBuffer;
    private static ComputeBuffer voxelUpdateSampleCountBuffer;

    private int tracedTexture1UpdateCount;

    private float lengthOfCone = 0.0f;

    private Vector2Int injectionTextureResolution = new Vector2Int(1280, 720);

    int voxelizationSliceOffset;
    int voxelizationSliceDispatch;

    int cascadeSwitch = 0;
    int frameSwitch = 0;
    int mipSwitch = 0;
    int emissiveCameraLocationSwitch;

    private Vector3 prevPosition;
    private Vector3 prevGridPosition;

    private Vector3 gridOffset;

    GameObject emissiveCameraGO;
    Camera emissiveCamera;
    Camera localCam;

    void Start()
    {
        Debug.Log("<Nigiri> Global Illumination System: (Development release!)");
        UpdateForceGI();
    }

    private void Awake()
    {
        localCam = GetComponent<Camera>();

        if (localCam.actualRenderingPath == RenderingPath.Forward)
            localCam.depthTextureMode = DepthTextureMode.Depth;

        _currentResolution = Resolution;

        Shader shader = Shader.Find("Nigiri_VolumeLight_BlitAdd");
        if (shader == null)
            throw new Exception("Critical Error: \"Hidden/BlitAdd\" shader is missing. Make sure it is included in \"Always Included Shaders\" in ProjectSettings/Graphics.");
        _blitAddMaterial = new Material(shader);

        shader = Shader.Find("Nigiri_VolumeLight_BilateralBlur");
        if (shader == null)
            throw new Exception("Critical Error: \"Hidden/BilateralBlur\" shader is missing. Make sure it is included in \"Always Included Shaders\" in ProjectSettings/Graphics.");
        _bilateralBlurMaterial = new Material(shader);

        _preLightPass = new CommandBuffer();
        _preLightPass.name = "<Nigiri> Volumetric Prepass";

        createRenderTextures();
        CreateComputeBuffers();
        ChangeResolution();

        if (_pointLightMesh == null)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _pointLightMesh = go.GetComponent<MeshFilter>().sharedMesh;
            DestroyImmediate(go);
        }

        if (_spotLightMesh == null)
        {
            _spotLightMesh = CreateSpotLightMesh();
        }

        if (_lightMaterial == null)
        {
            shader = Shader.Find("Nigiri_VolumeLight_VolumetricLight");
            if (shader == null)
                throw new Exception("Critical Error: \"Sandbox/VolumetricLight\" shader is missing. Make sure it is included in \"Always Included Shaders\" in ProjectSettings/Graphics.");
            _lightMaterial = new Material(shader);
        }

        if (_defaultSpotCookie == null)
        {
            _defaultSpotCookie = DefaultSpotCookie;
        }

        LoadNoise3dTexture();
        GenerateDitherTexture();

        // Create performance stopwatches.
        renderTimes.UpdateStopwatch = new System.Diagnostics.Stopwatch();
        renderTimes.PrimaryVoxelisationStopwatch = new System.Diagnostics.Stopwatch();
        renderTimes.SecondaryVoxelisationStopwatch = new System.Diagnostics.Stopwatch();
        renderTimes.MipMapStopwatch = new System.Diagnostics.Stopwatch();
        renderTimes.RenderStopwatch = new System.Diagnostics.Stopwatch();
        renderTimes.TraceStopwatch = new System.Diagnostics.Stopwatch();
        renderTimes.VoxelUpdateStopwatch = new System.Diagnostics.Stopwatch();
        renderTimes.VolumetricStopwatch = new System.Diagnostics.Stopwatch();
        renderTimes.ToneMappingStopwatch = new System.Diagnostics.Stopwatch();
        renderTimes.FXAAStopwatch = new System.Diagnostics.Stopwatch();
        // Zero counters
        renderTimes.Total = 0;
        renderTimes.UpdatePrimaryEncode = 0;
        renderTimes.UpdateSecondaryEncode = 0;
        renderTimes.UpdateMipMaps = 0;
        renderTimes.RenderTotal = 0;
        renderTimes.RenderTrace = 0;
        renderTimes.RenderVoxelUpdate = 0;
        renderTimes.RenderVolumetric = 0;
        renderTimes.RenderToneMapping = 0;
        renderTimes.RenderFXAA = 0;
        renderCounts.VoxelSamplesPrimary = 0;
        renderCounts.VoxelSamplesSecondary = 0;

    }

    void Update()
    {
        renderTimes.UpdateStopwatch.Start();

        //#if UNITY_EDITOR
        if (_currentResolution != Resolution)
        {
            _currentResolution = Resolution;
            ChangeResolution();
        }

        if (_volumeLightTexture == null) ChangeResolution();

        if ((_volumeLightTexture.width != localCam.pixelWidth || _volumeLightTexture.height != localCam.pixelHeight))
            ChangeResolution();

        if (matchSunColor) if (sunLight != null) sunColor = sunLight.color;
        if (matchSkyColor)
        {
            skyColor = RenderSettings.ambientSkyColor;
            //else skyColor = RenderSettings.skybox.color;
        }

        emissiveCameraLocationSwitch = (emissiveCameraLocationSwitch + 1) % (2);
        /*if (emissiveCameraLocationSwitch == 0) emissiveCameraGO.transform.localPosition = new Vector3(0, 0, -(int)(GIAreaSize * 0.0625f));
        else if (emissiveCameraLocationSwitch == 1) emissiveCameraGO.transform.localPosition = new Vector3(0, 0, (int)(GIAreaSize * 0.0625f));
        else if (emissiveCameraLocationSwitch == 2) emissiveCameraGO.transform.localPosition = new Vector3(-(int)(GIAreaSize * 0.0625f), 0, 0);
        else if (emissiveCameraLocationSwitch == 3) emissiveCameraGO.transform.localPosition = new Vector3((int)(GIAreaSize * 0.0625f), 0, 0);*/
        /*if (emissiveCameraLocationSwitch == 0) emissiveCameraGO.transform.localPosition = new Vector3(0, 0, -25);
        else if (emissiveCameraLocationSwitch == 1) emissiveCameraGO.transform.localPosition = new Vector3(0, 0, 25);
        else if (emissiveCameraLocationSwitch == 2) emissiveCameraGO.transform.localPosition = new Vector3(-25, 0, 0);
        else if (emissiveCameraLocationSwitch == 3) emissiveCameraGO.transform.localPosition = new Vector3(25, 0, 0);*/
        if (emissiveCameraLocationSwitch == 0) emissiveCameraGO.transform.localPosition = new Vector3(0, (int)(GIAreaSize / 2), 0);
        if (emissiveCameraLocationSwitch == 1) emissiveCameraGO.transform.localPosition = new Vector3(0, (int)(GIAreaSize / 2), 0);
        //else if (emissiveCameraLocationSwitch == 4) emissiveCameraGO.transform.localPosition = new Vector3(0, 0, 0);
        emissiveCameraGO.transform.LookAt(localCam.transform);

        emissiveCamera.orthographicSize = (int)(GIAreaSize / 2);
        emissiveCamera.farClipPlane = GIAreaSize;

        FilterMode filterMode = FilterMode.Point;
        if (bilinearFiltering) filterMode = FilterMode.Bilinear;

        voxelGridCascade1.filterMode = filterMode;
        voxelGrid1.filterMode = filterMode;
        voxelGrid2.filterMode = filterMode;
        voxelGrid3.filterMode = filterMode;
        voxelGrid4.filterMode = filterMode;
        voxelGrid5.filterMode = filterMode;

        UpdateVoxelGrid();

        // This line goes at the end of update or OnRender 
        vramUsed = "VRAM Usage: " + vramUsage.ToString("F2") + " M";

        
        // FPS counter
        frameRate.frameCount++;
        frameRate.dt += Time.unscaledDeltaTime;
        if (frameRate.dt > 1.0 / updateRateSeconds)
        {
            frameRate.PreciseAverage = frameRate.frameCount / frameRate.dt;
            frameRate.Average = System.Math.Round(frameRate.PreciseAverage, 0);
            frameRate.frameCount = 0;
            frameRate.dt -= 1.0F / updateRateSeconds;
        }
        frameRate.PreciseLast = 1.0 / Time.deltaTime;
        frameRate.Last = System.Math.Round(frameRate.PreciseLast, 0);
        ///END FPS counter

        // Performance counters
        renderTimes.Total = renderTimes.UpdateTotal + renderTimes.RenderTotal;
        renderTimes.UpdateStopwatch.Stop();
        renderTimes.UpdateTotal = renderTimes.UpdateStopwatch.Elapsed.TotalMilliseconds;
        renderTimes.UpdateStopwatch.Reset();
        ///END Performance counters

        // Render counters
        Nigiri_EmissiveCameraHelper.expensiveGPUCounters = expensiveGPUCounters_INCOMPLETE;
        if (expensiveGPUCounters_INCOMPLETE)
        {
            RenderCountBuffer.GetData(renderCounts.CounterData);
            renderCounts.VoxelSamplesPrimary = renderCounts.CounterData[(int)RenderCounts.Counter.VoxelisationSamplesPrimary];
            renderCounts.VoxelSamplesSecondary = (uint)(
                (Nigiri_EmissiveCameraHelper.sampleCountColour.a << 24) |
                (Nigiri_EmissiveCameraHelper.sampleCountColour.b << 16) |
                (Nigiri_EmissiveCameraHelper.sampleCountColour.g << 8) |
                (Nigiri_EmissiveCameraHelper.sampleCountColour.r << 0));
            renderCounts.CounterData[(int)RenderCounts.Counter.VoxelisationSamplesSecondary] = (int)renderCounts.VoxelSamplesSecondary;
            renderTimes.UpdateSecondaryEncode = Nigiri_EmissiveCameraHelper.stopwatchEncode;
        }
        ///END Render counters
    }

    // Use this for initialization
    void OnEnable ()
    {
        if (_preLightPass == null) Awake();

        if (_renderCommand != null) RegisterCommandBuffers();

        clearComputeCache = Resources.Load("Nigiri_Clear") as ComputeShader;
        nigiri_VoxelEncodeUpdater = Resources.Load("Nigiri_VoxelEncodeUpdater") as ComputeShader;
        transferIntsCompute = Resources.Load("Nigiri_TransferInts") as ComputeShader;
        mipFilterCompute = Resources.Load("Nigiri_MipFilter") as ComputeShader;
        depthShader = Shader.Find("Hidden/Nigiri_Blit_CameraDepthTexture");
        blitGBufferShader = Shader.Find("Hidden/Nigiri_Blit_gBuffer0");
        fxaaShader = Shader.Find("Hidden/Nigiri_FXAA");
        stereo2MonoShader = Shader.Find("Hidden/Nigiri_Blit_Stereo2Mono");
        fxaaMaterial = new Material(fxaaShader);

        colorShader = Shader.Find("Hidden/Nigiri_Color");


        _downsample1Compute = Resources.Load("Nigiri_AO_Downsample1") as ComputeShader; ;
        _downsample2Compute = Resources.Load("Nigiri_AO_Downsample2") as ComputeShader; ;
        _renderCompute = Resources.Load("Nigiri_AO_Render") as ComputeShader; ;
        _upsampleCompute = Resources.Load("Nigiri_AO_Upsample") as ComputeShader; ;
        _blitShader = Shader.Find("Hidden/Nigiri_AO_Blit");

        nigiri_VoxelEntry = Resources.Load("Nigiri_VoxelEntry") as ComputeShader;
        nigiri_InjectionCompute = Resources.Load("Nigiri_Injection") as ComputeShader;

		GetComponent<Camera>().depthTextureMode = DepthTextureMode.Depth | DepthTextureMode.DepthNormals | DepthTextureMode.MotionVectors;

		if (tracingShader == null)  tracingShader = Shader.Find("Hidden/Nigiri_Tracing");
        tracerMaterial = new Material(tracingShader);

        if (blitGBuffer0Material == null) blitGBuffer0Material = new Material(blitGBufferShader);
        if (depthMaterial == null) depthMaterial = new Material(depthShader);
        if (stereo2MonoMaterial == null) stereo2MonoMaterial = new Material(stereo2MonoShader);

        //gridOffset = localCam.transform.position; -- Removed due to no grid-offsetting.
        gridOffset = new Vector3(0, 0, 0);

        InitializeVoxelGrid();
        createRenderTextures();
        CreateComputeBuffers();

        Setup();

        //Get blue noise textures
        blueNoise = new Texture2D[8];
        for (int i = 0; i < 8; i++)
        {
            string fileName = "LDR_RGBA_" + i.ToString();
            Texture2D blueNoiseTexture = Resources.Load("Textures/Blue Noise/64_64/" + fileName) as Texture2D;

            if (blueNoiseTexture == null)
            {
                Debug.LogWarning("Unable to find noise texture \"Assets/Nigiri/Resources/Textures/Blue Noise/64_64/" + fileName + "\" for Nigiri!");
            }

            blueNoise[i] = blueNoiseTexture;

        }

        // Destroy stale Emissive Camera objects
        GameObject[] allObjects = UnityEngine.Object.FindObjectsOfType<GameObject>();
        foreach (GameObject obj in allObjects)
        {
            if (obj.transform.name == "NKGI_EMISSIVECAMERA" & obj.transform.parent == gameObject.transform)
            {
                DestroyImmediate(obj);
            }
        }
        emissiveCameraGO = null;
        ///END Destroy stale Emissive Camera objects

        // Create new Emissive Camera object
        emissiveCameraGO = new GameObject("NKGI_EMISSIVECAMERA");
        emissiveCameraGO.transform.parent = GetComponent<Camera>().transform;
        emissiveCameraGO.transform.localEulerAngles = new Vector3(0, 0, 0);
        //emissiveCameraGO.hideFlags = HideFlags.HideAndDontSave;
        emissiveCamera = emissiveCameraGO.AddComponent<Camera>();
        emissiveCamera.CopyFrom(GetComponent<Camera>());
        emissiveCameraGO.AddComponent<Nigiri_EmissiveCameraHelper>();
        emissiveCamera.enabled = false;
        emissiveCamera.stereoTargetEye = StereoTargetEyeMask.None;
        Nigiri_EmissiveCameraHelper.injectionResolution = new Vector2Int(highestVoxelResolution, highestVoxelResolution);
        ///END Create new Emissive Camera object

        UpdateForceGI();

        //Volumetric Lighting
        localCam.AddCommandBuffer(CameraEvent.BeforeLighting, _preLightPass);
        ///

        renderCounts.CounterData = new int[RenderCounterMax];
    }

    void OnValidate()
    {
        Setup();
        UpdateLUT();
    }

    void Reset()
    {
        Setup();
        UpdateLUT();
    }



    private void createRenderTextures()
    {
        if (injectionTextureResolution.x == 0 || injectionTextureResolution.y == 0) injectionTextureResolution = new Vector2Int(1280, 720);

        if (lightingTexture != null) lightingTexture.Release();
        if (lightingTexture2 != null) lightingTexture2.Release();
        if (depthTexture != null) depthTexture.Release();
        if (gi != null) gi.Release();
        if (blur != null) blur.Release();

        //if (lightingCurveLUT != null) lightingCurveLUT.Release();

        lightingTexture = new RenderTexture(injectionTextureResolution.x, injectionTextureResolution.y, 0, RenderTextureFormat.ARGBHalf);
        lightingTexture2 = new RenderTexture(injectionTextureResolution.x, injectionTextureResolution.y, 0, RenderTextureFormat.ARGBHalf);
        if (localCam.stereoEnabled) positionTexture = new RenderTexture(injectionTextureResolution.x / 2, injectionTextureResolution.y, 0, RenderTextureFormat.ARGBHalf);
        else positionTexture = new RenderTexture(injectionTextureResolution.x, injectionTextureResolution.y, 0, RenderTextureFormat.ARGBHalf);
        if (localCam.stereoEnabled) depthTexture = new RenderTexture(injectionTextureResolution.x / 2, injectionTextureResolution.y, 0, RenderTextureFormat.RHalf);
        else depthTexture = new RenderTexture(injectionTextureResolution.x, injectionTextureResolution.y, 0, RenderTextureFormat.RHalf);
        gi = new RenderTexture(injectionTextureResolution.x * subsamplingRatio, injectionTextureResolution.y * subsamplingRatio, 0, RenderTextureFormat.ARGBHalf);
        blur = new RenderTexture(injectionTextureResolution.x * subsamplingRatio, injectionTextureResolution.y * subsamplingRatio, 0, RenderTextureFormat.ARGBHalf);
        lightingTexture.filterMode = FilterMode.Bilinear;
        lightingTexture2.filterMode = FilterMode.Bilinear;

        depthTexture.filterMode = FilterMode.Bilinear;
        blur.filterMode = FilterMode.Bilinear;
        gi.filterMode = FilterMode.Bilinear;

        if (localCam.stereoEnabled)
        {
            //We cut the injection images in half to avoid duplicate work in stereo
            lightingTextureMono = new RenderTexture(injectionTextureResolution.x / 2, injectionTextureResolution.y, 0, RenderTextureFormat.ARGBHalf);
            lightingTexture2Mono = new RenderTexture(injectionTextureResolution.x / 2, injectionTextureResolution.y, 0, RenderTextureFormat.ARGBHalf);

            lightingTextureMono.vrUsage = VRTextureUsage.None;
            lightingTexture2Mono.vrUsage = VRTextureUsage.None;
            positionTexture.vrUsage = VRTextureUsage.None; // We disable this because it needs to not be stereo so the voxer does'nt do double the work
            lightingTexture.vrUsage = VRTextureUsage.TwoEyes;
            lightingTexture2.vrUsage = VRTextureUsage.TwoEyes;
            blur.vrUsage = VRTextureUsage.TwoEyes;
            gi.vrUsage = VRTextureUsage.TwoEyes;
            depthTexture.vrUsage = VRTextureUsage.TwoEyes; // Might cause regression with voxelization
            lightingTextureMono.Create();
            lightingTexture2Mono.Create();
        }

        lightingTexture.Create();
        lightingTexture2.Create();
        positionTexture.Create();
        depthTexture.Create();
        blur.Create();
        gi.Create();
    }

    private void CreateComputeBuffers()
    {
        Debug.Log("<Nigiri> Clearing compute buffers");


        // Voxel Primary
        if (voxelUpdaterCounter != null) voxelUpdaterCounter.Release();
        voxelUpdaterCounter = new ComputeBuffer(1, sizeof(float), ComputeBufferType.Counter);

        if (voxelUpdateSampleCount != null) voxelUpdateSampleCount.Release();
        voxelUpdateSampleCount = new ComputeBuffer(highestVoxelResolution * highestVoxelResolution * highestVoxelResolution, 4, ComputeBufferType.Default);

        if (voxelUpdateSampleBuffer != null) voxelUpdateSampleBuffer.Release();
        voxelUpdateSampleBuffer = new ComputeBuffer(highestVoxelResolution * highestVoxelResolution * highestVoxelResolution, sizeof(float) * 4, ComputeBufferType.Default);

        if (voxelUpdateSampleCountBuffer != null) voxelUpdateSampleCountBuffer.Release();
        voxelUpdateSampleCountBuffer = new ComputeBuffer(highestVoxelResolution * highestVoxelResolution * highestVoxelResolution, sizeof(uint), ComputeBufferType.Default);
        ///END Voxel Primary

        // Counters
        if (TempCountBuffer != null) TempCountBuffer.Release();
        TempCountBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Counter);

        if (RenderCountBuffer != null) RenderCountBuffer.Release();
        RenderCountBuffer = new ComputeBuffer(RenderCounterMax, sizeof(int), ComputeBufferType.IndirectArguments);
        ///END Counters
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

        voxelGrid1 = new RenderTexture(voxelGridDescriptorFloat4);

        voxelGridDescriptorFloat4.width = highestVoxelResolution / 2;
		voxelGridDescriptorFloat4.height = highestVoxelResolution / 2;
		voxelGridDescriptorFloat4.volumeDepth = highestVoxelResolution / 2;

		voxelGrid2 = new RenderTexture (voxelGridDescriptorFloat4);
        voxelGridCascade1 = new RenderTexture(voxelGridDescriptorFloat4);

        voxelGridDescriptorFloat4.width = highestVoxelResolution / 4;
		voxelGridDescriptorFloat4.height = highestVoxelResolution / 4;
		voxelGridDescriptorFloat4.volumeDepth = highestVoxelResolution / 4;

		voxelGrid3 = new RenderTexture (voxelGridDescriptorFloat4);
        voxelGridCascade2 = new RenderTexture(voxelGridDescriptorFloat4);

        voxelGridDescriptorFloat4.width = highestVoxelResolution / 8;
		voxelGridDescriptorFloat4.height = highestVoxelResolution / 8;
		voxelGridDescriptorFloat4.volumeDepth = highestVoxelResolution / 8;

		voxelGrid4 = new RenderTexture (voxelGridDescriptorFloat4);

        voxelGridDescriptorFloat4.width = highestVoxelResolution / 16;
        voxelGridDescriptorFloat4.height = highestVoxelResolution / 16;
        voxelGridDescriptorFloat4.volumeDepth = highestVoxelResolution / 16;

        voxelGrid5 = new RenderTexture (voxelGridDescriptorFloat4);

        voxelGridCascade1.Create();
        voxelGridCascade2.Create();
        voxelGrid1.Create();
        voxelGrid2.Create();
		voxelGrid3.Create();
		voxelGrid4.Create();
		voxelGrid5.Create();

	}

	// Function to update data in the voxel grid
	private void UpdateVoxelGrid ()
    {
        int kernelHandle = nigiri_VoxelEntry.FindKernel("CSMain");

        // These apply to all grids        
        Shader.SetGlobalFloat("_shadowStrength", shadowStrength);
        Shader.SetGlobalFloat("_emissiveIntensity", EmissiveIntensity);


        // Secondary Voxelisation
        if (dynamicPlusEmissiveLayer.value != 0 && secondaryVoxelization)
        {
            emissiveCamera.cullingMask = dynamicPlusEmissiveLayer;
            Nigiri_EmissiveCameraHelper.DoRender();
        }
        
        ///END Secondary Voxelisation
        ///

        // Voxelize main cam
        if (primaryVoxelization)
        {
            renderTimes.PrimaryVoxelisationStopwatch.Start();
            TempCountBuffer.SetCounterValue(0);


            if (localCam.stereoEnabled)
            {
                nigiri_VoxelEntry.SetTexture(kernelHandle, "lightingTexture", lightingTextureMono);
                nigiri_VoxelEntry.SetTexture(kernelHandle, "lightingTexture2", lightingTexture2Mono);
            }
            else
            {
                nigiri_VoxelEntry.SetTexture(kernelHandle, "lightingTexture", lightingTexture);
                nigiri_VoxelEntry.SetTexture(kernelHandle, "lightingTexture2", lightingTexture2);
            }
            nigiri_VoxelEntry.SetTexture(kernelHandle, "positionTexture", positionTexture);
            nigiri_VoxelEntry.SetTexture(kernelHandle, "depthTexture", Nigiri_EmissiveCameraHelper.lightingDepthTexture);
            if (cascadeSwitch == 0) nigiri_VoxelEntry.SetTexture(kernelHandle, "voxelGrid", voxelGrid1);
            else if (cascadeSwitch == 1) nigiri_VoxelEntry.SetTexture(kernelHandle, "voxelGrid", voxelGridCascade1);
            else if (cascadeSwitch == 2) nigiri_VoxelEntry.SetTexture(kernelHandle, "voxelGrid", voxelGridCascade2);
            //nigiri_VoxelEntry.SetTexture(kernelHandle, "voxelCasacadeGrid1", voxelGridCascade1);
            //nigiri_VoxelEntry.SetTexture(kernelHandle, "voxelCasacadeGrid2", voxelGridCascade2);

            nigiri_VoxelEntry.SetFloat("_shadowStrength", shadowStrength);
            nigiri_VoxelEntry.SetFloat("occlusionGain", occlusionGain);
            nigiri_VoxelEntry.SetFloat("worldVolumeBoundary", GIAreaSize);
            nigiri_VoxelEntry.SetFloat("emissiveIntensity", EmissiveIntensity);

            nigiri_VoxelEntry.SetInt("useDepth", 0);
            nigiri_VoxelEntry.SetInt("cascade", cascadeSwitch);
            nigiri_VoxelEntry.SetInt("voxelResolution", highestVoxelResolution);
            nigiri_VoxelEntry.SetInt("nearestNeighbourPropagation", neighbourPropagation ? 1 : 0);

            nigiri_VoxelEntry.SetBuffer(kernelHandle, "SampleBuffer", voxelUpdateSampleBuffer);
            nigiri_VoxelEntry.SetBuffer(kernelHandle, "SampleCountBuffer", voxelUpdateSampleCountBuffer);
            nigiri_VoxelEntry.SetBuffer(kernelHandle, "RenderCounter", TempCountBuffer);

            nigiri_VoxelEntry.Dispatch(kernelHandle, lightingTexture.width / 16, lightingTexture.height / 16, 1);
            renderTimes.PrimaryVoxelisationStopwatch.Stop();
            renderTimes.UpdatePrimaryEncode = renderTimes.PrimaryVoxelisationStopwatch.Elapsed.TotalMilliseconds;
            renderTimes.PrimaryVoxelisationStopwatch.Reset();

            if (expensiveGPUCounters_INCOMPLETE)
            {
                ComputeBuffer.CopyCount(TempCountBuffer, RenderCountBuffer, 4 * (int)RenderCounts.Counter.VoxelisationSamplesPrimary);
            }

            // Transfer encoded voxels to dataset
            renderTimes.VoxelUpdateStopwatch.Start();
            voxelUpdaterCounter.SetCounterValue(0);
            if (cascadeSwitch == 0) nigiri_VoxelEncodeUpdater.SetTexture(0, "voxelGrid", voxelGrid1);
            if (cascadeSwitch == 1) nigiri_VoxelEncodeUpdater.SetTexture(0, "voxelGrid", voxelGridCascade1);
            if (cascadeSwitch == 2) nigiri_VoxelEncodeUpdater.SetTexture(0, "voxelGrid", voxelGridCascade2);
            nigiri_VoxelEncodeUpdater.SetInt("Resolution", highestVoxelResolution);
            nigiri_VoxelEncodeUpdater.SetBuffer(0, "UpdateCounter", voxelUpdaterCounter);
            nigiri_VoxelEncodeUpdater.SetBuffer(0, "SampleBuffer", voxelUpdateSampleBuffer);
            nigiri_VoxelEncodeUpdater.SetBuffer(0, "SampleCountBuffer", voxelUpdateSampleCountBuffer);
            nigiri_VoxelEncodeUpdater.Dispatch(0, highestVoxelResolution / 8, highestVoxelResolution / 8, highestVoxelResolution / 8);
            renderTimes.VoxelUpdateStopwatch.Stop();
            renderTimes.RenderVoxelUpdate = renderTimes.VoxelUpdateStopwatch.Elapsed.TotalMilliseconds;
            renderTimes.VoxelUpdateStopwatch.Reset();

            cascadeSwitch = (cascadeSwitch + 1) % (3);
        }

        ///END Voxelize main cam

        // Update MipMaps
        renderTimes.MipMapStopwatch.Start();
        if (mipSwitch == 0)
        {
            int destinationRes = (int)highestVoxelResolution / 2;
            mipFilterCompute.SetInt("destinationRes", destinationRes);
            mipFilterCompute.SetTexture(gaussianMipFiltering ? 1 : 0, "Source", voxelGrid1);
            mipFilterCompute.SetTexture(gaussianMipFiltering ? 1 : 0, "Destination", voxelGrid2);
            mipFilterCompute.Dispatch(gaussianMipFiltering ? 1 : 0, destinationRes / 8, destinationRes / 8, 1);
        }
        else if (mipSwitch == 1)
        {
            int destinationRes = (int)highestVoxelResolution / 4;
            mipFilterCompute.SetInt("destinationRes", destinationRes);
            mipFilterCompute.SetTexture(gaussianMipFiltering ? 1 : 0, "Source", voxelGrid2);
            mipFilterCompute.SetTexture(gaussianMipFiltering ? 1 : 0, "Destination", voxelGrid3);
            mipFilterCompute.Dispatch(gaussianMipFiltering ? 1 : 0, destinationRes / 8, destinationRes / 8, 1);
        }
        else if (mipSwitch == 2)
        {
            int destinationRes = (int)highestVoxelResolution / 8;
            mipFilterCompute.SetInt("destinationRes", destinationRes);
            mipFilterCompute.SetTexture(gaussianMipFiltering ? 1 : 0, "Source", voxelGrid3);
            mipFilterCompute.SetTexture(gaussianMipFiltering ? 1 : 0, "Destination", voxelGrid4);
            mipFilterCompute.Dispatch(gaussianMipFiltering ? 1 : 0, destinationRes / 8, destinationRes / 8, 1);
        }
        else if (mipSwitch == 3)
        {
            int destinationRes = (int)highestVoxelResolution / 16;
            mipFilterCompute.SetInt("destinationRes", destinationRes);
            mipFilterCompute.SetTexture(gaussianMipFiltering ? 1 : 0, "Source", voxelGrid4);
            mipFilterCompute.SetTexture(gaussianMipFiltering ? 1 : 0, "Destination", voxelGrid5);
            mipFilterCompute.Dispatch(gaussianMipFiltering ? 1 : 0, destinationRes / 8, destinationRes / 8, 1);
        }
        mipSwitch = (mipSwitch + 1) % (4);

        renderTimes.MipMapStopwatch.Stop();
        renderTimes.UpdateMipMaps = renderTimes.MipMapStopwatch.Elapsed.TotalMilliseconds;
        renderTimes.MipMapStopwatch.Reset();
        ///END Update MipMaps

        //// Experimental Octree Building

        /// Performance counters
        //  




    }


    // This is called once per frame after the scene is rendered
    //[ImageEffectOpaque]
    void OnRenderImage (RenderTexture source, RenderTexture destination)
    {
        renderTimes.RenderStopwatch.Start();

        if (forceImmediateRefresh)
        {
            injectionTextureResolution.x = source.width / subsamplingRatio;
            injectionTextureResolution.y = source.height / subsamplingRatio;

            forceImmediateRefresh = false;
            UpdateForceGI();
        }

        if ((injectionTextureResolution.x != (int)source.width / subsamplingRatio) || (injectionTextureResolution.y != (int)source.height / subsamplingRatio))
        {
            Debug.Log("<Nigiri> Resizing render textures");
            injectionTextureResolution.x = (int)source.width / subsamplingRatio;
            injectionTextureResolution.y = (int)source.height / subsamplingRatio;
            createRenderTextures();
            CreateComputeBuffers();
        }

        Camera localCam = GetComponent<Camera>();

        //Fix stereo rendering matrix
        if (localCam.stereoEnabled)
        {
            // Left and Right Eye inverse View Matrices
            Matrix4x4 leftToWorld = localCam.GetStereoViewMatrix(Camera.StereoscopicEye.Left).inverse;
            Matrix4x4 rightToWorld = localCam.GetStereoViewMatrix(Camera.StereoscopicEye.Right).inverse;
            Shader.SetGlobalMatrix("_LeftEyeToWorld", leftToWorld);
            Shader.SetGlobalMatrix("_RightEyeToWorld", rightToWorld);

            Matrix4x4 leftEye = localCam.GetStereoProjectionMatrix(Camera.StereoscopicEye.Left);
            Matrix4x4 rightEye = localCam.GetStereoProjectionMatrix(Camera.StereoscopicEye.Right);

            // Compensate for RenderTexture...
            leftEye = GL.GetGPUProjectionMatrix(leftEye, true).inverse;
            rightEye = GL.GetGPUProjectionMatrix(rightEye, true).inverse;
            // Negate [1,1] to reflect Unity's CBuffer state
            leftEye[1, 1] *= -1;
            rightEye[1, 1] *= -1;

            Shader.SetGlobalMatrix("_LeftEyeProjection", leftEye);
            Shader.SetGlobalMatrix("_RightEyeProjection", rightEye);
        }
        //Fix stereo rendering matrix/

        //lengthOfCone = (32.0f * coneLength * GIAreaSize) / (highestVoxelResolution * Mathf.Tan(Mathf.PI / 6.0f));// * -2;
        lengthOfCone = GIAreaSize / (highestVoxelResolution);// * Mathf.Tan(Mathf.PI / 6.0f));// * -2;

        //Color Settings
        var linear = QualitySettings.activeColorSpace == ColorSpace.Linear;

        Setup();

        if (linear)
            _colorMaterial.EnableKeyword("COLORSPACE_LINEAR");
        else
            _colorMaterial.DisableKeyword("COLORSPACE_LINEAR");

        if (_colorTemp != 0.0f || _colorTint != 0.0f)
        {
            _colorMaterial.EnableKeyword("BALANCING_ON");
            _colorMaterial.SetVector("_Balance", CalculateColorBalance());
        }
        else
            _colorMaterial.DisableKeyword("BALANCING_ON");

        if (_toneMapping && linear)
        {
            _colorMaterial.EnableKeyword("TONEMAPPING_ON");
            _colorMaterial.SetFloat("_Exposure", _exposure);
        }
        else
            _colorMaterial.DisableKeyword("TONEMAPPING_ON");

        _colorMaterial.SetTexture("_Curves", _lutTexture);
        _colorMaterial.SetFloat("_Saturation", _saturation);

        if (_ditherMode == DitherMode.Ordered)
        {
            _colorMaterial.EnableKeyword("DITHER_ORDERED");
            _colorMaterial.DisableKeyword("DITHER_TRIANGULAR");
        }
        else if (_ditherMode == DitherMode.Triangular)
        {
            _colorMaterial.DisableKeyword("DITHER_ORDERED");
            _colorMaterial.EnableKeyword("DITHER_TRIANGULAR");
        }
        else
        {
            _colorMaterial.DisableKeyword("DITHER_ORDERED");
            _colorMaterial.DisableKeyword("DITHER_TRIANGULAR");
        }

        tracerMaterial.SetMatrix ("InverseViewMatrix", GetComponent<Camera>().cameraToWorldMatrix);
        tracerMaterial.SetMatrix ("InverseProjectionMatrix", GetComponent<Camera>().projectionMatrix.inverse);
        Shader.SetGlobalFloat("worldVolumeBoundary", GIAreaSize);
		tracerMaterial.SetFloat ("maximumIterations", maximumIterations);
        tracerMaterial.SetInt("depthStopOptimization", depthStopOptimization ? 1 : 0);
        tracerMaterial.SetFloat ("indirectLightingStrength", indirectLightingStrength);
        tracerMaterial.SetFloat ("lengthOfCone", lengthOfCone);
        //tracerMaterial.SetFloat("coneWidth", coneWidth);
        tracerMaterial.SetFloat("ConeTraceBias", coneTraceBias);
        tracerMaterial.SetColor("sunColor", sunColor);
        tracerMaterial.SetColor("skyColor", skyColor);
        if (sunLight != null) tracerMaterial.SetVector("sunLight", sunLight.transform.rotation.eulerAngles);
        else tracerMaterial.SetVector("sunLight", new Vector3(80, 0, 0));
        tracerMaterial.SetFloat("sunLightInjection", sunLightInjection);
        tracerMaterial.SetInt("sphericalSunlight", sphericalSunlight ? 1 : 0);
        tracerMaterial.SetInt("neighbourSearch", neighbourSearch ? 1 : 0);
        tracerMaterial.SetInt("highestValueSearch", mipLevelSearch ? 1 : 0);
        tracerMaterial.SetInt("skipFirstMipLevel", skipFirstMipLevel ? 1 : 0);
        tracerMaterial.SetInt("skipLastMipLevel", skipLastMipLevel ? 1 : 0);

        tracerMaterial.SetFloat("rayStep", rayStep);
        tracerMaterial.SetFloat("rayOffset", rayOffset);
        tracerMaterial.SetFloat("BalanceGain", reflectionGain * 10);
        tracerMaterial.SetFloat("maximumIterationsReflection", (float)reflectionSteps);
        tracerMaterial.SetVector("mainCameraPosition", localCam.transform.position);
        tracerMaterial.SetInt("DoReflections", traceReflections ? 1 : 0);
        tracerMaterial.SetFloat("skyReflectionIntensity", skyReflectionIntensity);

        Shader.SetGlobalInt("highestVoxelResolution", highestVoxelResolution);
        tracerMaterial.SetInt("StochasticSampling", stochasticSampling ? 1 : 0);
        tracerMaterial.SetFloat("stochasticSamplingScale", stochasticFactor);
        tracerMaterial.SetInt("VisualiseGI", VisualiseGI ? 1 : 0);
        tracerMaterial.SetInt("visualizeOcclusion", visualizeOcclusion ? 1 : 0);
        tracerMaterial.SetInt("visualizeReflections", visualizeReflections ? 1 : 0);
        tracerMaterial.SetFloat("GIGain", GIGain);
        tracerMaterial.SetFloat("NearLightGain", NearLightGain);
        tracerMaterial.SetInt("stereoEnabled", localCam.stereoEnabled ? 1 : 0);

        Graphics.Blit(source, lightingTexture);
        Graphics.Blit(null, lightingTexture2, blitGBuffer0Material);

        if (localCam.stereoEnabled)
        {
            Graphics.Blit(lightingTexture, lightingTextureMono, stereo2MonoMaterial);
            Graphics.Blit(lightingTexture2, lightingTexture2Mono, stereo2MonoMaterial);
        }

        //We send half the stereo eyeDistance to the depth blit. To offset correct coordinates in stereo clamping
        depthMaterial.SetInt("stereoEnabled", localCam.stereoEnabled ? 1 : 0);
        depthMaterial.SetInt("debug", visualizeDepth ? 1 : 0);
        Graphics.Blit(null, depthTexture, depthMaterial);

        //We only want to retrieve a single eye for the positional texture if we're in stereo
        tracerMaterial.SetInt("Stereo2Mono", localCam.stereoEnabled ? 1 : 0);
        Graphics.Blit(source, positionTexture, tracerMaterial, 0);
        tracerMaterial.SetInt("Stereo2Mono", 0);

        tracerMaterial.SetVector("gridOffset", gridOffset);

        if (visualizeDepth)
        {
            Graphics.Blit(depthTexture, destination);
            return;
        }

        tracerMaterial.SetTexture("voxelGrid1", voxelGrid1);
        tracerMaterial.SetTexture("voxelGrid2", voxelGrid2);
        tracerMaterial.SetTexture("voxelGrid3", voxelGrid3);
        tracerMaterial.SetTexture("voxelGrid4", voxelGrid4);
        tracerMaterial.SetTexture("voxelGrid5", voxelGrid5);
        tracerMaterial.SetTexture("voxelGridCascade1", voxelGridCascade1);
        tracerMaterial.SetTexture("voxelGridCascade2", voxelGridCascade2);


        if (VisualizeVoxels) {
			if (debugVoxelGrid == DebugVoxelGrid.GRID_1) {
				tracerMaterial.EnableKeyword ("GRID_1");
				tracerMaterial.DisableKeyword ("GRID_2");
				tracerMaterial.DisableKeyword ("GRID_3");
				tracerMaterial.DisableKeyword ("GRID_4");
				tracerMaterial.DisableKeyword ("GRID_5");
			} else if (debugVoxelGrid == DebugVoxelGrid.GRID_2) {
				tracerMaterial.DisableKeyword ("GRID_1");
				tracerMaterial.EnableKeyword ("GRID_2");
				tracerMaterial.DisableKeyword ("GRID_3");
				tracerMaterial.DisableKeyword ("GRID_4");
				tracerMaterial.DisableKeyword ("GRID_5");
			} else if (debugVoxelGrid == DebugVoxelGrid.GRID_3) {
				tracerMaterial.DisableKeyword ("GRID_1");
				tracerMaterial.DisableKeyword ("GRID_2");
				tracerMaterial.EnableKeyword ("GRID_3");
				tracerMaterial.DisableKeyword ("GRID_4");
				tracerMaterial.DisableKeyword ("GRID_5");
			} else if (debugVoxelGrid == DebugVoxelGrid.GRID_4) {
				tracerMaterial.DisableKeyword ("GRID_1");
				tracerMaterial.DisableKeyword ("GRID_2");
				tracerMaterial.DisableKeyword ("GRID_3");
				tracerMaterial.EnableKeyword ("GRID_4");
				tracerMaterial.DisableKeyword ("GRID_5");
			} else {
				tracerMaterial.DisableKeyword ("GRID_1");
				tracerMaterial.DisableKeyword ("GRID_2");
				tracerMaterial.DisableKeyword ("GRID_3");
				tracerMaterial.DisableKeyword ("GRID_4");
				tracerMaterial.EnableKeyword ("GRID_5");
			}

            renderTimes.TraceStopwatch.Start();
            Graphics.Blit (source, destination, tracerMaterial, 1);
            renderTimes.TraceStopwatch.Stop();
            renderTimes.RenderTrace = renderTimes.TraceStopwatch.Elapsed.TotalMilliseconds;
            renderTimes.TraceStopwatch.Reset();
            return;
		} else {
            Shader.SetGlobalTexture("NoiseTexture", blueNoise[frameSwitch % 8]);
            renderTimes.TraceStopwatch.Start();
            Graphics.SetRandomWriteTarget(1, voxelUpdateSampleCountBuffer, true);
            Graphics.Blit (source, gi, tracerMaterial, 2);
            Graphics.ClearRandomWriteTargets();
            renderTimes.TraceStopwatch.Stop();
            renderTimes.RenderTrace = renderTimes.TraceStopwatch.Elapsed.TotalMilliseconds;
            renderTimes.TraceStopwatch.Reset();

            // Clear voxels not updated, but traced through this frame.
            /*voxelGrid1.filterMode = FilterMode.Point;
            clearComputeCache.SetTexture(1, "RG0", voxelGrid1);
            clearComputeCache.SetTexture(1, "voxelCasacadeGrid1", voxelGridCascade1);
            clearComputeCache.SetTexture(1, "voxelCasacadeGrid2", voxelGridCascade2);
            clearComputeCache.SetInt("Resolution", highestVoxelResolution);
            clearComputeCache.SetBuffer(1, "voxelUpdateBuffer", voxelUpdateSampleCountBuffer);
            clearComputeCache.SetFloat("temporalStablityVsRefreshRate", temporalStablityVsRefreshRate);
            clearComputeCache.Dispatch(1, highestVoxelResolution / 16, highestVoxelResolution / 16, 1);
            if (gaussianMipFiltering) voxelGrid1.filterMode = FilterMode.Bilinear;*/
            ///

            // FXAA
            renderTimes.FXAAStopwatch.Start();
            Graphics.Blit(gi, blur, fxaaMaterial, 0);
            Graphics.Blit(blur, gi, fxaaMaterial, 1);
            renderTimes.FXAAStopwatch.Stop();
            renderTimes.RenderFXAA = renderTimes.FXAAStopwatch.Elapsed.TotalMilliseconds;
            renderTimes.FXAAStopwatch.Reset();
            ///END FXAA

            //Graphics.Blit(gi, destination);

            //Advance the frame counter
            frameSwitch = (frameSwitch + 1) % (64);

        }

        //Volumetric Lighting
        renderTimes.VolumetricStopwatch.Start();
        if (renderVolumetricLighting)
        {
            if (Resolution == VolumtericResolution.Quarter)
            {
                if (_quarterDepthBuffer == null) ChangeResolution();
                RenderTexture temp = RenderTexture.GetTemporary(_quarterDepthBuffer.width, _quarterDepthBuffer.height, 0, RenderTextureFormat.ARGBHalf, 
                    RenderTextureReadWrite.Default, 1, RenderTextureMemoryless.None, XRSettings.eyeTextureDesc.vrUsage);

                temp.filterMode = FilterMode.Bilinear;

                // horizontal bilateral blur at quarter res
                Graphics.Blit(_quarterVolumeLightTexture, temp, _bilateralBlurMaterial, 8);
                // vertical bilateral blur at quarter res
                Graphics.Blit(temp, _quarterVolumeLightTexture, _bilateralBlurMaterial, 9);

                // upscale to full res
                Graphics.Blit(_quarterVolumeLightTexture, _volumeLightTexture, _bilateralBlurMaterial, 7);

                RenderTexture.ReleaseTemporary(temp);
            }
            else if (Resolution == VolumtericResolution.Half)
            {
                if (_halfVolumeLightTexture == null) ChangeResolution();

                RenderTexture temp = RenderTexture.GetTemporary(_halfVolumeLightTexture.width, _halfVolumeLightTexture.height, 0, RenderTextureFormat.ARGBHalf,
                    RenderTextureReadWrite.Default, 1, RenderTextureMemoryless.None, XRSettings.eyeTextureDesc.vrUsage);

                temp.filterMode = FilterMode.Bilinear;

                // horizontal bilateral blur at half res
                Graphics.Blit(_halfVolumeLightTexture, temp, _bilateralBlurMaterial, 2);

                // vertical bilateral blur at half res
                Graphics.Blit(temp, _halfVolumeLightTexture, _bilateralBlurMaterial, 3);

                // upscale to full res
                Graphics.Blit(_halfVolumeLightTexture, _volumeLightTexture, _bilateralBlurMaterial, 5);
                RenderTexture.ReleaseTemporary(temp);           
            }
            else
            {
                if (_volumeLightTexture == null) ChangeResolution();

                RenderTexture temp = RenderTexture.GetTemporary(_volumeLightTexture.width, _volumeLightTexture.height, 0, RenderTextureFormat.ARGBHalf,
                    RenderTextureReadWrite.Default, 1, RenderTextureMemoryless.None, XRSettings.eyeTextureDesc.vrUsage);

                temp.filterMode = FilterMode.Bilinear;

                // horizontal bilateral blur at full res
                Graphics.Blit(_volumeLightTexture, temp, _bilateralBlurMaterial, 0);
                // vertical bilateral blur at full res
                Graphics.Blit(temp, _volumeLightTexture, _bilateralBlurMaterial, 1);
                RenderTexture.ReleaseTemporary(temp);
            }

            // add volume light buffer to rendered scene
            if (visualizeVolumetricLight) Graphics.Blit(_volumeLightTexture, destination);
            else
            {
                RenderTexture temp = RenderTexture.GetTemporary(_volumeLightTexture.width, _volumeLightTexture.height, 0, RenderTextureFormat.ARGBHalf,
                RenderTextureReadWrite.Default, 1, RenderTextureMemoryless.None, XRSettings.eyeTextureDesc.vrUsage);
                temp.filterMode = FilterMode.Bilinear;
                renderTimes.ToneMappingStopwatch.Start();
                Graphics.Blit(gi, temp, _colorMaterial);
                renderTimes.ToneMappingStopwatch.Stop();

                _blitAddMaterial.SetTexture("_Source", temp);
                Graphics.Blit(_volumeLightTexture, destination, _blitAddMaterial, 0);
                RenderTexture.ReleaseTemporary(temp);

            }

        }
        else
        {
            renderTimes.ToneMappingStopwatch.Start();
            Graphics.Blit(gi, destination, _colorMaterial);
            renderTimes.ToneMappingStopwatch.Stop();
        }
        renderTimes.RenderToneMapping = renderTimes.ToneMappingStopwatch.Elapsed.TotalMilliseconds;
        renderTimes.ToneMappingStopwatch.Reset();
        renderTimes.VolumetricStopwatch.Stop();
        renderTimes.RenderVolumetric = Math.Round(renderTimes.VolumetricStopwatch.Elapsed.TotalMilliseconds - renderTimes.RenderToneMapping, 4);
        renderTimes.VolumetricStopwatch.Reset();
        ///

        renderTimes.RenderStopwatch.Stop();
        renderTimes.RenderTotal = renderTimes.RenderStopwatch.Elapsed.TotalMilliseconds;
        renderTimes.RenderStopwatch.Reset();
    }

    private void OnDisable()
    {
        if (_renderCommand != null) UnregisterCommandBuffers();

        if (lightingTexture != null) lightingTexture.Release();
        if (lightingTexture2 != null) lightingTexture2.Release();
        if (lightingTextureMono != null) lightingTextureMono.Release();
        if (lightingTexture2Mono != null) lightingTexture2Mono.Release();
        if (positionTexture != null) positionTexture.Release();
        if (depthTexture != null) depthTexture.Release();
        if (gi != null) gi.Release();
        if (blur != null) blur.Release();
             
        if (voxelUpdateSampleCount != null) voxelUpdateSampleCount.Release();
        if (emissiveCameraGO != null) GameObject.DestroyImmediate(emissiveCameraGO);

        if (voxelGridCascade1 != null) voxelGridCascade1.Release();
        if (voxelGridCascade2 != null) voxelGridCascade2.Release();
        if (voxelGrid1 != null) voxelGrid1.Release();
        if (voxelGrid2 != null) voxelGrid2.Release();
        if (voxelGrid3 != null) voxelGrid3.Release();
        if (voxelGrid4 != null) voxelGrid4.Release();
        if (voxelGrid5 != null) voxelGrid5.Release();

        if (voxelUpdaterCounter != null) voxelUpdaterCounter.Release();
        if (voxelUpdateSampleBuffer != null) voxelUpdateSampleBuffer.Release();
        if (voxelUpdateSampleCountBuffer != null) voxelUpdateSampleCountBuffer.Release();

        if (TempCountBuffer != null) TempCountBuffer.Release();
        if (RenderCountBuffer != null) RenderCountBuffer.Release();

        //Volumetric Lighting
        //_camera.RemoveAllCommandBuffers();
        localCam.RemoveCommandBuffer(CameraEvent.BeforeLighting, _preLightPass);
        ///
    }

    public void UpdateForceGI()
    {
        Debug.Log("<Nigiri> Clearing cache");

        CreateComputeBuffers();

        clearComputeCache.SetTexture(0, "RG0", voxelGridCascade1);
        clearComputeCache.SetInt("Resolution", highestVoxelResolution);
        clearComputeCache.Dispatch(0, highestVoxelResolution / 16, highestVoxelResolution / 16, 1);

        clearComputeCache.SetTexture(0, "RG0", voxelGridCascade2);
        clearComputeCache.SetInt("Resolution", highestVoxelResolution);
        clearComputeCache.Dispatch(0, highestVoxelResolution / 16, highestVoxelResolution / 16, 1);

        clearComputeCache.SetTexture(0, "RG0", voxelGrid1);
        clearComputeCache.SetInt("Resolution", highestVoxelResolution);
        clearComputeCache.Dispatch(0, highestVoxelResolution / 16, highestVoxelResolution / 16, 1);

        clearComputeCache.SetTexture(0, "RG0", voxelGrid2);
        clearComputeCache.SetInt("Resolution", highestVoxelResolution);
        clearComputeCache.Dispatch(0, highestVoxelResolution / 16, highestVoxelResolution / 16, 1);

        clearComputeCache.SetTexture(0, "RG0", voxelGrid3);
        clearComputeCache.SetInt("Resolution", highestVoxelResolution);
        clearComputeCache.Dispatch(0, highestVoxelResolution / 16, highestVoxelResolution / 16, 1);

        clearComputeCache.SetTexture(0, "RG0", voxelGrid4);
        clearComputeCache.SetInt("Resolution", highestVoxelResolution);
        clearComputeCache.Dispatch(0, highestVoxelResolution / 16, highestVoxelResolution / 16, 1);

        clearComputeCache.SetTexture(0, "RG0", voxelGrid5);
        clearComputeCache.SetInt("Resolution", 256);
        clearComputeCache.Dispatch(0, highestVoxelResolution / 16, highestVoxelResolution / 16, 1);

        mipSwitch = 0;
        prevPosition = GetComponent<Camera>().transform.position;
    }

    public int bitValue(RenderTexture x)
    {

        int bit = 0;
        switch (x.format)
        {
            case RenderTextureFormat.ARGB32:
                break;
            case RenderTextureFormat.Depth:
                break;
            case RenderTextureFormat.ARGBHalf:
                bit = 16 * 4;
                break;
            case RenderTextureFormat.Shadowmap:
                break;
            case RenderTextureFormat.RGB565:
                break;
            case RenderTextureFormat.ARGB4444:
                break;
            case RenderTextureFormat.ARGB1555:
                break;
            case RenderTextureFormat.Default:
                break;
            case RenderTextureFormat.ARGB2101010:
                break;
            case RenderTextureFormat.DefaultHDR:
                break;
            case RenderTextureFormat.ARGB64:
                break;
            case RenderTextureFormat.ARGBFloat:
                break;
            case RenderTextureFormat.RGFloat:
                break;
            case RenderTextureFormat.RGHalf:
                break;
            case RenderTextureFormat.RFloat:
                bit = 32;
                break;
            case RenderTextureFormat.RHalf:
                bit = 16;
                break;
            case RenderTextureFormat.R8:
                bit = 8;
                break;
            case RenderTextureFormat.ARGBInt:
                break;
            case RenderTextureFormat.RGInt:
                break;
            case RenderTextureFormat.RInt:
                break;
            case RenderTextureFormat.BGRA32:
                break;
            case RenderTextureFormat.RGB111110Float:
                break;
            case RenderTextureFormat.RG32:
                break;
            case RenderTextureFormat.RGBAUShort:
                break;
            case RenderTextureFormat.RG16:
                break;
            case RenderTextureFormat.BGRA10101010_XR:
                break;
            case RenderTextureFormat.BGR101010_XR:
                break;
            default:
                break;
        }
        if (bit == 0)
            Debug.Log(bit + " " + x.name + " bit Value is 0, resolve");
        return bit;
    }


    ///// <summary> 
    ///// Estimates the VRAM usage of all the render textures used to render GI. 
    ///// </summary> 
    public float vramUsage  //TODO: Update vram usage calculation 
    {
        get
        {
            if (!enabled)
            {
                return 0.0f;
            }
            long v = 0;

            if (lightingTexture != null)
                v += lightingTexture.width * lightingTexture.height * bitValue(lightingTexture);

            if (lightingTextureMono != null)
                v += lightingTextureMono.width * lightingTextureMono.height * bitValue(lightingTextureMono); ;

            if (positionTexture != null)
                v += positionTexture.width * positionTexture.height * bitValue(positionTexture);

            if (depthTexture != null)
                v += depthTexture.width * depthTexture.height * depthTexture.volumeDepth * bitValue(depthTexture);

            if (gi != null)
                v += gi.width * gi.height * bitValue(gi);

            if (blur != null)
                v += blur.width * blur.height * bitValue(blur);

            if (voxelGrid1 != null)
                v += voxelGrid1.width * voxelGrid1.height * voxelGrid1.volumeDepth * bitValue(voxelGrid1);
            if (voxelGrid2 != null)
                v += voxelGrid2.width * voxelGrid2.height * voxelGrid2.volumeDepth * bitValue(voxelGrid2);
            if (voxelGrid3 != null)
                v += voxelGrid3.width * voxelGrid3.height * voxelGrid3.volumeDepth * bitValue(voxelGrid3);
            if (voxelGrid4 != null)
                v += voxelGrid4.width * voxelGrid4.height * voxelGrid4.volumeDepth * bitValue(voxelGrid4);
            if (voxelGrid5 != null)
                v += voxelGrid5.width * voxelGrid5.height * voxelGrid5.volumeDepth * bitValue(voxelGrid5);

            if (voxelGridCascade1 != null)
                v += voxelGridCascade1.width * voxelGridCascade1.height * voxelGridCascade1.volumeDepth * bitValue(voxelGridCascade1);
            if (voxelGridCascade2 != null)
                v += voxelGridCascade2.width * voxelGridCascade2.height * voxelGridCascade2.volumeDepth * bitValue(voxelGridCascade2);

            float vram = (v / 8388608.0f);

            return vram;
        }
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

    #region AO
    bool singlePassStereoEnabled
    {
        get
        {
            return
                localCam != null &&
                localCam.stereoEnabled &&
                localCam.targetTexture == null &&
                _drawCountPerFrame == 1;
        }
    }

    bool ambientOnlyEnabled
    {
        get
        {
            return
                _ambientOnly && localCam.allowHDR &&
                localCam.actualRenderingPath == RenderingPath.DeferredShading;
        }
    }

    void RegisterCommandBuffers()
    {
        // In deferred ambient-only mode, we use BeforeReflections not
        // AfterGBuffer because we need the resolved depth that is not yet
        // available at the moment of AfterGBuffer.

        if (ambientOnlyEnabled)
            localCam.AddCommandBuffer(CameraEvent.BeforeReflections, _renderCommand);
        else
            localCam.AddCommandBuffer(CameraEvent.BeforeImageEffects, _renderCommand);

        if (_debug > 0)
            localCam.AddCommandBuffer(CameraEvent.AfterImageEffects, _compositeCommand);
        else if (ambientOnlyEnabled)
            localCam.AddCommandBuffer(CameraEvent.BeforeLighting, _compositeCommand);
        else
            localCam.AddCommandBuffer(CameraEvent.BeforeImageEffects, _compositeCommand);
    }

    void UnregisterCommandBuffers()
    {
        localCam.RemoveCommandBuffer(CameraEvent.BeforeReflections, _renderCommand);
        localCam.RemoveCommandBuffer(CameraEvent.BeforeImageEffects, _renderCommand);
        localCam.RemoveCommandBuffer(CameraEvent.BeforeLighting, _compositeCommand);
        localCam.RemoveCommandBuffer(CameraEvent.BeforeImageEffects, _compositeCommand);
        localCam.RemoveCommandBuffer(CameraEvent.AfterImageEffects, _compositeCommand);
    }

    void DoLazyInitialization()
    {
        // Camera reference
        if (localCam == null)
        {
            //localCam = GetComponent<Camera>();
            // We requires the camera depth texture.
            //localCam.depthTextureMode = DepthTextureMode.Depth;
        }

        // Render texture handles
        if (_result == null)
        {
            _depthCopy = new RTHandle("DepthCopy", TextureType.Float, MipLevel.Original);
            _linearDepth = new RTHandle("LinearDepth", TextureType.HalfUAV, MipLevel.Original);

            _lowDepth1 = new RTHandle("LowDepth1", TextureType.FloatUAV, MipLevel.L1);
            _lowDepth2 = new RTHandle("LowDepth2", TextureType.FloatUAV, MipLevel.L2);
            _lowDepth3 = new RTHandle("LowDepth3", TextureType.FloatUAV, MipLevel.L3);
            _lowDepth4 = new RTHandle("LowDepth4", TextureType.FloatUAV, MipLevel.L4);

            _tiledDepth1 = new RTHandle("TiledDepth1", TextureType.HalfTiledUAV, MipLevel.L3);
            _tiledDepth2 = new RTHandle("TiledDepth2", TextureType.HalfTiledUAV, MipLevel.L4);
            _tiledDepth3 = new RTHandle("TiledDepth3", TextureType.HalfTiledUAV, MipLevel.L5);
            _tiledDepth4 = new RTHandle("TiledDepth4", TextureType.HalfTiledUAV, MipLevel.L6);

            _occlusion1 = new RTHandle("Occlusion1", TextureType.FixedUAV, MipLevel.L1);
            _occlusion2 = new RTHandle("Occlusion2", TextureType.FixedUAV, MipLevel.L2);
            _occlusion3 = new RTHandle("Occlusion3", TextureType.FixedUAV, MipLevel.L3);
            _occlusion4 = new RTHandle("Occlusion4", TextureType.FixedUAV, MipLevel.L4);

            _combined1 = new RTHandle("Combined1", TextureType.FixedUAV, MipLevel.L1);
            _combined2 = new RTHandle("Combined2", TextureType.FixedUAV, MipLevel.L2);
            _combined3 = new RTHandle("Combined3", TextureType.FixedUAV, MipLevel.L3);

            _result = new RTHandle("AmbientOcclusion", TextureType.FixedUAV, MipLevel.Original);
        }

        // Command buffers
        if (_renderCommand == null)
        {
            _renderCommand = new CommandBuffer();
            _renderCommand.name = "<Nigiri> Occlusion";

            _compositeCommand = new CommandBuffer();
            _compositeCommand.name = "<Nigiri> Occlusion Composition";
        }

        // Materials
        if (_blitMaterial == null)
        {
            _blitMaterial = new Material(_blitShader)
            {
                hideFlags = HideFlags.DontSave
            };
        }
    }

    void RebuildCommandBuffers()
    {
        UnregisterCommandBuffers();

        // Update the base dimensions and reallocate static RTs.
        RTHandle.SetBaseDimensions(
            localCam.pixelWidth * (singlePassStereoEnabled ? 2 : 1),
            localCam.pixelHeight
        );

        _result.AllocateNow(localCam.stereoEnabled);

        // Rebuild the render commands.
        _renderCommand.Clear();

        _renderCommand.BeginSample("AO_Render");

        PushDownsampleCommands(_renderCommand);

        _occlusion1.PushAllocationCommand(_renderCommand);
        _occlusion2.PushAllocationCommand(_renderCommand);
        _occlusion3.PushAllocationCommand(_renderCommand);
        _occlusion4.PushAllocationCommand(_renderCommand);

        var tanHalfFovH = CalculateTanHalfFovHeight();
        PushRenderCommands(_renderCommand, _tiledDepth1, _occlusion1, tanHalfFovH);
        PushRenderCommands(_renderCommand, _tiledDepth2, _occlusion2, tanHalfFovH);
        PushRenderCommands(_renderCommand, _tiledDepth3, _occlusion3, tanHalfFovH);
        PushRenderCommands(_renderCommand, _tiledDepth4, _occlusion4, tanHalfFovH);

        _combined1.PushAllocationCommand(_renderCommand);
        _combined2.PushAllocationCommand(_renderCommand);
        _combined3.PushAllocationCommand(_renderCommand);

        PushUpsampleCommands(_renderCommand, _lowDepth4, _occlusion4, _lowDepth3, _occlusion3, _combined3);
        PushUpsampleCommands(_renderCommand, _lowDepth3, _combined3, _lowDepth2, _occlusion2, _combined2);
        PushUpsampleCommands(_renderCommand, _lowDepth2, _combined2, _lowDepth1, _occlusion1, _combined1);
        PushUpsampleCommands(_renderCommand, _lowDepth1, _combined1, _linearDepth, null, _result);

        if (_debug > 0) PushDebugBlitCommands(_renderCommand);

        _renderCommand.EndSample("AO_Render");

        // Rebuild the composite commands.
        _compositeCommand.Clear();
        _compositeCommand.BeginSample("AO_Composite");
        PushCompositeCommands(_compositeCommand);
        _compositeCommand.EndSample("AO_Composite");

        RegisterCommandBuffers();
    }

    #endregion

    #region Utilities for command buffer builders

    bool CheckIfResolvedDepthAvailable()
    {
        // AFAIK, resolved depth is only available on D3D11/12.
        // TODO: Is there more proper way to determine this?
        var rpath = localCam.actualRenderingPath;
        var gtype = SystemInfo.graphicsDeviceType;
        return rpath == RenderingPath.DeferredShading &&
              (gtype == GraphicsDeviceType.Direct3D11 ||
               gtype == GraphicsDeviceType.Direct3D12 ||
               gtype == GraphicsDeviceType.XboxOne);
    }

    // Calculate values in _ZBuferParams (built-in shader variable)
    // We can't use _ZBufferParams in compute shaders, so this function is
    // used to give the values in it to compute shaders.
    Vector4 CalculateZBufferParams()
    {
        var fpn = localCam.farClipPlane / localCam.nearClipPlane;
        if (SystemInfo.usesReversedZBuffer)
            return new Vector4(fpn - 1, 1, 0, 0);
        else
            return new Vector4(1 - fpn, fpn, 0, 0);
    }

    float CalculateTanHalfFovHeight()
    {
        return 1 / localCam.projectionMatrix[0, 0];
    }

    // The arrays below are reused between frames to reduce GC allocation.

    static readonly float[] SampleThickness = {
            Mathf.Sqrt(1 - 0.2f * 0.2f),
            Mathf.Sqrt(1 - 0.4f * 0.4f),
            Mathf.Sqrt(1 - 0.6f * 0.6f),
            Mathf.Sqrt(1 - 0.8f * 0.8f),
            Mathf.Sqrt(1 - 0.2f * 0.2f - 0.2f * 0.2f),
            Mathf.Sqrt(1 - 0.2f * 0.2f - 0.4f * 0.4f),
            Mathf.Sqrt(1 - 0.2f * 0.2f - 0.6f * 0.6f),
            Mathf.Sqrt(1 - 0.2f * 0.2f - 0.8f * 0.8f),
            Mathf.Sqrt(1 - 0.4f * 0.4f - 0.4f * 0.4f),
            Mathf.Sqrt(1 - 0.4f * 0.4f - 0.6f * 0.6f),
            Mathf.Sqrt(1 - 0.4f * 0.4f - 0.8f * 0.8f),
            Mathf.Sqrt(1 - 0.6f * 0.6f - 0.6f * 0.6f)
        };

    static float[] InvThicknessTable = new float[12];
    static float[] SampleWeightTable = new float[12];

    static RenderTargetIdentifier[] _mrtComposite = {
            BuiltinRenderTextureType.GBuffer0,    // Albedo, Occ
            BuiltinRenderTextureType.CameraTarget // Ambient
        };

    #endregion

    #region Command buffer builders

    void PushDownsampleCommands(CommandBuffer cmd)
    {
        // Make a copy of the depth texture, or reuse the resolved depth
        // buffer (it's only available in some specific situations).
        var useDepthCopy = !CheckIfResolvedDepthAvailable();
        if (useDepthCopy)
        {
            _depthCopy.PushAllocationCommand(cmd);
            cmd.SetRenderTarget(_depthCopy.id);
            cmd.DrawProcedural(Matrix4x4.identity, _blitMaterial, 0, MeshTopology.Triangles, 3);
        }

        // Temporary buffer allocations.
        _linearDepth.PushAllocationCommand(cmd);
        _lowDepth1.PushAllocationCommand(cmd);
        _lowDepth2.PushAllocationCommand(cmd);
        _lowDepth3.PushAllocationCommand(cmd);
        _lowDepth4.PushAllocationCommand(cmd);
        _tiledDepth1.PushAllocationCommand(cmd);
        _tiledDepth2.PushAllocationCommand(cmd);
        _tiledDepth3.PushAllocationCommand(cmd);
        _tiledDepth4.PushAllocationCommand(cmd);

        // 1st downsampling pass.
        var cs = _downsample1Compute;
        var kernel = cs.FindKernel("main");

        cmd.SetComputeTextureParam(cs, kernel, "LinearZ", _linearDepth.id);
        cmd.SetComputeTextureParam(cs, kernel, "DS2x", _lowDepth1.id);
        cmd.SetComputeTextureParam(cs, kernel, "DS4x", _lowDepth2.id);
        cmd.SetComputeTextureParam(cs, kernel, "DS2xAtlas", _tiledDepth1.id);
        cmd.SetComputeTextureParam(cs, kernel, "DS4xAtlas", _tiledDepth2.id);
        cmd.SetComputeVectorParam(cs, "ZBufferParams", CalculateZBufferParams());

        if (useDepthCopy)
            cmd.SetComputeTextureParam(cs, kernel, "Depth", _depthCopy.id);
        else
            cmd.SetComputeTextureParam(cs, kernel, "Depth", BuiltinRenderTextureType.ResolvedDepth);

        cmd.DispatchCompute(cs, kernel, _tiledDepth2.width, _tiledDepth2.height, 1);

        if (useDepthCopy) cmd.ReleaseTemporaryRT(_depthCopy.nameID);

        // 2nd downsampling pass.
        cs = _downsample2Compute;
        kernel = cs.FindKernel("main");

        cmd.SetComputeTextureParam(cs, kernel, "DS4x", _lowDepth2.id);
        cmd.SetComputeTextureParam(cs, kernel, "DS8x", _lowDepth3.id);
        cmd.SetComputeTextureParam(cs, kernel, "DS16x", _lowDepth4.id);
        cmd.SetComputeTextureParam(cs, kernel, "DS8xAtlas", _tiledDepth3.id);
        cmd.SetComputeTextureParam(cs, kernel, "DS16xAtlas", _tiledDepth4.id);

        cmd.DispatchCompute(cs, kernel, _tiledDepth4.width, _tiledDepth4.height, 1);
    }

    void PushRenderCommands(CommandBuffer cmd, RTHandle source, RTHandle dest, float TanHalfFovH)
    {
        // Here we compute multipliers that convert the center depth value into (the reciprocal of)
        // sphere thicknesses at each sample location.  This assumes a maximum sample radius of 5
        // units, but since a sphere has no thickness at its extent, we don't need to sample that far
        // out.  Only samples whole integer offsets with distance less than 25 are used.  This means
        // that there is no sample at (3, 4) because its distance is exactly 25 (and has a thickness of 0.)

        // The shaders are set up to sample a circular region within a 5-pixel radius.
        const float ScreenspaceDiameter = 10;

        // SphereDiameter = CenterDepth * ThicknessMultiplier.  This will compute the thickness of a sphere centered
        // at a specific depth.  The ellipsoid scale can stretch a sphere into an ellipsoid, which changes the
        // characteristics of the AO.
        // TanHalfFovH:  Radius of sphere in depth units if its center lies at Z = 1
        // ScreenspaceDiameter:  Diameter of sample sphere in pixel units
        // ScreenspaceDiameter / BufferWidth:  Ratio of the screen width that the sphere actually covers
        // Note about the "2.0f * ":  Diameter = 2 * Radius
        var ThicknessMultiplier = 2 * TanHalfFovH * ScreenspaceDiameter / source.width;
        if (!source.isTiled) ThicknessMultiplier *= 2;
        if (singlePassStereoEnabled) ThicknessMultiplier *= 2;

        // This will transform a depth value from [0, thickness] to [0, 1].
        var InverseRangeFactor = 1 / ThicknessMultiplier;

        // The thicknesses are smaller for all off-center samples of the sphere.  Compute thicknesses relative
        // to the center sample.
        for (var i = 0; i < 12; i++)
            InvThicknessTable[i] = InverseRangeFactor / SampleThickness[i];

        // These are the weights that are multiplied against the samples because not all samples are
        // equally important.  The farther the sample is from the center location, the less they matter.
        // We use the thickness of the sphere to determine the weight.  The scalars in front are the number
        // of samples with this weight because we sum the samples together before multiplying by the weight,
        // so as an aggregate all of those samples matter more.  After generating this table, the weights
        // are normalized.
        SampleWeightTable[0] = 4 * SampleThickness[0];    // Axial
        SampleWeightTable[1] = 4 * SampleThickness[1];    // Axial
        SampleWeightTable[2] = 4 * SampleThickness[2];    // Axial
        SampleWeightTable[3] = 4 * SampleThickness[3];    // Axial
        SampleWeightTable[4] = 4 * SampleThickness[4];    // Diagonal
        SampleWeightTable[5] = 8 * SampleThickness[5];    // L-shaped
        SampleWeightTable[6] = 8 * SampleThickness[6];    // L-shaped
        SampleWeightTable[7] = 8 * SampleThickness[7];    // L-shaped
        SampleWeightTable[8] = 4 * SampleThickness[8];    // Diagonal
        SampleWeightTable[9] = 8 * SampleThickness[9];    // L-shaped
        SampleWeightTable[10] = 8 * SampleThickness[10];    // L-shaped
        SampleWeightTable[11] = 4 * SampleThickness[11];    // Diagonal

        // Zero out the unused samples.
        // FIXME: should we support SAMPLE_EXHAUSTIVELY mode?
        SampleWeightTable[0] = 0;
        SampleWeightTable[2] = 0;
        SampleWeightTable[5] = 0;
        SampleWeightTable[7] = 0;
        SampleWeightTable[9] = 0;

        // Normalize the weights by dividing by the sum of all weights
        var totalWeight = 0.0f;

        foreach (var w in SampleWeightTable)
            totalWeight += w;

        for (var i = 0; i < SampleWeightTable.Length; i++)
            SampleWeightTable[i] /= totalWeight;

        // Set the arguments for the render kernel.
        var cs = _renderCompute;
        var kernel = cs.FindKernel("main_interleaved");

        cmd.SetComputeFloatParams(cs, "gInvThicknessTable", InvThicknessTable);
        cmd.SetComputeFloatParams(cs, "gSampleWeightTable", SampleWeightTable);
        cmd.SetComputeVectorParam(cs, "gInvSliceDimension", source.inverseDimensions);
        cmd.SetComputeFloatParam(cs, "gRejectFadeoff", -1 / _thicknessModifier);
        cmd.SetComputeFloatParam(cs, "gIntensity", _intensity);
        cmd.SetComputeTextureParam(cs, kernel, "DepthTex", source.id);
        cmd.SetComputeTextureParam(cs, kernel, "Occlusion", dest.id);

        // Calculate the thread group count and add a dispatch command with them.
        uint xsize, ysize, zsize;
        cs.GetKernelThreadGroupSizes(kernel, out xsize, out ysize, out zsize);

        cmd.DispatchCompute(
            cs, kernel,
            (source.width + (int)xsize - 1) / (int)xsize,
            (source.height + (int)ysize - 1) / (int)ysize,
            (source.depth + (int)zsize - 1) / (int)zsize
        );
    }

    void PushUpsampleCommands(
        CommandBuffer cmd,
        RTHandle lowResDepth, RTHandle interleavedAO,
        RTHandle highResDepth, RTHandle highResAO,
        RTHandle dest
    )
    {
        var cs = _upsampleCompute;
        var kernel = cs.FindKernel((highResAO == null) ? "main" : "main_blendout");

        var stepSize = 1920.0f / lowResDepth.width;
        var blurTolerance = 1 - Mathf.Pow(10, _blurTolerance) * stepSize;
        blurTolerance *= blurTolerance;
        var upsampleTolerance = Mathf.Pow(10, _upsampleTolerance);
        var noiseFilterWeight = 1 / (Mathf.Pow(10, _noiseFilterTolerance) + upsampleTolerance);

        cmd.SetComputeVectorParam(cs, "InvLowResolution", lowResDepth.inverseDimensions);
        cmd.SetComputeVectorParam(cs, "InvHighResolution", highResDepth.inverseDimensions);
        cmd.SetComputeFloatParam(cs, "NoiseFilterStrength", noiseFilterWeight);
        cmd.SetComputeFloatParam(cs, "StepSize", stepSize);
        cmd.SetComputeFloatParam(cs, "kBlurTolerance", blurTolerance);
        cmd.SetComputeFloatParam(cs, "kUpsampleTolerance", upsampleTolerance);

        cmd.SetComputeTextureParam(cs, kernel, "LoResDB", lowResDepth.id);
        cmd.SetComputeTextureParam(cs, kernel, "HiResDB", highResDepth.id);
        cmd.SetComputeTextureParam(cs, kernel, "LoResAO1", interleavedAO.id);

        if (highResAO != null)
            cmd.SetComputeTextureParam(cs, kernel, "HiResAO", highResAO.id);

        cmd.SetComputeTextureParam(cs, kernel, "AoResult", dest.id);

        var xcount = (highResDepth.width + 17) / 16;
        var ycount = (highResDepth.height + 17) / 16;
        cmd.DispatchCompute(cs, kernel, xcount, ycount, 1);
    }

    void PushDebugBlitCommands(CommandBuffer cmd)
    {
        var rt = _linearDepth; // Show linear depth by default.

        switch (_debug)
        {
            case 2: rt = _lowDepth1; break;
            case 3: rt = _lowDepth2; break;
            case 4: rt = _lowDepth3; break;
            case 5: rt = _lowDepth4; break;
            case 6: rt = _tiledDepth1; break;
            case 7: rt = _tiledDepth2; break;
            case 8: rt = _tiledDepth3; break;
            case 9: rt = _tiledDepth4; break;
            case 10: rt = _occlusion1; break;
            case 11: rt = _occlusion2; break;
            case 12: rt = _occlusion3; break;
            case 13: rt = _occlusion4; break;
            case 14: rt = _combined1; break;
            case 15: rt = _combined2; break;
            case 16: rt = _combined3; break;
        }

        if (rt.isTiled)
        {
            cmd.SetGlobalTexture("_TileTexture", rt.id);
            cmd.Blit(null, _result.id, _blitMaterial, 4);
        }
        else if (_debug < 17)
        {
            cmd.Blit(rt.id, _result.id);
        }
        // When _debug == 17, do nothing and show _result.
    }

    void PushCompositeCommands(CommandBuffer cmd)
    {
        cmd.SetGlobalTexture("_AOTexture", _result.id);

        if (_debug > 0)
        {
            cmd.Blit(_result.id, BuiltinRenderTextureType.CameraTarget, _blitMaterial, 3);
        }
        else if (ambientOnlyEnabled)
        {
            cmd.SetRenderTarget(_mrtComposite, BuiltinRenderTextureType.CameraTarget);
            cmd.DrawProcedural(Matrix4x4.identity, _blitMaterial, 1, MeshTopology.Triangles, 3);
        }
        else
        {
            cmd.Blit(null, BuiltinRenderTextureType.CameraTarget, _blitMaterial, 2);
        }
    }

    #endregion

    #region Exposed properties

    // These properties are simply exposed from the original MiniEngine
    // AO effect. Most of them are hidden in our inspector because they
    // are not useful nor user-friencly. If you want to try them out,
    // uncomment the first line of AmbientOcclusionEditor.cs.

    [Range(-8, 0)] private float _noiseFilterTolerance = 0;

    private float noiseFilterTolerance
    {
        get { return _noiseFilterTolerance; }
        set { _noiseFilterTolerance = value; }
    }

    [Range(-8, -1)] private float _blurTolerance = -4.6f;

    private float blurTolerance
    {
        get { return _blurTolerance; }
        set { _blurTolerance = value; }
    }

    [Range(-12, -1)] private float _upsampleTolerance = -12;

    private float upsampleTolerance
    {
        get { return _upsampleTolerance; }
        set { _upsampleTolerance = value; }
    }

    [Range(0, 17)] private int _debug;

    public bool ambientOnly
    {
        get { return _ambientOnly; }
        set { _ambientOnly = value; }
    }

    #endregion

    #region Built-in resources

    private ComputeShader _downsample1Compute;
    private ComputeShader _downsample2Compute;
    private ComputeShader _renderCompute;
    private ComputeShader _upsampleCompute;
    private Shader _blitShader;

    #endregion

    #region Detecting property changes

    float _noiseFilterToleranceOld;
    float _blurToleranceOld;
    float _upsampleToleranceOld;
    float _thicknessModifierOld;
    float _intensityOld;
    int _debugOld;

    bool CheckUpdate<T>(ref T oldValue, T current) where T : System.IComparable<T>
    {
        if (oldValue.CompareTo(current) != 0)
        {
            oldValue = current;
            return true;
        }
        else
        {
            return false;
        }
    }

    bool CheckPropertiesChanged()
    {
        return
            CheckUpdate(ref _noiseFilterToleranceOld, _noiseFilterTolerance) ||
            CheckUpdate(ref _blurToleranceOld, _blurTolerance) ||
            CheckUpdate(ref _upsampleToleranceOld, _upsampleTolerance) ||
            CheckUpdate(ref _thicknessModifierOld, _thicknessModifier) ||
            CheckUpdate(ref _intensityOld, _intensity) ||
            CheckUpdate(ref _debugOld, _debug);
    }

    #endregion

    #region Render texture handle class

    // Render Texture Handle (RTHandle) is a class for handling render
    // textures that are internally used in AO rendering. It provides a
    // transparent interface for both statically allocated RTs and
    // temporary RTs allocated from command buffers.

    internal enum MipLevel { Original, L1, L2, L3, L4, L5, L6 }

    internal enum TextureType
    {
        Fixed, Half, Float,                        // 2D render texture
        FixedUAV, HalfUAV, FloatUAV,               // Read/write enabled
        FixedTiledUAV, HalfTiledUAV, FloatTiledUAV // Texture array
    }

    internal class RTHandle
    {
        // Base dimensions (shared between handles)
        static int _baseWidth;
        static int _baseHeight;

        public static void SetBaseDimensions(int w, int h)
        {
            _baseWidth = w;
            _baseHeight = h;
        }

        public static bool CheckBaseDimensions(int w, int h)
        {
            return _baseWidth == w && _baseHeight == h;
        }

        // Public properties
        public int nameID { get { return _id; } }
        public int width { get { return _width; } }
        public int height { get { return _height; } }
        public int depth { get { return isTiled ? 16 : 1; } }
        public bool isTiled { get { return (int)_type > 5; } }
        public bool hasUAV { get { return (int)_type > 2; } }

        public RenderTargetIdentifier id
        {
            get
            {
                if (_rt != null)
                    return new RenderTargetIdentifier(_rt);
                else
                    return new RenderTargetIdentifier(_id);
            }
        }

        public Vector2 inverseDimensions
        {
            get { return new Vector2(1.0f / width, 1.0f / height); }
        }

        // Constructor
        public RTHandle(string name, TextureType type, MipLevel level)
        {
            _id = Shader.PropertyToID(name);
            _type = type;
            _level = level;
        }

        // Allocate the buffer in advance of use.
        public void AllocateNow(bool vrUsage)
        {
            CalculateDimensions();

            if (_rt == null)
            {
                // Initial allocation.
                _rt = new RenderTexture(
                    _width, _height, 0,
                    renderTextureFormat,
                    RenderTextureReadWrite.Linear
                );
                _rt.hideFlags = HideFlags.DontSave;
            }
            else
            {
                // Release and reallocate.
                _rt.Release();
                _rt.width = _width;
                _rt.height = _height;
                _rt.format = renderTextureFormat;
            }

            //if (vrUsage) _rt.vrUsage = XRSettings.eyeTextureDesc.vrUsage;
            _rt.filterMode = FilterMode.Point;
            _rt.enableRandomWrite = hasUAV;

            // Should it be tiled?
            if (isTiled)
            {
                _rt.dimension = TextureDimension.Tex2DArray;
                _rt.volumeDepth = depth;
            }

            _rt.Create();
        }

        // Push the allocation command to the given command buffer.
        public void PushAllocationCommand(CommandBuffer cmd)
        {
            CalculateDimensions();

            if (isTiled)
            {
                cmd.GetTemporaryRTArray(
                    _id, _width, _height, depth, 0,
                    FilterMode.Point, renderTextureFormat,
                    RenderTextureReadWrite.Linear, 1, hasUAV
                );
            }
            else
            {
                cmd.GetTemporaryRT(
                    _id, _width, _height, 0,
                    FilterMode.Point, renderTextureFormat,
                    RenderTextureReadWrite.Linear, 1, hasUAV
                );
            }
        }

        // Destroy internal objects.
        public void Destroy()
        {
            if (_rt != null)
            {
                if (Application.isPlaying)
                    RenderTexture.Destroy(_rt);
                else
                    RenderTexture.DestroyImmediate(_rt);
            }
        }

        // Private variables
        int _id;
        RenderTexture _rt;
        int _width, _height;
        TextureType _type;
        MipLevel _level;

        // Determine the render texture format.
        RenderTextureFormat renderTextureFormat
        {
            get
            {
                switch ((int)_type % 3)
                {
                    case 0: return RenderTextureFormat.R8;
                    case 1: return RenderTextureFormat.RHalf;
                    default: return RenderTextureFormat.RFloat;
                }
            }
        }

        // Calculate width/height of the texture from the base dimensions.
        void CalculateDimensions()
        {
            var div = 1 << (int)_level;
            _width = (_baseWidth + (div - 1)) / div;
            _height = (_baseHeight + (div - 1)) / div;
        }
    }

    #endregion

    #region Internal objects

    int _drawCountPerFrame; // used to detect single-pass stereo

    RTHandle _depthCopy;
    RTHandle _linearDepth;
    RTHandle _lowDepth1;
    RTHandle _lowDepth2;
    RTHandle _lowDepth3;
    RTHandle _lowDepth4;
    RTHandle _tiledDepth1;
    RTHandle _tiledDepth2;
    RTHandle _tiledDepth3;
    RTHandle _tiledDepth4;
    RTHandle _occlusion1;
    RTHandle _occlusion2;
    RTHandle _occlusion3;
    RTHandle _occlusion4;
    RTHandle _combined1;
    RTHandle _combined2;
    RTHandle _combined3;
    RTHandle _result;

    CommandBuffer _renderCommand;
    CommandBuffer _compositeCommand;

    Material _blitMaterial;

    #endregion

    #region MonoBehaviour functions

    void LateUpdate()
    {
        //Occlusion
        DoLazyInitialization();

        // Check if we have to rebuild the command buffers.
        var rebuild = CheckPropertiesChanged();

        // Check if the screen size was changed from the previous frame.
        // We must rebuild the command buffers when it's changed.
        rebuild |= !RTHandle.CheckBaseDimensions(
            localCam.pixelWidth * (singlePassStereoEnabled ? 2 : 1),
            localCam.pixelHeight
        );

        // In edit mode, it's almost impossible to check up all the factors
        // that can affect AO, so we update them every frame.
        rebuild |= !Application.isPlaying;

        if (rebuild) RebuildCommandBuffers();

        _drawCountPerFrame = 0;
        ///
    }

    void OnPreRender()
    {
        //Occlusion
        _drawCountPerFrame++;
        ///

        //Volumetric Lighting
        if (renderVolumetricLighting)
        {
            // use very low value for near clip plane to simplify cone/frustum intersection
            Matrix4x4 proj = Matrix4x4.Perspective(localCam.fieldOfView, localCam.aspect, 0.01f, localCam.farClipPlane);

#if UNITY_2017_2_OR_NEWER
            if (UnityEngine.XR.XRSettings.enabled)
            {
                // when using VR override the used projection matrix
                proj = Camera.current.projectionMatrix;
            }
#endif

            proj = GL.GetGPUProjectionMatrix(proj, true);
            _viewProj = proj * localCam.worldToCameraMatrix;

            _preLightPass.Clear();

            bool dx11 = SystemInfo.graphicsShaderLevel > 40;

            if (Resolution == VolumtericResolution.Quarter)
            {
                Texture nullTexture = null;
                // down sample depth to half res
                _preLightPass.Blit(nullTexture, _halfDepthBuffer, _bilateralBlurMaterial, dx11 ? 4 : 10);
                // down sample depth to quarter res
                _preLightPass.Blit(nullTexture, _quarterDepthBuffer, _bilateralBlurMaterial, dx11 ? 6 : 11);

                _preLightPass.SetRenderTarget(_quarterVolumeLightTexture);
            }
            else if (Resolution == VolumtericResolution.Half)
            {
                Texture nullTexture = null;
                // down sample depth to half res
                _preLightPass.Blit(nullTexture, _halfDepthBuffer, _bilateralBlurMaterial, dx11 ? 4 : 10);

                _preLightPass.SetRenderTarget(_halfVolumeLightTexture);
            }
            else
            {
                _preLightPass.SetRenderTarget(_volumeLightTexture);
            }

            _preLightPass.ClearRenderTarget(false, true, new Color(0, 0, 0, 1));

            UpdateMaterialParameters();

            if (PreRenderEvent != null)
                PreRenderEvent(this, _viewProj);
        }
        ///
    }

    void OnDestroy()
    {
        if (_result != null)
        {
            _tiledDepth1.Destroy();
            _tiledDepth2.Destroy();
            _tiledDepth3.Destroy();
            _tiledDepth4.Destroy();
            _result.Destroy();
        }

        if (_renderCommand != null)
        {
            _renderCommand.Dispose();
            _compositeCommand.Dispose();
        }

        if (_blitMaterial != null)
        {
            if (Application.isPlaying)
                Destroy(_blitMaterial);
            else
                DestroyImmediate(_blitMaterial);
        }
    }

    #endregion


    #region Color
    //ColorSUITE
    // RGBM encoding.
    static Color EncodeRGBM(float r, float g, float b)
    {
        var a = Mathf.Max(Mathf.Max(r, g), Mathf.Max(b, 1e-6f));
        a = Mathf.Ceil(a * 255) / 255;
        return new Color(r / a, g / a, b / a, a);
    }

    // An analytical model of chromaticity of the standard illuminant, by Judd et al.
    // http://en.wikipedia.org/wiki/Standard_illuminant#Illuminant_series_D
    // Slightly modifed to adjust it with the D65 white point (x=0.31271, y=0.32902).
    static float StandardIlluminantY(float x)
    {
        return 2.87f * x - 3.0f * x * x - 0.27509507f;
    }

    // CIE xy chromaticity to CAT02 LMS.
    // http://en.wikipedia.org/wiki/LMS_color_space#CAT02
    static Vector3 CIExyToLMS(float x, float y)
    {
        var Y = 1.0f;
        var X = Y * x / y;
        var Z = Y * (1.0f - x - y) / y;

        var L = 0.7328f * X + 0.4296f * Y - 0.1624f * Z;
        var M = -0.7036f * X + 1.6975f * Y + 0.0061f * Z;
        var S = 0.0030f * X + 0.0136f * Y + 0.9834f * Z;

        return new Vector3(L, M, S);
    }


    // Set up the temporary assets.
    void Setup()
    {
        if (_colorMaterial == null)
        {
            if (colorShader == null) colorShader = Shader.Find("Hidden/Nigiri_Color");
            _colorMaterial = new Material(colorShader);
            _colorMaterial.hideFlags = HideFlags.DontSave;
        }

        if (_lutTexture == null)
        {
            _lutTexture = new Texture2D(512, 1, TextureFormat.ARGB32, false, true);
            _lutTexture.hideFlags = HideFlags.DontSave;
            _lutTexture.wrapMode = TextureWrapMode.Clamp;
            UpdateLUT();
        }
    }

    // Update the LUT texture.
    void UpdateLUT()
    {
        for (var x = 0; x < _lutTexture.width; x++)
        {
            var u = 1.0f / (_lutTexture.width - 1) * x;
            var r = _cCurve.Evaluate(_rCurve.Evaluate(u));
            var g = _cCurve.Evaluate(_gCurve.Evaluate(u));
            var b = _cCurve.Evaluate(_bCurve.Evaluate(u));
            _lutTexture.SetPixel(x, 0, EncodeRGBM(r, g, b));
        }
        _lutTexture.Apply();
    }

    // Calculate the color balance coefficients.
    Vector3 CalculateColorBalance()
    {
        // Get the CIE xy chromaticity of the reference white point.
        // Note: 0.31271 = x value on the D65 white point
        var x = 0.31271f - _colorTemp * (_colorTemp < 0.0f ? 0.1f : 0.05f);
        var y = StandardIlluminantY(x) + _colorTint * 0.05f;

        // Calculate the coefficients in the LMS space.
        var w1 = new Vector3(0.949237f, 1.03542f, 1.08728f); // D65 white point
        var w2 = CIExyToLMS(x, y);
        return new Vector3(w1.x / w2.x, w1.y / w2.y, w1.z / w2.z);
    }

    #endregion

    #region Volumetric Light Rendering

    public enum VolumtericResolution
    {
        Full,
        Half,
        Quarter
    };

    public static event Action<Nigiri, Matrix4x4> PreRenderEvent;

    private static Mesh _pointLightMesh;
    private static Mesh _spotLightMesh;
    private static Material _lightMaterial;

    private CommandBuffer _preLightPass;

    private Matrix4x4 _viewProj;
    private Material _blitAddMaterial;
    private Material _bilateralBlurMaterial;

    private RenderTexture _volumeLightTexture;
    private RenderTexture _halfVolumeLightTexture;
    private RenderTexture _quarterVolumeLightTexture;
    private static Texture _defaultSpotCookie;

    private RenderTexture _halfDepthBuffer;
    private RenderTexture _quarterDepthBuffer;
    private VolumtericResolution _currentResolution = VolumtericResolution.Half;
    private Texture2D _ditheringTexture;
    private Texture3D _noiseTexture;

    public CommandBuffer GlobalCommandBuffer { get { return _preLightPass; } }

    public static Material GetLightMaterial()
    {
        return _lightMaterial;
    }

    public static Mesh GetPointLightMesh()
    {
        return _pointLightMesh;
    }

    public static Mesh GetSpotLightMesh()
    {
        return _spotLightMesh;
    }

    public RenderTexture GetVolumeLightBuffer()
    {
        if (Resolution == VolumtericResolution.Quarter)
            return _quarterVolumeLightTexture;
        else if (Resolution == VolumtericResolution.Half)
            return _halfVolumeLightTexture;
        else
            return _volumeLightTexture;
    }

    public RenderTexture GetVolumeLightDepthBuffer()
    {
        if (Resolution == VolumtericResolution.Quarter)
            return _quarterDepthBuffer;
        else if (Resolution == VolumtericResolution.Half)
            return _halfDepthBuffer;
        else
            return null;
    }

    public static Texture GetDefaultSpotCookie()
    {
        return _defaultSpotCookie;
    }

    void ChangeResolution()
    {
        int width = localCam.pixelWidth;
        int height = localCam.pixelHeight;

        if (_volumeLightTexture != null)
            DestroyImmediate(_volumeLightTexture);

        _volumeLightTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Default);
        //_volumeLightTexture.vrUsage = XRSettings.eyeTextureDesc.vrUsage;

        if (localCam.stereoEnabled)
        {
            _volumeLightTexture.vrUsage = VRTextureUsage.TwoEyes;

        }

        _volumeLightTexture.name = "VolumeLightBuffer";
        _volumeLightTexture.filterMode = FilterMode.Bilinear;

        if (_halfDepthBuffer != null)
            DestroyImmediate(_halfDepthBuffer);
        if (_halfVolumeLightTexture != null)
            DestroyImmediate(_halfVolumeLightTexture);

        if (Resolution == VolumtericResolution.Half || Resolution == VolumtericResolution.Quarter)
        {
            _halfVolumeLightTexture = new RenderTexture(width / 2, height / 2, 0, RenderTextureFormat.ARGBHalf);
            //_halfVolumeLightTexture.vrUsage = XRSettings.eyeTextureDesc.vrUsage;

            _halfVolumeLightTexture.name = "VolumeLightBufferHalf";
            _halfVolumeLightTexture.filterMode = FilterMode.Bilinear;

            _halfDepthBuffer = new RenderTexture(width / 2, height / 2, 0, RenderTextureFormat.RFloat);
            //_halfDepthBuffer.vrUsage = XRSettings.eyeTextureDesc.vrUsage;

            if (localCam.stereoEnabled)
            {
                _halfVolumeLightTexture.vrUsage = VRTextureUsage.TwoEyes;
                _halfDepthBuffer.vrUsage = VRTextureUsage.TwoEyes;

            }
            _halfDepthBuffer.name = "VolumeLightHalfDepth";
            _halfDepthBuffer.Create();
            _halfDepthBuffer.filterMode = FilterMode.Point;
        }

        if (_quarterVolumeLightTexture != null)
            DestroyImmediate(_quarterVolumeLightTexture);
        if (_quarterDepthBuffer != null)
            DestroyImmediate(_quarterDepthBuffer);

        if (Resolution == VolumtericResolution.Quarter)
        {
            _quarterVolumeLightTexture = new RenderTexture(width / 4, height / 4, 0, RenderTextureFormat.ARGBHalf);
            //_quarterVolumeLightTexture.vrUsage = XRSettings.eyeTextureDesc.vrUsage;

            _quarterVolumeLightTexture.name = "VolumeLightBufferQuarter";
            _quarterVolumeLightTexture.filterMode = FilterMode.Bilinear;

            _quarterDepthBuffer = new RenderTexture(width / 4, height / 4, 0, RenderTextureFormat.RFloat);
            //_quarterDepthBuffer.vrUsage = XRSettings.eyeTextureDesc.vrUsage;
            if (localCam.stereoEnabled)
            {
                _quarterVolumeLightTexture.vrUsage = VRTextureUsage.TwoEyes;
                _quarterDepthBuffer.vrUsage = VRTextureUsage.TwoEyes;


            }
            _quarterDepthBuffer.name = "VolumeLightQuarterDepth";
            _quarterDepthBuffer.Create();
            _quarterDepthBuffer.filterMode = FilterMode.Point;
        }
    }

    private void UpdateMaterialParameters()
    {
        _bilateralBlurMaterial.SetTexture("_HalfResDepthBuffer", _halfDepthBuffer);
        _bilateralBlurMaterial.SetTexture("_HalfResColor", _halfVolumeLightTexture);
        _bilateralBlurMaterial.SetTexture("_QuarterResDepthBuffer", _quarterDepthBuffer);
        _bilateralBlurMaterial.SetTexture("_QuarterResColor", _quarterVolumeLightTexture);

        Shader.SetGlobalTexture("_DitherTexture", _ditheringTexture);
        Shader.SetGlobalTexture("_NoiseTexture", _noiseTexture);
    }

    void LoadNoise3dTexture()
    {
        // basic dds loader for 3d texture - !not very robust!

        TextAsset data = Resources.Load("NoiseVolume") as TextAsset;

        byte[] bytes = data.bytes;

        uint height = BitConverter.ToUInt32(data.bytes, 12);
        uint width = BitConverter.ToUInt32(data.bytes, 16);
        uint pitch = BitConverter.ToUInt32(data.bytes, 20);
        uint depth = BitConverter.ToUInt32(data.bytes, 24);
        uint formatFlags = BitConverter.ToUInt32(data.bytes, 20 * 4);
        //uint fourCC = BitConverter.ToUInt32(data.bytes, 21 * 4);
        uint bitdepth = BitConverter.ToUInt32(data.bytes, 22 * 4);
        if (bitdepth == 0)
            bitdepth = pitch / width * 8;


        // doesn't work with TextureFormat.Alpha8 for some reason
        _noiseTexture = new Texture3D((int)width, (int)height, (int)depth, TextureFormat.RGBA32, false);
        _noiseTexture.name = "3D Noise";

        Color[] c = new Color[width * height * depth];

        uint index = 128;
        if (data.bytes[21 * 4] == 'D' && data.bytes[21 * 4 + 1] == 'X' && data.bytes[21 * 4 + 2] == '1' && data.bytes[21 * 4 + 3] == '0' &&
            (formatFlags & 0x4) != 0)
        {
            uint format = BitConverter.ToUInt32(data.bytes, (int)index);
            if (format >= 60 && format <= 65)
                bitdepth = 8;
            else if (format >= 48 && format <= 52)
                bitdepth = 16;
            else if (format >= 27 && format <= 32)
                bitdepth = 32;

            //Debug.Log("DXGI format: " + format);
            // dx10 format, skip dx10 header
            //Debug.Log("DX10 format");
            index += 20;
        }

        uint byteDepth = bitdepth / 8;
        pitch = (width * bitdepth + 7) / 8;

        for (int d = 0; d < depth; ++d)
        {
            //index = 128;
            for (int h = 0; h < height; ++h)
            {
                for (int w = 0; w < width; ++w)
                {
                    float v = (bytes[index + w * byteDepth] / 255.0f);
                    c[w + h * width + d * width * height] = new Color(v, v, v, v);
                }

                index += pitch;
            }
        }

        _noiseTexture.SetPixels(c);
        _noiseTexture.Apply();
    }

    private void GenerateDitherTexture()
    {
        if (_ditheringTexture != null)
        {
            return;
        }

        int size = 8;
#if DITHER_4_4
        size = 4;
#endif
        // again, I couldn't make it work with Alpha8
        _ditheringTexture = new Texture2D(size, size, TextureFormat.Alpha8, false, true);
        _ditheringTexture.filterMode = FilterMode.Point;
        Color32[] c = new Color32[size * size];

        byte b;
#if DITHER_4_4
        b = (byte)(0.0f / 16.0f * 255); c[0] = new Color32(b, b, b, b);
        b = (byte)(8.0f / 16.0f * 255); c[1] = new Color32(b, b, b, b);
        b = (byte)(2.0f / 16.0f * 255); c[2] = new Color32(b, b, b, b);
        b = (byte)(10.0f / 16.0f * 255); c[3] = new Color32(b, b, b, b);

        b = (byte)(12.0f / 16.0f * 255); c[4] = new Color32(b, b, b, b);
        b = (byte)(4.0f / 16.0f * 255); c[5] = new Color32(b, b, b, b);
        b = (byte)(14.0f / 16.0f * 255); c[6] = new Color32(b, b, b, b);
        b = (byte)(6.0f / 16.0f * 255); c[7] = new Color32(b, b, b, b);

        b = (byte)(3.0f / 16.0f * 255); c[8] = new Color32(b, b, b, b);
        b = (byte)(11.0f / 16.0f * 255); c[9] = new Color32(b, b, b, b);
        b = (byte)(1.0f / 16.0f * 255); c[10] = new Color32(b, b, b, b);
        b = (byte)(9.0f / 16.0f * 255); c[11] = new Color32(b, b, b, b);

        b = (byte)(15.0f / 16.0f * 255); c[12] = new Color32(b, b, b, b);
        b = (byte)(7.0f / 16.0f * 255); c[13] = new Color32(b, b, b, b);
        b = (byte)(13.0f / 16.0f * 255); c[14] = new Color32(b, b, b, b);
        b = (byte)(5.0f / 16.0f * 255); c[15] = new Color32(b, b, b, b);
#else
        int i = 0;
        b = (byte)(1.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(49.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(13.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(61.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(4.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(52.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(16.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(64.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);

        b = (byte)(33.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(17.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(45.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(29.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(36.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(20.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(48.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(32.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);

        b = (byte)(9.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(57.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(5.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(53.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(12.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(60.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(8.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(56.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);

        b = (byte)(41.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(25.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(37.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(21.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(44.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(28.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(40.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(24.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);

        b = (byte)(3.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(51.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(15.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(63.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(2.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(50.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(14.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(62.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);

        b = (byte)(35.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(19.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(47.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(31.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(34.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(18.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(46.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(30.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);

        b = (byte)(11.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(59.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(7.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(55.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(10.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(58.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(6.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(54.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);

        b = (byte)(43.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(27.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(39.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(23.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(42.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(26.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(38.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(22.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
#endif

        _ditheringTexture.SetPixels32(c);
        _ditheringTexture.Apply();
    }

    private Mesh CreateSpotLightMesh()
    {
        // copy & pasted from other project, the geometry is too complex, should be simplified
        Mesh mesh = new Mesh();

        const int segmentCount = 16;
        Vector3[] vertices = new Vector3[2 + segmentCount * 3];
        Color32[] colors = new Color32[2 + segmentCount * 3];

        vertices[0] = new Vector3(0, 0, 0);
        vertices[1] = new Vector3(0, 0, 1);

        float angle = 0;
        float step = Mathf.PI * 2.0f / segmentCount;
        float ratio = 0.9f;

        for (int i = 0; i < segmentCount; ++i)
        {
            vertices[i + 2] = new Vector3(-Mathf.Cos(angle) * ratio, Mathf.Sin(angle) * ratio, ratio);
            colors[i + 2] = new Color32(255, 255, 255, 255);
            vertices[i + 2 + segmentCount] = new Vector3(-Mathf.Cos(angle), Mathf.Sin(angle), 1);
            colors[i + 2 + segmentCount] = new Color32(255, 255, 255, 0);
            vertices[i + 2 + segmentCount * 2] = new Vector3(-Mathf.Cos(angle) * ratio, Mathf.Sin(angle) * ratio, 1);
            colors[i + 2 + segmentCount * 2] = new Color32(255, 255, 255, 255);
            angle += step;
        }

        mesh.vertices = vertices;
        mesh.colors32 = colors;

        int[] indices = new int[segmentCount * 3 * 2 + segmentCount * 6 * 2];
        int index = 0;

        for (int i = 2; i < segmentCount + 1; ++i)
        {
            indices[index++] = 0;
            indices[index++] = i;
            indices[index++] = i + 1;
        }

        indices[index++] = 0;
        indices[index++] = segmentCount + 1;
        indices[index++] = 2;

        for (int i = 2; i < segmentCount + 1; ++i)
        {
            indices[index++] = i;
            indices[index++] = i + segmentCount;
            indices[index++] = i + 1;

            indices[index++] = i + 1;
            indices[index++] = i + segmentCount;
            indices[index++] = i + segmentCount + 1;
        }

        indices[index++] = 2;
        indices[index++] = 1 + segmentCount;
        indices[index++] = 2 + segmentCount;

        indices[index++] = 2 + segmentCount;
        indices[index++] = 1 + segmentCount;
        indices[index++] = 1 + segmentCount + segmentCount;

        //------------
        for (int i = 2 + segmentCount; i < segmentCount + 1 + segmentCount; ++i)
        {
            indices[index++] = i;
            indices[index++] = i + segmentCount;
            indices[index++] = i + 1;

            indices[index++] = i + 1;
            indices[index++] = i + segmentCount;
            indices[index++] = i + segmentCount + 1;
        }

        indices[index++] = 2 + segmentCount;
        indices[index++] = 1 + segmentCount * 2;
        indices[index++] = 2 + segmentCount * 2;

        indices[index++] = 2 + segmentCount * 2;
        indices[index++] = 1 + segmentCount * 2;
        indices[index++] = 1 + segmentCount * 3;

        ////-------------------------------------
        for (int i = 2 + segmentCount * 2; i < segmentCount * 3 + 1; ++i)
        {
            indices[index++] = 1;
            indices[index++] = i + 1;
            indices[index++] = i;
        }

        indices[index++] = 1;
        indices[index++] = 2 + segmentCount * 2;
        indices[index++] = segmentCount * 3 + 1;

        mesh.triangles = indices;
        mesh.RecalculateBounds();

        return mesh;
    }
}
#endregion

