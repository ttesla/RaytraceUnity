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
    }

    private void OnDisable()
    {
        RayTracingMaster.Instance.UnregisterObject(this);
    }
}
