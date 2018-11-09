using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NKGI : MonoBehaviour {

    public Shader shaderRSM;
    public Shader shaderRSMPost;
    public Material material_RSM;
    public Material material_RSMPost;

    // Use this for initialization
    void Start () {
        shaderRSM = Shader.Find("NKGI/Reflective Shadow Map");
        shaderRSMPost = Shader.Find("Hidden/NKGI_RSM_Post");
        material_RSM = new Material(shaderRSM);
        material_RSMPost = new Material(shaderRSMPost);
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

        // Render that camera to get the Reflective Shadow Map
        NKGI_RSMCamera.targetTexture = NKGI_RSM;
        NKGI_RSMCamera.SetReplacementShader(shaderRSM, "");
        NKGI_RSMCamera.Render();


        Shader.SetGlobalTexture("NKGI_RSM", NKGI_RSM);

        //Output /something/
        Graphics.Blit(NKGI_RSM, destination);
        //Graphics.Blit(source, destination, material_RSMPost);

        //Destroy Dummy Camera
        GameObject.DestroyImmediate(NKGI_RSMCameraGO);

        //Release Render Textures
        NKGI_RSM.Release();
    }
}
