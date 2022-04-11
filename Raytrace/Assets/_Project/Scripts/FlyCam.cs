using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FlyCam : MonoBehaviour
{
    public Transform CamTrans;
    public float RotSpeed;
    public float MoveSpeed;

    private void Awake()
    {
        if(CamTrans == null) 
        {
            CamTrans = GetComponent<Camera>().transform;
        }
    }

    void Update()
    {
        var horizontalVal = Input.GetAxis("Mouse X") * Time.deltaTime * RotSpeed;
        var verticalVal = Input.GetAxis("Mouse Y") * Time.deltaTime * RotSpeed;

        transform.Rotate(Vector3.up, horizontalVal);
        transform.Rotate(Vector3.left, verticalVal);
        var euler = transform.eulerAngles;
        euler.z = 0.0f;
        transform.rotation = Quaternion.Euler(euler);

        var move = Input.GetAxis("Vertical");
        transform.position += transform.forward * (move * MoveSpeed * Time.deltaTime);
    }
}
