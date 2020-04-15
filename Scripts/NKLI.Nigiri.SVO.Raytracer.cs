/// <summary>
/// NKLI     : Nigiri - SVO Raytracer
/// Copywrite: Abigail Sara Hocking of Newbury, 2020. 
/// Licence  : The Nigiri 'Bits and pieces' Licence. [v3]
/// </summary>

using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace NKLI.Nigiri.SVO
{
    public class Raytracer
    {
        // Read-only properties
        public bool Initialied { get; private set; }
        public Tree SVO_Tree { get; private set; }
        public float GI_Area_Size { get; private set; }

        public RenderTexture Texture_Output { get; private set; }

        private Light directionalLight;
        private Camera attachedCamera;

        //PRIVATE FIELDS
        private Camera scene_view_camera;
        private Vector3 last_camera_position;
        private Quaternion last_camera_rotation;
        private Matrix4x4 worldspace_frustum_corners;

        private ComputeShader Shader_Raytracer;
        private int groups_x;
        private int groups_y;
        private int path_tracing_kernel;

        private Material tonemap_blit;

        private RenderTexture hdr_rt;


        public Raytracer(Tree SVO, Camera _camera, Light _directionalLight, float giAreaSize)
        {
            try
            {
                // Load encode shader
                Shader_Raytracer = Resources.Load("NKLI_Nigiri_SVORaytracer") as ComputeShader;
                if (Shader_Raytracer == null) throw new Exception("[Nigiri] failed to load compute shader 'NKLI_Nigiri_SVORaytracer'");


                // Binds to SVO
                SVO_Tree = SVO;

                if (_camera != null)
                {
                    attachedCamera = _camera;
                }

                directionalLight = _directionalLight;


                // Sets values
                GI_Area_Size = giAreaSize;



                ////
                ///
                //Shader_Raytracer = Resources.Load<ComputeShader>("PathTracingCS");
                path_tracing_kernel = Shader_Raytracer.FindKernel("PathTrace_uniform_grid");

                Setup(attachedCamera);


                Initialied = true;
            }
            catch (Exception ex)
            {
                Debug.LogError("NKLI.Nigiri.SVO.Raytracer failed to initialize" + Environment.NewLine + ex.Message + Environment.NewLine + ex.StackTrace);
                Initialied = false;
            }
        }


        public RenderTexture Trace(RenderTexture source, RenderTexture positionTexture)
        {
            if (Initialied)
            {
                // Update logic
                if (attachedCamera.transform.position != last_camera_position || attachedCamera.transform.rotation != last_camera_rotation)
                {
                    ResetBuffer();
                }

                last_camera_position = attachedCamera.transform.position;
                last_camera_rotation = attachedCamera.transform.rotation;


                Vector3[] frustumCorners = new Vector3[4];
                scene_view_camera.CalculateFrustumCorners(new Rect(0, 0, 1, 1), scene_view_camera.farClipPlane, Camera.MonoOrStereoscopicEye.Mono, frustumCorners);
                worldspace_frustum_corners.SetRow(0, scene_view_camera.transform.TransformVector(frustumCorners[0]));
                worldspace_frustum_corners.SetRow(1, scene_view_camera.transform.TransformVector(frustumCorners[1]));
                worldspace_frustum_corners.SetRow(2, scene_view_camera.transform.TransformVector(frustumCorners[3]));
                worldspace_frustum_corners.SetRow(3, scene_view_camera.transform.TransformVector(frustumCorners[2]));
                Shader_Raytracer.SetMatrix("worldspace_frustum_corners", worldspace_frustum_corners);
                Shader_Raytracer.SetVector("camera_position", scene_view_camera.transform.position);

                Shader_Raytracer.SetFloat("_giAreaSize", GI_Area_Size);
                Shader_Raytracer.SetBuffer(0, "_SVO", SVO_Tree.Buffer_SVO);

                Shader_Raytracer.SetTexture(path_tracing_kernel, "output", hdr_rt);

                int random_seed = Random.Range(0, int.MaxValue / 100);
                Shader_Raytracer.SetInt("start_seed", random_seed);

                Shader_Raytracer.Dispatch(path_tracing_kernel, groups_x, groups_y, 1);

                //Graphics.Blit(hdr_rt, Texture_Output, tonemap_blit, 0);



                return hdr_rt;
            }

            return source;
        }

        public void Setup(Camera cam)
        {
            //I must call this function every time the viewport is resized, VERY IMPORTANT

            scene_view_camera = cam;

            Shader_Raytracer.SetVector("screen_size", new Vector4(cam.pixelRect.width, cam.pixelRect.height, 0, 0));

            groups_x = Mathf.CeilToInt(cam.pixelRect.width / 4.0f);
            groups_y = Mathf.CeilToInt(cam.pixelRect.height / 4.0f);

            tonemap_blit = new Material(Shader.Find("PathTracing/Tonemap"));

            hdr_rt = new RenderTexture((int)cam.pixelRect.width, (int)cam.pixelRect.height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            hdr_rt.enableRandomWrite = true;
            hdr_rt.Create();

            Texture_Output = new RenderTexture((int)cam.pixelRect.width, (int)cam.pixelRect.height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            Texture_Output.enableRandomWrite = true;
            Texture_Output.Create();
        }

        private void ResetBuffer()
        {
            RenderTexture old = RenderTexture.active;
            RenderTexture.active = hdr_rt;
            GL.Clear(false, true, Color.clear);
            RenderTexture.active = old;
        }

        /// <summary>
        /// Destructor
        /// </summary>
        public void Dispose()
        {
            if (hdr_rt) hdr_rt.Release();
            if (Texture_Output) Texture_Output.Release();
        }
    }
}
