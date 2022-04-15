using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshFilter))]
public class RayTracingObject : MonoBehaviour
{
    private void Start()
    {
        RayTracingMaster.Instance.RegisterObject(this);
        RayTracingMaster.Instance.FrameRendered += OnFrameRendered;
    }

    private void OnFrameRendered(int frameCount)
    {
        transform.Rotate(Vector3.up, 1.0f);
    }

    private void OnDisable()
    {
        RayTracingMaster.Instance.UnregisterObject(this);
    }
}
