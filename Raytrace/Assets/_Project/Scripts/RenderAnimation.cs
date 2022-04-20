using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RenderAnimation : MonoBehaviour
{
    [Header("Dynamic Objects")]
    public Transform MiddleCubeTrans;
    public Transform CamTrans;
    public float CamWaveDistance;
    public Transform[] DynamicSpheres;

    [Header("Spheres")]
    public Vector2 SphereRadius;
    public uint SpheresMax;
    public float SpherePlacementRadius;
    public int SphereSeed;

    private float mCamY;

    void Start()
    {
        InitStaticSpheres();

        mCamY = CamTrans.position.y;
        RayTracingMaster.Instance.FrameRendered += OnFrameRendered;
    }

    private void OnFrameRendered(int frameCount)
    {
        // Cube rotate
        MiddleCubeTrans.Rotate(Vector3.up, 1.5f, Space.World);
        
        // Cam rotate and swing
        CamTrans.RotateAround(Vector3.zero, Vector3.up, -1.0f);
        var camPos = CamTrans.position;
        camPos.y = mCamY + Mathf.Sin(Mathf.PI * (frameCount / 90f)) * CamWaveDistance;
        CamTrans.position = camPos;

        foreach(var sphere in DynamicSpheres) 
        {
            sphere.RotateAround(Vector3.zero, Vector3.up, -2.0f);
        }
    }

    private void InitStaticSpheres() 
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
                    sphere.albedo = metal ? Vector3.zero : new Vector3(color.r, color.g, color.b);
                    sphere.specular = metal ? new Vector3(color.r, color.g, color.b) : new Vector3(0.04f, 0.04f, 0.04f);
                    sphere.smoothness = Random.Range(0.75f, 1.0f);
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

        RayTracingMaster.Instance.RegisterStaticSpheres(spheres);
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
}
