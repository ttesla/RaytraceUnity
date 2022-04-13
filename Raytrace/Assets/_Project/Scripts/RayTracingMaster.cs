using System.Collections.Generic;
using UnityEngine;


struct Sphere
{
    public Vector3 position;
    public float radius;
    public Vector3 albedo;
    public Vector3 specular;
    public float smoothness;
    public Vector3 emission;
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
    public int SphereSeed;

    private ComputeBuffer mSphereBuffer;
    private RenderTexture mTarget;
    private RenderTexture mConverged;
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
        RayTracingShader.SetFloat("_Seed", Random.value);

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

            Graphics.Blit(mTarget, mConverged, SamplerMaterial);
            Graphics.Blit(mConverged, destination);

            //Graphics.Blit(mTarget, destination, SamplerMaterial);
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
            {
                mTarget.Release();
                mConverged.Release();
            }

            // Get a render target for Ray Tracing
            mTarget = new RenderTexture(Screen.width, Screen.height, 0,
                RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            mTarget.enableRandomWrite = true;
            mTarget.Create();

            // Get a render target for Ray Tracing
            mConverged = new RenderTexture(Screen.width, Screen.height, 0,
                RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            mConverged.enableRandomWrite = true;
            mConverged.Create();

            // Set newly created target texture
            RayTracingShader.SetTexture(0, "_Result", mTarget);

            // Reset sampling
            mCurrentSample = 0;

            Debug.Log("Render Texture Re-Init!");
        }
    }


    private void SetUpScene()
    {
        Random.InitState(SphereSeed);
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
                float chance = Random.value;
                if (chance < 0.7f)
                {
                    bool metal = chance < 0.6f;
                    sphere.albedo = metal ? Vector4.zero : new Vector4(color.r, color.g, color.b);
                    sphere.specular = metal ? new Vector4(color.r, color.g, color.b) : new Vector4(0.04f, 0.04f, 0.04f);
                    sphere.smoothness = Random.value;
                }
                else
                {
                    Color emission = Random.ColorHSV(0, 1, 0, 1, 3.0f, 8.0f);
                    sphere.emission = new Vector3(emission.r, emission.g, emission.b);
                }

                // Add the sphere to the list
                spheres.Add(sphere);
            }
        }

        // Suns
        //Sphere sun1 = new Sphere();
        //sun1.emission = new Vector3(1.0f, 1.0f, 0.2f);
        //sun1.position = new Vector3(-50, 100, 0);
        //sun1.radius = 50;

        //Sphere sun2 = new Sphere();
        //sun2.emission = new Vector3(1.0f, 1.0f, 0.2f);
        //sun2.position = new Vector3(50, 100, 0);
        //sun2.radius = 50;

        //spheres.Add(sun1);
        //spheres.Add(sun2);

        // Assign to compute buffer
        mSphereBuffer = new ComputeBuffer(spheres.Count, 56);
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
