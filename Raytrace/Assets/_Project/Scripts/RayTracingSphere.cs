using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RayTracingSphere : MonoBehaviour
{
    public Color Albedo;
    public Color Specular;
    [Range(0.0f, 1.0f)]
    public float Smoothness;
    public bool Emission;
    public Vector3 EmissionColor;

    public Sphere Sphere => mSphere;

    private Sphere mSphere;


    private void Start()
    {
        mSphere = new Sphere();
        mSphere.albedo = new Vector3(Albedo.r, Albedo.g, Albedo.b);
        mSphere.specular = new Vector3(Specular.r, Specular.g, Specular.b);
        mSphere.radius = transform.localScale.x / 2.0f;
        mSphere.position = transform.position;
        mSphere.smoothness = Smoothness;

        if (Emission)
        {
            mSphere.emission = new Vector3(EmissionColor.x, EmissionColor.y, EmissionColor.z);

            // Test
            //Color emission = Random.ColorHSV(0, 1, 0, 1, 3.0f, 8.0f);
            //EmissionColor.x = emission.r;
            //EmissionColor.y = emission.g;
            //EmissionColor.z = emission.b;
            //mSphere.emission = new Vector3(emission.r, emission.g, emission.b);
        }

        RayTracingMaster.Instance.RegisterDynamicSphere(this);
        RayTracingMaster.Instance.FrameRendered += OnFrameRendered;
    }

    private void OnFrameRendered(int frame)
    {
        mSphere.position = transform.position;
    }
}
