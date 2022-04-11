using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Rotator : MonoBehaviour
{
    public float Distance;
    public float RotSpeed;
    public float DistanceSpeed;

    private float rY;
    private Vector3 mStartPos;
    private float mT;

    // Start is called before the first frame update
    void Start()
    {
        mStartPos = transform.position;
    }

    // Update is called once per frame
    void Update()
    {
        rY += Time.deltaTime * RotSpeed;
        var rotation = Quaternion.Euler(30.0f, rY, 0);

        var position = rotation * new Vector3(0.0f, 0.0f, -Distance) + mStartPos;

        transform.rotation = rotation;
        transform.position = position;

        Distance = Mathf.Sin(mT * DistanceSpeed) * 6.0f;

        mT += Time.deltaTime;
    }
}
