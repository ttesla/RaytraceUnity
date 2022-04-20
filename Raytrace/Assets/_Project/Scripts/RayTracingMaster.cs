using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public struct Sphere
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

    [Header("Rendering")]
    public int MaxRenderCount;
    public float FrameRenderDelay;
    public bool Record;
    public event System.Action<int> FrameRendered;

    [Header("Sampling")]
    public bool UseSampling;
    public Material SamplerMaterial;

    private RenderTexture mTarget;
    private RenderTexture mConverged;
    private Camera mCamera;
    private uint mCurrentSample = 0;

    private bool mMeshObjectsNeedRebuilding = false;
    private List<RayTracingObject> mRayTracingObjects = new List<RayTracingObject>();
    private List<Sphere> mDynamicSpheres = new List<Sphere>();
    private List<Sphere> mStaticSpheres = new List<Sphere>();
    private List<MeshObject> mMeshObjects = new List<MeshObject>();
    private List<Vector3> mVertices       = new List<Vector3>();
    private List<int> mIndices            = new List<int>();
    private ComputeBuffer mStaticSphereBuffer;
    private ComputeBuffer mDynamicSphereBuffer;
    private ComputeBuffer mMeshObjectBuffer;
    private ComputeBuffer mVertexBuffer;
    private ComputeBuffer mIndexBuffer;
    private int mFrameCount;
    private bool mIsRenderingStarted;


    private void Awake()
    {
        mCamera = GetComponent<Camera>();
        Instance = this;
    }

    private void Start()
    {
        StartCoroutine(RenderingRoutine());
    }

    private void OnDisable()
    {
        ReleaseBuffers();
    }
    private void Update()
    {
        if (UseSampling && !mIsRenderingStarted && transform.hasChanged)
        {
            mCurrentSample = 0;
            transform.hasChanged = false;
        }

        if (Input.GetKeyDown(KeyCode.Alpha1)) 
        {
            TakeScreenshot();
        }
    }
  
    public void RegisterObject(RayTracingObject obj)
    {
        mRayTracingObjects.Add(obj);
        mMeshObjectsNeedRebuilding = true;
    }

    public void RegisterDynamicSphere(Sphere sphere)
    {
        mDynamicSpheres.Add(sphere);
        mMeshObjectsNeedRebuilding = true;
    }

    public void RegisterStaticSpheres(List<Sphere> spheres) 
    {
        RayTraceUtility.CreateComputeBuffer(ref mStaticSphereBuffer, spheres, 56);
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

        SetComputeBuffer("_Spheres", mStaticSphereBuffer);
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

    private void ReleaseBuffers()
    {
        mStaticSphereBuffer?.Release();
        mMeshObjectBuffer?.Release();
        mVertexBuffer?.Release();
        mIndexBuffer?.Release();
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

        RayTraceUtility.CreateComputeBuffer(ref mMeshObjectBuffer, mMeshObjects, 72);
        RayTraceUtility.CreateComputeBuffer(ref mVertexBuffer, mVertices, 12);
        RayTraceUtility.CreateComputeBuffer(ref mIndexBuffer, mIndices, 4);
    }

    private void SetComputeBuffer(string name, ComputeBuffer buffer)
    {
        if (buffer != null)
        {
            RayTracingShader.SetBuffer(0, name, buffer);
        }
    }

    private bool mDirectorySetup = false;

    private void DirectorySetup() 
    {
        if (!mDirectorySetup) 
        {
            if (UnityEngine.Windows.Directory.Exists("Render"))
            {
                Debug.Log("Render directory exists...");
            }
            else
            {
                Debug.Log("Creating directory for render...");
                UnityEngine.Windows.Directory.CreateDirectory("Render");
            }

            mDirectorySetup = true;
        }
    }

    private IEnumerator RenderingRoutine() 
    {
        mIsRenderingStarted = true;

        // Initial wait for waiting everything up and ready
        yield return 1.0f;

        // Render routine
        for(int i = 0; i < MaxRenderCount; i++) 
        {
            Debug.Log("Rendering Frame:" + mFrameCount);
            yield return new WaitForSeconds(FrameRenderDelay);

            if (Record) 
            {
                TakeScreenshot();
                yield return new WaitForSeconds(0.1f);
            }
            
            FrameRendered?.Invoke(mFrameCount);
            mFrameCount++;
            mMeshObjectsNeedRebuilding = true;
        }
    }

    private void TakeScreenshot() 
    {
        DirectorySetup();
        ScreenCapture.CaptureScreenshot("Render/Frame_" + mFrameCount.ToString("00000") + ".png");
    }
}
