using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RayTracingSphere : MonoBehaviour
{
    public Color Albedo;
    public Color Specular;
    public bool Emission;
    public Color EmissionColor;

    private Sphere mSphere;


    private void Start()
    {
        mSphere = new Sphere();
        mSphere.albedo = new Vector3(Albedo.r, Albedo.g, Albedo.b);
        mSphere.specular = new Vector3(Specular.r, Specular.g, Specular.b);
        mSphere.radius = transform.localScale.x / 2.0f;
        mSphere.position = transform.position;

        if (Emission)
        {
            mSphere.emission = new Vector3(EmissionColor.r, EmissionColor.g, EmissionColor.b);
        }

        RayTracingMaster.Instance.RegisterDynamicSphere(mSphere);
        RayTracingMaster.Instance.FrameRendered += OnFrameRendered;
    }

    private void OnFrameRendered(int frame)
    {
        mSphere.position = transform.position;
    }
}
