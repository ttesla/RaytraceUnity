using System.Collections.Generic;
using UnityEngine;

struct Sphere
{
    public Vector3 position;
    public float radius;
    public Vector3 albedo;
    public Vector3 specular;
};


public class RayTracingMaster : MonoBehaviour
{
    public ComputeShader RayTracingShader;
    public Texture SkyboxTexture;
    public Light DirectionalLight;

    [Header("Sampling")]
    public bool UseSampling;
    public Material SamplerMaterial;

    [Header("Spheres")]
    public Vector2 SphereRadius = new Vector2(3.0f, 8.0f);
    public uint SpheresMax = 100;
    public float SpherePlacementRadius = 100.0f;
    private ComputeBuffer mSphereBuffer;

    private RenderTexture mTarget;
    private Camera mCamera;

    private uint mCurrentSample = 0;
    

    private void Awake()
    {
        mCamera = GetComponent<Camera>();
    }

    private void Start()
    {
        SetUpScene();
    }

    private void Update()
    {
        if (UseSampling && transform.hasChanged)
        {
            mCurrentSample = 0;
            transform.hasChanged = false;

            //Debug.Log("Transform changed!");
        }
    }

    private void SetShaderParameters()
    {
        RayTracingShader.SetMatrix("_CameraToWorld", mCamera.cameraToWorldMatrix);
        RayTracingShader.SetMatrix("_CameraInverseProjection", mCamera.projectionMatrix.inverse);
        RayTracingShader.SetTexture(0, "_SkyboxTexture", SkyboxTexture);
        RayTracingShader.SetFloat("_Time", Time.time);

        Vector3 l = DirectionalLight.transform.forward;
        RayTracingShader.SetVector("_DirectionalLight", new Vector4(l.x, l.y, l.z, DirectionalLight.intensity));

        RayTracingShader.SetBuffer(0, "_Spheres", mSphereBuffer);

        if (UseSampling)
        {
            RayTracingShader.SetVector("_PixelOffset", new Vector2(Random.value / 2.0f, Random.value / 2.0f));
        }
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        SetShaderParameters();
        Render(destination);
    }

    private void Render(RenderTexture destination)
    {
        // Make sure we have a current render target
        InitRenderTexture();

        // Set the target and dispatch the compute shader
        int threadGroupsX = Mathf.CeilToInt(Screen.width / 8.0f);
        int threadGroupsY = Mathf.CeilToInt(Screen.height / 8.0f);
        //Debug.Log("TX: " + threadGroupsX);
        //Debug.Log("TY: " + threadGroupsY);
        RayTracingShader.Dispatch(0, threadGroupsX, threadGroupsY, 1);

        // Sampling...
        if (UseSampling) 
        {
            SamplerMaterial.SetFloat("_Sample", mCurrentSample);
            mCurrentSample++;

            Graphics.Blit(mTarget, destination, SamplerMaterial);
        }
        else 
        {
            Graphics.Blit(mTarget, destination);
        }
    }

    private void InitRenderTexture()
    {
        if (mTarget == null || mTarget.width != Screen.width || mTarget.height != Screen.height)
        {
            // Release render texture if we already have one
            if (mTarget != null)
                mTarget.Release();

            // Get a render target for Ray Tracing
            mTarget = new RenderTexture(Screen.width, Screen.height, 0,
                RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            mTarget.enableRandomWrite = true;
            mTarget.Create();

            // Set newly created target texture
            RayTracingShader.SetTexture(0, "_Result", mTarget);

            Debug.Log("Render Texture Re-Init!");
        }
    }


    private void SetUpScene()
    {
        List<Sphere> spheres = new List<Sphere>();
        
        // Add a number of random spheres
        for (int i = 0; i < SpheresMax; i++)
        {
            Sphere sphere = new Sphere();
           
            // Radius and radius
            sphere.radius = SphereRadius.x + Random.value * (SphereRadius.y - SphereRadius.x);
            Vector2 randomPos = Random.insideUnitCircle * SpherePlacementRadius;
            sphere.position = new Vector3(randomPos.x, sphere.radius, randomPos.y);
            // Reject spheres that are intersecting others

            if (!IsCollidingWithOtherSpheres(sphere, spheres)) 
            {
                // Albedo and specular color
                Color color = Random.ColorHSV();
                bool metal = Random.value < 0.5f;
                sphere.albedo = metal ? Vector3.zero : new Vector3(color.r, color.g, color.b);
                sphere.specular = metal ? new Vector3(color.r, color.g, color.b) : Vector3.one * 0.04f;
                // Add the sphere to the list
                spheres.Add(sphere);
            }
            else 
            {
                i--;
            }
        }

        // Assign to compute buffer
        mSphereBuffer = new ComputeBuffer(spheres.Count, 40);
        mSphereBuffer.SetData(spheres);
    }

    private bool IsCollidingWithOtherSpheres(Sphere sphere, List<Sphere> spheres)
    {
        foreach (Sphere other in spheres)
        {
            float minDist = sphere.radius + other.radius;
            if (Vector3.SqrMagnitude(sphere.position - other.position) < minDist * minDist)
            {
                return true;
            }
        }

        return false;
    }


    private void ReleaseBuffer()
    {
        if (mSphereBuffer != null) 
        {
            mSphereBuffer.Release();
        }
    }
}
