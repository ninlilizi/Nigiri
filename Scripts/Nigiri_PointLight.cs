using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Light))]
public class Nigiri_PointLight : MonoBehaviour {

    public int emissiveLayer;

    public Light lightComponent;
    public GameObject emissiveSphere;
    public Material emissiveMaterial;
    public Shader emissiveShader;

    public Color emissionColor;

    // Use this for initialization
    void OnEnable()
    {
        lightComponent = GetComponent<Light>();

        if (lightComponent == null)
        {
            Debug.Log("<Nigiri> Point Light. Light not found");
            enabled = false;
            return;
        }

        emissiveShader = Shader.Find("Standard");
        emissiveMaterial = new Material(emissiveShader);

        emissiveSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        emissiveSphere.hideFlags = HideFlags.HideAndDontSave;
        emissiveSphere.transform.parent = GetComponent<Transform>();
        emissiveSphere.transform.localPosition = new Vector3Int(0, 0, 0);

        emissiveSphere.transform.localScale = new Vector3(lightComponent.range, lightComponent.range, lightComponent.range);
        emissiveSphere.GetComponent<Renderer>().material = emissiveMaterial;

        emissiveMaterial.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        emissiveMaterial.enableInstancing = true;
        lightComponent.enabled = false;
    }   

    private void OnDisable()
    {
        lightComponent.enabled = true;

        if (emissiveSphere != null) GameObject.DestroyImmediate(emissiveSphere);
        if (emissiveMaterial != null) Material.DestroyImmediate(emissiveMaterial);
    }

    // Update is called once per frame
    void Update ()
    {
        emissiveSphere.layer = emissiveLayer;

        emissiveMaterial.SetColor("_EmissionColor", lightComponent.color * lightComponent.intensity);
        emissiveMaterial.SetColor("_Color", lightComponent.color);
        emissiveMaterial.EnableKeyword("_EMISSION");
    }
}
