using System.Collections.Generic;
using System.Linq;
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

struct MeshObject
{
    public Matrix4x4 localToWorldMatrix;
    public int indices_offset;
    public int indices_count;
}


public class RayTracingMaster : MonoBehaviour
{
    public static RayTracingMaster Instance;

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

    private bool mMeshObjectsNeedRebuilding = false;
    private List<RayTracingObject> mRayTracingObjects = new List<RayTracingObject>();
    
    private List<MeshObject> mMeshObjects = new List<MeshObject>();
    private List<Vector3> mVertices       = new List<Vector3>();
    private List<int> mIndices            = new List<int>();
    private ComputeBuffer mMeshObjectBuffer;
    private ComputeBuffer mVertexBuffer;
    private ComputeBuffer mIndexBuffer;


    private void Awake()
    {
        mCamera = GetComponent<Camera>();
        Instance = this;
    }

    private void Start()
    {
        SetUpScene();
    }

    private void OnDisable()
    {
        mSphereBuffer?.Release();
        mMeshObjectBuffer?.Release();
        mVertexBuffer?.Release();
        mIndexBuffer?.Release();
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

  
    public void RegisterObject(RayTracingObject obj)
    {
        mRayTracingObjects.Add(obj);
        mMeshObjectsNeedRebuilding = true;
    }
    public void UnregisterObject(RayTracingObject obj)
    {
        mRayTracingObjects.Remove(obj);
        mMeshObjectsNeedRebuilding = true;
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

        SetComputeBuffer("_Spheres", mSphereBuffer);
        SetComputeBuffer("_MeshObjects", mMeshObjectBuffer);
        SetComputeBuffer("_Vertices", mVertexBuffer);
        SetComputeBuffer("_Indices", mIndexBuffer);

        if (UseSampling)
        {
            RayTracingShader.SetVector("_PixelOffset", new Vector2(Random.value / 2.0f, Random.value / 2.0f));
        }
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        RebuildMeshObjectBuffers();
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
        CreateComputeBuffer(ref mSphereBuffer, spheres, 56);
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

    private void RebuildMeshObjectBuffers()
    {
        if (!mMeshObjectsNeedRebuilding)
        {
            return;
        }

        mMeshObjectsNeedRebuilding = false;
        mCurrentSample = 0;
        // Clear all lists
        mMeshObjects.Clear();
        mVertices.Clear();
        mIndices.Clear();

        // Loop over all objects and gather their data
        foreach (RayTracingObject obj in mRayTracingObjects)
        {
            Mesh mesh = obj.GetComponent<MeshFilter>().sharedMesh;
            // Add vertex data
            int firstVertex = mVertices.Count;
            mVertices.AddRange(mesh.vertices);

            // Add index data - if the vertex buffer wasn't empty before, the
            // indices need to be offset
            int firstIndex = mIndices.Count;
            var indices = mesh.GetIndices(0);
            mIndices.AddRange(indices.Select(index => index + firstVertex));
            // Add the object itself
            mMeshObjects.Add(new MeshObject()
            {
                localToWorldMatrix = obj.transform.localToWorldMatrix,
                indices_offset = firstIndex,
                indices_count = indices.Length
            });
        }

        CreateComputeBuffer(ref mMeshObjectBuffer, mMeshObjects, 72);
        CreateComputeBuffer(ref mVertexBuffer, mVertices, 12);
        CreateComputeBuffer(ref mIndexBuffer, mIndices, 4);
    }

    private static void CreateComputeBuffer<T>(ref ComputeBuffer buffer, List<T> data, int stride)
    where T : struct
    {
        // Do we already have a compute buffer?
        if (buffer != null)
        {
            // If no data or buffer doesn't match the given criteria, release it
            if (data.Count == 0 || buffer.count != data.Count || buffer.stride != stride)
            {
                buffer.Release();
                buffer = null;
            }
        }

        if (data.Count != 0)
        {
            // If the buffer has been released or wasn't there to
            // begin with, create it
            if (buffer == null)
            {
                buffer = new ComputeBuffer(data.Count, stride);
            }

            // Set data on the buffer
            buffer.SetData(data);
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
