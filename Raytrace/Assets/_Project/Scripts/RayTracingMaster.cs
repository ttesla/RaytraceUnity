using System.Collections.Generic;
using UnityEngine;
public class RayTracingMaster : MonoBehaviour
{
    public ComputeShader RayTracingShader;
    public Texture SkyboxTexture;

    [Header("Spheres")]
    public int SphereSeed;
    public Vector2 SphereRadius = new Vector2(3.0f, 8.0f);
    public uint SpheresMax = 100;
    public float SpherePlacementRadius = 100.0f;

    private ComputeBuffer _sphereBuffer;
    private RenderTexture mTarget;
    private Camera mCamera;
    private uint mCurrentSample = 0;
    private Material mAddMaterial;

    struct Sphere
    {
        public Vector3 position;
        public float radius;
        public Vector3 albedo;
        public Vector3 specular;
        public float smoothness;
        public Vector3 emission;
    }

    void Awake() 
    {
        mCamera = GetComponent<Camera>();
        InitRenderTexture();
    }

    private void OnEnable()
    {
        mCurrentSample = 0;
        SetUpScene();
    }

    private void OnDisable()
    {
        _sphereBuffer?.Release();
    }

    private void SetShaderParameters()
    {
        RayTracingShader.SetTexture(0, "_SkyboxTexture", SkyboxTexture);
        RayTracingShader.SetMatrix("_CameraToWorld", mCamera.cameraToWorldMatrix);
        RayTracingShader.SetMatrix("_CameraInverseProjection", mCamera.projectionMatrix.inverse);
        RayTracingShader.SetVector("_PixelOffset", new Vector2(Random.value, Random.value));
        RayTracingShader.SetFloat("_Seed", Random.value);

        RayTracingShader.SetBuffer(0, "_Spheres", _sphereBuffer);
    }

    private void InitRenderTexture()
    {
        if (mTarget == null || mTarget.width != Screen.width || mTarget.height != Screen.height)
        {
            // Release render texture if we already have one
            if (mTarget != null)
                mTarget.Release();

            // Get a render target for Ray Tracing
            mTarget = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            mTarget.enableRandomWrite = true;
            mTarget.Create();
        }
    }

    private void Update()
    {
        if (transform.hasChanged)
        {
            mCurrentSample = 0;
            transform.hasChanged = false;
        }
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        SetShaderParameters();
        Render(destination);
    }
    
    private void Render(RenderTexture destination)
    {
        // Set the target and dispatch the compute shader
        RayTracingShader.SetTexture(0, "_Result", mTarget);
        int threadGroupsX = Mathf.CeilToInt(Screen.width / 8.0f);
        int threadGroupsY = Mathf.CeilToInt(Screen.height / 8.0f);
        RayTracingShader.Dispatch(0, threadGroupsX, threadGroupsY, 1);

        // Blit the result texture to the screen
        if (mAddMaterial == null)
            mAddMaterial = new Material(Shader.Find("Hidden/AddShader"));

        mAddMaterial.SetFloat("_Sample", mCurrentSample);
        Graphics.Blit(mTarget, destination, mAddMaterial);
        mCurrentSample++;
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
            foreach (Sphere other in spheres)
            {
                float minDist = sphere.radius + other.radius;
                if (Vector3.SqrMagnitude(sphere.position - other.position) < minDist * minDist)
                    goto SkipSphere;
            }

            // Albedo and specular color
            Color color = Random.ColorHSV();
            float chance = Random.value;
            if (chance < 0.8f)
            {
                bool metal = chance < 0.4f;
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

        SkipSphere:
            continue;
        }

        // Assign to compute buffer
        if (_sphereBuffer != null)
            _sphereBuffer.Release();
        if (spheres.Count > 0)
        {
            _sphereBuffer = new ComputeBuffer(spheres.Count, 56);
            _sphereBuffer.SetData(spheres);
        }
    }


    private void SetComputeBuffer(string name, ComputeBuffer buffer)
    {
        if (buffer != null)
        {
            RayTracingShader.SetBuffer(0, name, buffer);
        }
    }
}
