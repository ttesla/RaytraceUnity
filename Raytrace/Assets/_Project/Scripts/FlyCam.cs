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
        if (Input.GetMouseButton(0)) 
        {
            Cursor.lockState = CursorLockMode.Locked;
            var horizontalVal = Input.GetAxis("Mouse X") * Time.deltaTime * RotSpeed;
            var verticalVal = Input.GetAxis("Mouse Y") * Time.deltaTime * RotSpeed;

            transform.Rotate(Vector3.up, horizontalVal);
            transform.Rotate(Vector3.left, verticalVal);
            var euler = transform.eulerAngles;
            euler.z = 0.0f;
            transform.rotation = Quaternion.Euler(euler);

            var moveVector = new Vector3(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"), 0.0f);

            transform.position += (transform.forward * moveVector.y + transform.right * moveVector.x) * MoveSpeed * Time.deltaTime;

            //transform.position += transform.rotation* moveVector * MoveSpeed * Time.deltaTime;
        }

        if (Input.GetKeyDown(KeyCode.Escape)) 
        {
            Cursor.lockState = CursorLockMode.None;
        }
    }
}
